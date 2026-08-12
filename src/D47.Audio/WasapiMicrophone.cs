using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// The microphone, running continuously into <see cref="ListenGate"/>.
/// <para>
/// It runs whenever d47 is running rather than starting on key-down, which is what the
/// checklist's "the key-down path awaits nothing before recording starts" requires: opening a
/// WASAPI capture device takes tens of milliseconds and would land on top of the polling delay
/// the pre-roll is already absorbing. Continuous capture also means the pre-roll has something
/// in it to be retroactive about.
/// </para>
/// <para>
/// <b>Nothing captured here leaves the machine or is written to disk.</b> Audio goes into the
/// gate's ring buffer, is overwritten within the pre-roll window unless the key is held, and
/// reaches a transcriber only for the stretch the Commander actually asked for.
/// </para>
/// </summary>
public sealed class WasapiMicrophone(ListenGate gate, ILogger<WasapiMicrophone> logger) : IDisposable
{
    /// <summary>
    /// What Whisper wants: 16 kHz mono. Resampling once here means the gate, the ring buffer
    /// and the transcriber all agree on a rate without anyone converting twice.
    /// </summary>
    public const int SampleRate = 16000;

    private WasapiCapture? _capture;
    private MediaFoundationResampler? _resampler;
    private BufferedWaveProvider? _incoming;
    private string? _openDevice;
    private bool _disposed;

    private readonly Lock _lifecycle = new();

    /// <summary>Whether audio is actually flowing. False is a state to report, not an error.</summary>
    public bool IsCapturing { get; private set; }

    /// <summary>Why capture is not running, when it is not.</summary>
    public string? Unavailable { get; private set; }

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
            // A machine with no audio subsystem at all. An empty list is the honest answer and
            // the settings row renders as "none found" rather than failing to render.
            return [];
        }
    }

    /// <summary>
    /// Opens the chosen device, or the system default when null. Safe to call repeatedly; a
    /// device that is already open is left alone rather than being cycled.
    /// </summary>
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
                    // A blank selection producing a silent default is the failure the checklist
                    // calls out by name: a turn reports no speech detected with nothing
                    // indicating why. Named here so the panel can say which device went away.
                    Unavailable = deviceId is { Length: > 0 }
                        ? "The selected microphone is not available. Pick another in Settings."
                        : "No microphone is available.";

                    logger.LogWarning("{Reason}", Unavailable);
                    return;
                }

                _capture = new WasapiCapture(device);
                _capture.DataAvailable += OnData;
                _capture.RecordingStopped += OnStopped;

                // The device decides its own format; d47 converts. Forcing a format on a
                // capture device is how a perfectly good microphone becomes "unsupported".
                _incoming = new BufferedWaveProvider(_capture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(2),
                };

                _resampler = new MediaFoundationResampler(
                    _incoming,
                    WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1))
                {
                    ResamplerQuality = 60,
                };

                _capture.StartRecording();

                _openDevice = deviceId;
                IsCapturing = true;
                Unavailable = null;

                logger.LogInformation(
                    "Listening on {Device} ({Format})", device.FriendlyName, _capture.WaveFormat);
            }
            catch (Exception ex)
            {
                // No microphone is a capability being off, not a startup failure — d47 stays
                // fully usable typed (list.md Phase 3, "Capabilities as state, not guard").
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

            // Drained here rather than on a timer: the resampler is pull-based, and this
            // callback is the only thing that knows new input arrived.
            var bytes = new byte[4096];
            var samples = new float[bytes.Length / sizeof(float)];

            int read;

            while ((read = _resampler.Read(bytes, 0, bytes.Length)) > 0)
            {
                var count = read / sizeof(float);
                Buffer.BlockCopy(bytes, 0, samples, 0, read);
                gate.Write(samples.AsSpan(0, count));
            }
        }
        catch (Exception ex)
        {
            // This is a real-time callback. It must never throw into NAudio's capture thread,
            // which would stop recording silently and leave push-to-talk looking broken.
            logger.LogError(ex, "Dropping a capture buffer");
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

        // Anything the gate had open is now incomplete. Emitting a half-utterance would
        // transcribe a sentence the Commander did not finish saying.
        gate.Abandon();
    }

    private void Stop()
    {
        IsCapturing = false;

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
