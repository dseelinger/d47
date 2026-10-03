using D47.Audio;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// The Settings controls for the Commander's own voice: record and stop, play back, delete. The listening
/// microphone is closed while a take runs, so nothing the Commander reads reaches a transcriber.
/// </summary>
internal sealed class OwnVoiceRecording(
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
    private string? _last;
    private bool _paused;
    private bool _wired;

    /// <summary>Raised when what the rows show has changed, on whichever thread changed it.</summary>
    public event Action? Changed;

    public bool Recording => capture.IsRecording;

    public string State()
    {
        if (Recording)
        {
            return $"Recording. Read aloud: \"{OwnVoice.Sentence}\" Press Stop when you finish; it stops by itself "
                + $"after {OwnVoice.MaxLength.TotalSeconds:0} s.";
        }

        lock (_gate)
        {
            if (_last is { } last)
            {
                return last;
            }
        }

        return store.Exists
            ? "A recording of your voice is saved on this PC, protected for this Windows user."
            : $"No recording. Press Record, then read aloud: \"{OwnVoice.Sentence}\"";
    }

    /// <summary>Starts a take, or stops the one running.</summary>
    public void Toggle()
    {
        if (Recording)
        {
            capture.Stop();
            return;
        }

        if (!_wired)
        {
            capture.Finished += OnFinished;
            _wired = true;
        }

        _paused = pauseListening();

        if (capture.Start(inputDevice(), OwnVoice.MaxLength) is { } failure)
        {
            Resume();
            Say(failure);
            return;
        }

        Say(null);
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
        Resume();

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
