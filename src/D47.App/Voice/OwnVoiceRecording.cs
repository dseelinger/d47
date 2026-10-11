using D47.Audio;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// The microphone takes of the Custom voices page: Your voice, saved when the take stops, and a take for Add a
/// voice, handed back to the caller. One take runs at a time, and the listening microphone is closed while it
/// does, so nothing the Commander reads reaches a transcriber.
/// </summary>
public sealed class OwnVoiceRecording(
    OwnVoice store,
    OwnVoiceCapture capture,
    Func<string?> inputDevice,
    Func<bool> pauseListening,
    Action resumeListening,
    Action<AudioClip> play,
    ILogger logger) : IDisposable
{
    internal const string PlaybackGroup = "own-voice";

    private readonly Lock _gate = new();
    private readonly System.Diagnostics.Stopwatch _elapsed = new();
    private string? _last;
    private bool _paused;
    private bool _wired;
    private Action<OwnVoiceTake>? _taken;

    /// <summary>Raised when what the rows show has changed, on whichever thread changed it.</summary>
    public event Action? Changed;

    public bool Recording => capture.IsRecording;

    /// <summary>Whether a recording of Your voice is saved.</summary>
    public bool Recorded => store.Exists;

    /// <summary>Whether the running take is for Add a voice rather than Your voice.</summary>
    public bool RecordingCustom
    {
        get
        {
            lock (_gate)
            {
                return Recording && _taken is not null;
            }
        }
    }

    /// <summary>How long the running take has recorded.</summary>
    public TimeSpan Elapsed => _elapsed.Elapsed;

    /// <summary>What the Your voice row says.</summary>
    public string State()
    {
        if (Recording && !RecordingCustom)
        {
            return $"Recording. Press Stop when you finish; it stops by itself after {OwnVoice.MaxLength.TotalSeconds:0} s.";
        }

        lock (_gate)
        {
            if (_last is { } last)
            {
                return last;
            }
        }

        return store.Exists
            ? "You, recorded here · Stories cast your lines in it"
            : "Not recorded. Story lines in your voice need a recording.";
    }

    /// <summary>Starts a take of Your voice, or stops the one running.</summary>
    public void Toggle()
    {
        if (Recording)
        {
            capture.Stop();
            return;
        }

        Say(Start(null));
    }

    /// <summary>Starts a take for Add a voice, handed to <paramref name="taken"/> on the capture thread once it stops.</summary>
    /// <returns>Why it could not start, or null when it did.</returns>
    public string? RecordCustom(Action<OwnVoiceTake> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        if (Recording)
        {
            return "A recording is already running.";
        }

        var failure = Start(taken);
        Changed?.Invoke();
        return failure;
    }

    /// <summary>Stops the running take, whichever it is.</summary>
    public void Stop() => capture.Stop();

    private string? Start(Action<OwnVoiceTake>? taken)
    {
        if (!_wired)
        {
            capture.Finished += OnFinished;
            _wired = true;
        }

        lock (_gate)
        {
            _taken = taken;
        }

        _paused = pauseListening();

        if (capture.Start(inputDevice(), OwnVoice.MaxLength) is { } failure)
        {
            lock (_gate)
            {
                _taken = null;
            }

            Resume();
            return failure;
        }

        _elapsed.Restart();
        return null;
    }

    /// <summary>Plays the recording back, or says on the row why it cannot.</summary>
    public void Play()
    {
        if (store.Clip() is not { } clip)
        {
            Say(store.Exists
                ? "The recording would not decrypt for this Windows user. Delete it and record again."
                : "There is no recording to play.");
            return;
        }

        play(clip);
    }

    /// <summary>The recording played back on the cue channel, which the audio recorder does not keep.</summary>
    internal static AudioRequest Playback(AudioClip clip) => new()
    {
        Channel = AudioChannel.Cue,
        Clip = PcmConverter.ToStandard(clip),
        Group = PlaybackGroup,
    };

    public void Delete()
    {
        store.Delete();
        Say("Your recording is deleted.");
    }

    private void OnFinished(OwnVoiceTake take)
    {
        _elapsed.Stop();
        Resume();

        Action<OwnVoiceTake>? taken;

        lock (_gate)
        {
            taken = _taken;
            _taken = null;
        }

        if (taken is not null)
        {
            taken(take);
            Changed?.Invoke();
            return;
        }

        try
        {
            var refused = take.Failure ?? store.Save(take.Samples, take.SampleRate);
            Array.Clear(take.Samples);

            Say(refused is null
                ? "Saved. Your voice is kept on this PC, protected for this Windows user."
                : $"Not kept. {refused}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or System.Security.Cryptography.CryptographicException)
        {
            logger.LogError(ex, "The Commander's voice could not be saved");
            Say($"Not kept. It could not be saved: {ex.Message}");
        }
    }

    private void Resume()
    {
        if (_paused)
        {
            _paused = false;
            resumeListening();
        }
    }

    private void Say(string? state)
    {
        lock (_gate)
        {
            _last = state;
        }

        Changed?.Invoke();
    }

    public void Dispose()
    {
        capture.Finished -= OnFinished;
        capture.Dispose();
    }
}
