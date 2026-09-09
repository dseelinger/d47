using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// The microphone, running continuously into whatever <see cref="ICaptureSink"/> it was handed — the
/// gate, or the echo canceller in front of it.
/// </summary>
public sealed class WasapiMicrophone(ICaptureSink sink, ILogger<WasapiMicrophone> logger) : IDisposable
{
    /// <summary>What Whisper wants: 16 kHz mono.</summary>
    public const int SampleRate = 16000;

    private WasapiCapture? _capture;
    private MediaFoundationResampler? _resampler;
    private BufferedWaveProvider? _incoming;
    private string? _openDevice;
    private bool _disposed;

    private readonly Lock _lifecycle = new();

    /// <summary>Whether audio is actually flowing.</summary>
    public bool IsCapturing { get; private set; }

    /// <summary>Why capture is not running, when it is not.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>The friendly name of the device currently open, for saying which one went quiet.</summary>
    public string? OpenDeviceName { get; private set; }

    /// <summary>The input devices offered, as id/name pairs for the settings picker.</summary>
    public static IReadOnlyList<(string Id, string Name)> Devices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            return
            [
                .. enumerator
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .Select(device => (device.ID, device.FriendlyName)),
            ];
        }
        catch (Exception)
        {
            // A machine with no audio subsystem at all.
            return [];
        }
    }

    /// <summary>What the system default currently resolves to, without opening anything.</summary>
    public static string? DefaultDeviceName()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            // Communications, matching Open: reporting the multimedia default would name a different device
            // from the one d47 would actually listen on.
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).FriendlyName;
        }
        catch (Exception)
        {
            return null;
        }
    }

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

            Stop();

            try
            {
                using var enumerator = new MMDeviceEnumerator();

                var device = deviceId is { Length: > 0 }
                    ? enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(candidate => candidate.ID == deviceId)
                    : enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);

                if (device is null)
                {
                    // A blank selection producing a silent default is the failure the checklist calls out by
                    // name: a turn reports no speech detected with nothing indicating why.
                    Unavailable = deviceId is { Length: > 0 }
                        ? "The selected microphone is not available. Pick another in Settings."
                        : "No microphone is available.";

                    logger.LogWarning("{Reason}", Unavailable);
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
                OpenDeviceName = device.FriendlyName;
                IsCapturing = true;
                Unavailable = null;

                logger.LogInformation(
                    "Listening on {Device} ({Format})", device.FriendlyName, _capture.WaveFormat);
            }
            catch (Exception ex)
            {
                // No microphone is a capability being off, not a startup failure — d47 stays fully usable
                // typed (Phase 3, "Capabilities as state, not guard").
                Unavailable = $"The microphone could not be opened: {ex.Message}";
                logger.LogError(ex, "Could not open the microphone");
                Stop();
            }
        }
    }

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
                sink);
        }
        catch (Exception ex)
        {
            // This is a real-time callback.
            logger.LogError(ex, "Dropping a capture buffer");
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
            logger.LogWarning(ex, "Microphone capture stopped");
        }

        IsCapturing = false;

        // Anything the gate had open is now incomplete.
        sink.Reset();
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
                logger.LogDebug(ex, "The microphone did not stop cleanly");
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
    }
}
