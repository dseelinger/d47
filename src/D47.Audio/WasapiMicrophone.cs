using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// The microphone, running continuously into whatever <see cref="ICaptureSink"/> it was handed — the
/// gate, or the echo canceller in front of it.
/// </summary>
public sealed class WasapiMicrophone : IDefaultDeviceReopener, IDisposable
{
    /// <summary>What Whisper wants: 16 kHz mono.</summary>
    public const int SampleRate = 16000;

    /// <summary>
    /// The Windows role "system default" follows: the Default Device, the row Sound settings shows
    /// first. Not <see cref="Role.Communications"/> — Windows only splits that row off from the
    /// Default Device once a headset or voice-chat app is involved, and a Commander does not look at
    /// it (#65).
    /// </summary>
    internal const Role DefaultRole = Role.Console;

    private readonly ICaptureSink _sink;
    private readonly ILogger<WasapiMicrophone> _logger;
    private readonly IAudioEndpointEnumerator _enumerator;
    private readonly DefaultDeviceFollower _follower;

    private WasapiCapture? _capture;
    private MediaFoundationResampler? _resampler;
    private BufferedWaveProvider? _incoming;
    private string? _openDevice;
    private string? _openEndpointId;
    private bool _disposed;

    private readonly Lock _lifecycle = new();

    public WasapiMicrophone(ICaptureSink sink, ILogger<WasapiMicrophone> logger)
        : this(sink, logger, new WasapiEndpointEnumerator())
    {
    }

    internal WasapiMicrophone(ICaptureSink sink, ILogger<WasapiMicrophone> logger, IAudioEndpointEnumerator enumerator)
    {
        _sink = sink;
        _logger = logger;
        _enumerator = enumerator;
        _follower = new DefaultDeviceFollower(enumerator, DataFlow.Capture, DefaultRole);
    }

    /// <summary>Whether audio is actually flowing.</summary>
    public bool IsCapturing { get; private set; }

    /// <summary>Why capture is not running, when it is not.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>The friendly name of the device currently open, for saying which one went quiet.</summary>
    public string? OpenDeviceName { get; private set; }

    /// <summary>The input devices offered, as id/name pairs for the settings picker.</summary>
    public IReadOnlyList<(string Id, string Name)> Devices() =>
        [.. _enumerator.Active(DataFlow.Capture).Select(device => (device.Id, device.Name))];

    /// <summary>What the system default (the Windows Default Device) currently resolves to, without opening anything.</summary>
    public string? DefaultDeviceName() => _enumerator.Default(DataFlow.Capture, DefaultRole)?.Name;

    /// <summary>The endpoint <paramref name="deviceId"/> names, or the Default Device when it is null.</summary>
    internal AudioEndpoint? Resolve(string? deviceId) => deviceId is { Length: > 0 }
        ? _enumerator.Active(DataFlow.Capture)
            .Where(candidate => candidate.Id == deviceId)
            .Select(candidate => (AudioEndpoint?)candidate)
            .FirstOrDefault()
        : _enumerator.Default(DataFlow.Capture, DefaultRole);

