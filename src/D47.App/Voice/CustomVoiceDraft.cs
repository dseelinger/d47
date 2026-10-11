using D47.Audio;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// The take Add a voice holds before it is saved: recorded through <see cref="OwnVoiceRecording"/> or decoded
/// from a file on the thread pool. A take whose sound would be refused is dropped at once, with the reason.
/// </summary>
public sealed class CustomVoiceDraft(
    CustomVoices store,
    OwnVoiceRecording recording,
    IAudioFileDecoder decoder,
    Action<AudioClip> play,
    ILogger logger) : IDisposable
{
    private readonly Lock _gate = new();
    private float[]? _samples;
    private int _sampleRate;
    private string? _source;
    private string? _refusal;
    private string? _refusalDetail;
    private bool _decoding;

    /// <summary>Raised when what the page shows has changed, on whichever thread changed it.</summary>
    public event Action? Changed;

    public bool HasTake
    {
        get
        {
            lock (_gate)
            {
                return _samples is not null;
            }
        }
    }

    public bool Decoding
    {
        get
        {
            lock (_gate)
            {
                return _decoding;
            }
        }
    }

    /// <summary>Why the last take was not kept, or null.</summary>
    public string? Refusal
    {
        get
        {
            lock (_gate)
            {
                return _refusal;
            }
        }
    }

    /// <summary>Where the refused take came from and how long it was.</summary>
    public string? RefusalDetail
    {
        get
        {
            lock (_gate)
            {
                return _refusalDetail;
            }
        }
    }

    /// <summary>The file the held take came from and its length, or null for a recording.</summary>
    public string? Source
    {
        get
        {
            lock (_gate)
            {
                return _samples is null ? null : _source;
            }
        }
    }

    /// <summary>The span of the held take a save keeps, or null without one.</summary>
    public (TimeSpan From, TimeSpan To)? Kept
    {
        get
        {
            lock (_gate)
            {
                if (_samples is not { } samples)
                {
                    return null;
                }

                var (start, length) = CustomVoices.Kept(samples, _sampleRate);

                return (Seconds(start, _sampleRate), Seconds(start + length, _sampleRate));
            }
        }
    }

    /// <summary>Starts a take from the microphone, or stops the one running.</summary>
    /// <returns>Why it could not start, or null.</returns>
    public string? Record()
    {
        if (recording.RecordingCustom)
        {
            recording.Stop();
            return null;
        }

        Clear();

        return recording.RecordCustom(take => Hold(take.Failure, take.Samples, take.SampleRate, null));
    }

    /// <summary>Decodes <paramref name="path"/> on the thread pool and holds it as the take.</summary>
    public async Task ImportAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        Clear();

        lock (_gate)
        {
            _decoding = true;
        }

        Changed?.Invoke();

        var name = Path.GetFileName(path);

        try
        {
            var decoded = await Task.Run(() => decoder.Decode(path)).ConfigureAwait(false);

            Hold(null, decoded.Samples, decoded.SampleRate, name);
        }
        catch (AudioDecodeException ex)
        {
            Hold(ex.Message, [], 1, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "A file chosen for a custom voice could not be read");
            Hold($"The file could not be read: {ex.Message}", [], 1, name);
        }
    }

    /// <summary>Plays what a save would keep.</summary>
    public void Play()
    {
        AudioClip clip;

        lock (_gate)
        {
            if (_samples is not { } samples)
            {
                return;
            }

            clip = CustomVoices.Preview("Custom voice take", samples, _sampleRate);
        }

        play(clip);
    }

    /// <summary>Saves the held take as a custom voice and lets it go, or returns why it was refused.</summary>
    public string? Save(string name, string? gender, string? pitch, string? pace)
    {
        float[] samples;
        int sampleRate;

        lock (_gate)
        {
            if (_samples is null)
            {
                return "Record or choose a file first.";
            }

            samples = _samples;
            sampleRate = _sampleRate;
        }

        string? refused;

        try
        {
            refused = store.Save(name, gender, pitch, pace, samples, sampleRate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or System.Security.Cryptography.CryptographicException)
        {
            logger.LogError(ex, "A custom voice could not be saved");
            refused = $"It could not be saved: {ex.Message}";
        }

        if (refused is null)
        {
            Clear();
        }

        return refused;
    }

    /// <summary>Drops the held take and any refusal.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            Wipe();
            _refusal = null;
            _refusalDetail = null;
        }

        Changed?.Invoke();
    }

    private void Hold(string? failure, float[] samples, int sampleRate, string? file)
    {
        var length = Seconds(samples.Length, sampleRate);
        var refused = failure ?? CustomVoices.Refuse(samples, sampleRate);

        lock (_gate)
        {
            Wipe();
            _decoding = false;

            if (refused is null)
            {
                _samples = samples;
                _sampleRate = sampleRate;
                _source = file is null ? null : $"{file} · {Clock(length)}";
                _refusal = null;
                _refusalDetail = null;
            }
            else
            {
                Array.Clear(samples);
                _refusal = refused;
                _refusalDetail = file is null ? $"Recorded · {Clock(length)}" : file;
            }
        }

        Changed?.Invoke();
    }

    private void Wipe()
    {
        if (_samples is { } held)
        {
            Array.Clear(held);
        }

        _samples = null;
        _source = null;
    }

    private static TimeSpan Seconds(int samples, int sampleRate) =>
        TimeSpan.FromSeconds((double)samples / Math.Max(1, sampleRate));

    /// <summary>A length as <c>m:ss</c>.</summary>
    internal static string Clock(TimeSpan length)
    {
        var seconds = (int)Math.Round(length.TotalSeconds);

        return $"{seconds / 60}:{seconds % 60:00}";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Wipe();
        }
    }
}
