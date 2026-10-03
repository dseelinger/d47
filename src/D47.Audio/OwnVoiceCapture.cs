using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>One take of the Commander's voice, mono at the device's own rate, or why there is none.</summary>
public sealed record OwnVoiceTake(float[] Samples, int SampleRate, string? Failure);

/// <summary>
/// Records one take from a microphone at the device's own format, for the Commander's own voice. It is a
/// capture of its own, never fed to a capture sink, so the take reaches no transcriber and no recorder.
/// </summary>
public sealed class OwnVoiceCapture(ILogger<OwnVoiceCapture> logger) : IDisposable
{
    private readonly Lock _gate = new();

    private WasapiCapture? _capture;
    private MemoryStream? _bytes;
    private long _limitBytes;

    /// <summary>Raised once per take, on the capture thread, after the device has stopped.</summary>
    public event Action<OwnVoiceTake>? Finished;

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _capture is not null;
            }
        }
    }

    /// <summary>Starts a take on the chosen device, or the Default Device when null, stopping itself after <paramref name="limit"/>.</summary>
    /// <returns>Why it could not start, or null when it did.</returns>
    public string? Start(string? deviceId, TimeSpan limit)
    {
        lock (_gate)
        {
            if (_capture is not null)
            {
                return null;
            }

            try
            {
                using var enumerator = new MMDeviceEnumerator();

                var device = deviceId is { Length: > 0 }
                    ? enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(candidate => candidate.ID == deviceId)
                    : enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, WasapiMicrophone.DefaultRole)
                        ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, WasapiMicrophone.DefaultRole)
                        : null;

                if (device is null)
                {
                    return deviceId is { Length: > 0 }
                        ? "The selected microphone is not available. Pick another under Voice Input."
                        : "No microphone is available.";
                }

                var capture = new WasapiCapture(device);

                _bytes = new MemoryStream();
                _limitBytes = (long)(limit.TotalSeconds * capture.WaveFormat.AverageBytesPerSecond);
                capture.DataAvailable += OnData;
                capture.RecordingStopped += OnStopped;
                _capture = capture;
                capture.StartRecording();

                logger.LogInformation("Recording the Commander's voice on {Device} ({Format})", device.FriendlyName, capture.WaveFormat);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not open the microphone to record the Commander's voice");
                Release()?.Dispose();
                return $"The microphone could not be opened: {ex.Message}";
            }
        }
    }

    /// <summary>Ends the take; <see cref="Finished"/> follows once the device has stopped.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _capture?.StopRecording();
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        lock (_gate)
        {
            if (_bytes is null || _capture is null)
            {
                return;
            }

            var room = (int)Math.Max(0, Math.Min(e.BytesRecorded, _limitBytes - _bytes.Length));
            _bytes.Write(e.Buffer, 0, room);

            if (_bytes.Length >= _limitBytes)
            {
                _capture.StopRecording();
            }
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        OwnVoiceTake take;
        WasapiCapture? stopped;

        lock (_gate)
        {
            if (_capture is null || _bytes is null)
            {
                return;
            }

            if (e.Exception is { } ex)
            {
                logger.LogWarning(ex, "The microphone stopped while recording the Commander's voice");
                take = new OwnVoiceTake([], _capture.WaveFormat.SampleRate, $"The microphone stopped: {ex.Message}");
            }
            else
            {
                take = Mono(_bytes.ToArray(), _capture.WaveFormat);
            }

            stopped = Release();
        }

        // Disposing joins the capture thread, which may be the one raising this.
        _ = Task.Run(() => stopped?.Dispose());

        Finished?.Invoke(take);
    }

    /// <summary>The captured bytes, in whatever format the device chose, as one channel of floats.</summary>
    internal static OwnVoiceTake Mono(byte[] bytes, WaveFormat format)
    {
        using var raw = new RawSourceWaveStream(bytes, 0, bytes.Length, format);
        var provider = raw.ToSampleProvider();
        var channels = provider.WaveFormat.Channels;
        var interleaved = new float[bytes.Length / Math.Max(1, format.BlockAlign) * channels];
        var read = provider.Read(interleaved, 0, interleaved.Length);
        var samples = new float[read / channels];

        for (var frame = 0; frame < samples.Length; frame++)
        {
            var sum = 0f;

            for (var channel = 0; channel < channels; channel++)
            {
                sum += interleaved[(frame * channels) + channel];
            }

            samples[frame] = sum / channels;
        }

        Array.Clear(interleaved);
        Array.Clear(bytes);

        return new OwnVoiceTake(samples, provider.WaveFormat.SampleRate, null);
    }

    /// <summary>Detaches the capture and wipes the take; the caller disposes the capture outside the lock.</summary>
    private WasapiCapture? Release()
    {
        var capture = _capture;

        if (capture is not null)
        {
            capture.DataAvailable -= OnData;
            capture.RecordingStopped -= OnStopped;
            _capture = null;
        }

        if (_bytes is { } bytes)
        {
            Array.Clear(bytes.GetBuffer());
            bytes.Dispose();
            _bytes = null;
        }

        return capture;
    }

    public void Dispose()
    {
        WasapiCapture? capture;

        lock (_gate)
        {
            capture = Release();
        }

        capture?.Dispose();
    }
}