    /// <summary>Opens the chosen device, or the system default when null.</summary>
    public void Open(string? deviceId)
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsCapturing && string.Equals(_openDevice, deviceId, StringComparison.Ordinal))
            {
                return;
            }

            OpenCore(deviceId);
        }
    }

    /// <summary>
    /// Re-opens even when <paramref name="deviceId"/> matches what is already configured — the resolved
    /// endpoint behind "system default" may have moved (#67).
    /// </summary>
    public void Reopen(string? deviceId)
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            OpenCore(deviceId);
        }
    }

    private void OpenCore(string? deviceId)
    {
        Stop();

        try
        {
            var target = Resolve(deviceId);

            using var enumerator = new MMDeviceEnumerator();

            var device = target is { } endpoint
                ? enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(candidate => candidate.ID == endpoint.Id)
                : null;

            if (device is null)
            {
                // A blank selection producing a silent default is the failure the checklist calls out by
                // name: a turn reports no speech detected with nothing indicating why.
                Unavailable = deviceId is { Length: > 0 }
                    ? "The selected microphone is not available. Pick another in Settings."
                    : "No microphone is available.";

                _logger.LogWarning("{Reason}", Unavailable);
                return;
            }

            _capture = new WasapiCapture(device);
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;

            // The device decides its own format; d47 converts.
            _incoming = new BufferedWaveProvider(_capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2),

                // The other half of the ReadFully story, and the more damaging half.
                ReadFully = false,
            };

            _resampler = new MediaFoundationResampler(
                _incoming,
                WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1))
            {
                ResamplerQuality = 60,
            };

            _capture.StartRecording();

            _openDevice = deviceId;
            _openEndpointId = device.ID;
            OpenDeviceName = device.FriendlyName;
            IsCapturing = true;
            Unavailable = null;

            _logger.LogInformation(
                "Listening on {Device} ({Format})", device.FriendlyName, _capture.WaveFormat);
        }
        catch (Exception ex)
        {
            // No microphone is a capability being off, not a startup failure — d47 stays fully usable
            // typed (Phase 3, "Capabilities as state, not guard").
            Unavailable = $"The microphone could not be opened: {ex.Message}";
            _logger.LogError(ex, "Could not open the microphone");
            Stop();
        }
    }

    /// <summary>
    /// Whether the Default Device has moved and settled, and is due to be followed. Call once per tick;
    /// stays true until <see cref="AcknowledgeDefaultDeviceMove"/> (#67).
    /// </summary>
    public bool DefaultDeviceMoved(DateTimeOffset now) => _follower.Poll(now);

    /// <summary>Marks a due default-device move as handled.</summary>
    public void AcknowledgeDefaultDeviceMove() => _follower.Acknowledge();

    /// <summary>Whether the device currently open has itself disappeared, as opposed to merely losing "default".</summary>
    public bool OpenDeviceIsGone() =>
        _openEndpointId is { Length: > 0 } id && !_enumerator.Active(DataFlow.Capture).Any(endpoint => endpoint.Id == id);

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (_incoming is null || _resampler is null || e.BytesRecorded == 0)
        {
            return;
        }

        try
        {
            _incoming.AddSamples(e.Buffer, 0, e.BytesRecorded);

            // Drained here rather than on a timer: the resampler is pull-based, and this callback is the only
            // thing that knows new input arrived.
            Drain(
                _resampler,
                OutputBytesFor(e.BytesRecorded, _incoming.WaveFormat, _resampler.WaveFormat),
                _sink);
        }
        catch (Exception ex)
        {
            // This is a real-time callback.
            _logger.LogError(ex, "Dropping a capture buffer");
        }
    }

    /// <summary>How many bytes of resampled audio the input just handed over is worth.</summary>
    internal static long OutputBytesFor(int inputBytes, WaveFormat input, WaveFormat output) =>
        (long)inputBytes * output.AverageBytesPerSecond / input.AverageBytesPerSecond;

    /// <summary>Moves <paramref name="budget"/> bytes from the resampler into the gate.</summary>
    internal static void Drain(IWaveProvider resampler, long budget, ICaptureSink sink)
    {
        var bytes = new byte[4096];
        var samples = new float[bytes.Length / sizeof(float)];

        while (budget > 0)
        {
            var wanted = (int)Math.Min(budget, bytes.Length);

            // Whole samples only; a partial float would be split across two writes and land in the gate as a
            // click.
            wanted -= wanted % sizeof(float);

            if (wanted == 0)
            {
                return;
            }

            var read = resampler.Read(bytes, 0, wanted);

            if (read <= 0)
            {
                return;
            }

            var count = read / sizeof(float);
            Buffer.BlockCopy(bytes, 0, samples, 0, count * sizeof(float));
            sink.Write(samples.AsSpan(0, count));

            budget -= read;
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is { } ex)
        {
            // Usually the device being unplugged mid-session.
            Unavailable = $"The microphone stopped: {ex.Message}";
            _logger.LogWarning(ex, "Microphone capture stopped");
        }

        IsCapturing = false;

        // Anything the gate had open is now incomplete.
        _sink.Reset();
    }

    /// <summary>Closes the device and stays reusable, which is what "push-to-talk is unbound" means.</summary>
    public void Close()
    {
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _openDevice = null;
            Unavailable = null;
        }
    }

    private void Stop()
    {
        IsCapturing = false;
        OpenDeviceName = null;
        _openEndpointId = null;

        if (_capture is { } capture)
        {
            capture.DataAvailable -= OnData;
            capture.RecordingStopped -= OnStopped;

            try
            {
                capture.StopRecording();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "The microphone did not stop cleanly");
            }

            capture.Dispose();
            _capture = null;
        }

        _resampler?.Dispose();
        _resampler = null;
        _incoming = null;
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        _follower.Dispose();
        (_enumerator as IDisposable)?.Dispose();
    }
}
