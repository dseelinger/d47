using D47.Core.Journal;

namespace D47.Core.Audio;

/// <summary>What the Commander can do to the ambient music by hand.</summary>
public enum MusicAction
{
    Pause,
    Resume,
    Next,
}

/// <summary>What the transport controls show: the track, whether it is playing, and whether the Commander paused it.</summary>
public sealed record MusicState(string? Track, bool Playing, bool Paused);

/// <summary>
/// The ambience layer and its transport: follows the situation, starts the next track, and holds the
/// Commander's pause. Pause is session state; the mute setting is separate and saved.
/// </summary>
public sealed class AmbientMusic(AudioArbiter audio, Func<CueLibrary> library, Random? shuffle = null)
{
    private readonly Ambience _ambience = new(shuffle);
    private readonly Lock _gate = new();

    private MusicTrack? _current;
    private bool _paused;

    /// <summary>Raised after the track or the paused state changes, outside any lock.</summary>
    public event Action<MusicState>? Changed;

    public bool Paused
    {
        get
        {
            lock (_gate)
            {
                return _paused;
            }
        }
    }

    public MusicState State
    {
        get
        {
            lock (_gate)
            {
                return Snapshot();
            }
        }
    }

    /// <summary>Points the ambience at the current situation. A change stops the old track.</summary>
    public void Follow(GameStatus status, string? musicTrack)
    {
        lock (_gate)
        {
            if (!_ambience.Enter(Situations.For(status, musicTrack, library())))
            {
                return;
            }

            audio.StopMusic();
            _current = null;

            if (!_paused)
            {
                StartNext();
            }
        }

        Raise();
    }

    /// <summary>A track ended on its own.</summary>
    public void TrackFinished()
    {
        lock (_gate)
        {
            _current = null;

            if (!_paused)
            {
                StartNext();
            }
        }

        Raise();
    }

    /// <summary>The mute setting changed: muting stops the track, unmuting starts one unless paused.</summary>
    public void MuteChanged(bool muted)
    {
        lock (_gate)
        {
            if (muted)
            {
                audio.StopMusic();
                _current = null;
            }
            else if (!_paused && !audio.Activity.MusicPlaying)
            {
                StartNext();
            }
        }

        Raise();
    }

    /// <summary>Carries out one transport action and says what happened.</summary>
    public string Control(MusicAction action)
    {
        string said;

        lock (_gate)
        {
            said = action switch
            {
                MusicAction.Pause => Pause(),
                MusicAction.Resume => Resume(),
                _ => Skip(),
            };
        }

        Raise();
        return said;
    }

    private string Pause()
    {
        if (_paused)
        {
            return "The music is already paused.";
        }

        _paused = true;
        audio.PauseMusic();
        return "Music paused.";
    }

    private string Resume()
    {
        _paused = false;

        if (audio.Mix.Music.Muted)
        {
            return "Ambient music is muted in the Audio mixer.";
        }

        if (audio.MusicHeld)
        {
            audio.ResumeMusic();
            return "Music resumed.";
        }

        if (audio.Activity.MusicPlaying)
        {
            return "The music is already playing.";
        }

        return StartNext() ? "Music resumed." : NothingToPlay;
    }

    private string Skip()
    {
        _paused = false;

        if (audio.Mix.Music.Muted)
        {
            return "Ambient music is muted in the Audio mixer.";
        }

        audio.StopMusic();
        _current = null;
        return StartNext() ? "Next track." : NothingToPlay;
    }

    private const string NothingToPlay = "There is no music to play. Drop tracks into data\\audio\\music.";

    /// <summary>Starts the next track unless muted. False when there is nothing to play.</summary>
    private bool StartNext()
    {
        if (audio.Mix.Music.Muted)
        {
            return false;
        }

        _current = _ambience.Next(library());

        if (_current is null)
        {
            return false;
        }

        audio.PlayMusic(_current);
        return true;
    }

    /// <summary>Asks the arbiter too, since "stop" silences the track without passing through here.</summary>
    private MusicState Snapshot()
    {
        var live = _current is not null && audio.Activity.MusicPlaying;
        return new MusicState(live ? _current!.Name : null, live && !_paused, _paused);
    }

    private void Raise()
    {
        MusicState state;

        lock (_gate)
        {
            state = Snapshot();
        }

        Changed?.Invoke(state);
    }
}
