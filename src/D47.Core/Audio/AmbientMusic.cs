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
/// Commander's pause. Pause is session state; the mute setting is separate and saved. Nothing plays
/// until <see cref="GameRunning"/> says Elite is running. A one-shot folder plays one cue that keeps the
/// slot to its end, or <see cref="CueLimit"/>, then hands back to the situation current at that moment.
/// </summary>
public sealed class AmbientMusic(AudioArbiter audio, Func<CueLibrary> library, Random? shuffle = null)
{
    /// <summary>The longest a one-shot cue holds the slot.</summary>
    public static readonly TimeSpan CueLimit = TimeSpan.FromSeconds(60);

    private readonly Lock _gate = new();

    private Ambience _ambience = new(shuffle);
    private MusicTrack? _current;
    private bool _paused;
    private bool _running;
    private string? _cue;
    private string? _spent;
    private DateTimeOffset _cueStart;
    private GameStatus? _status;
    private string? _track;

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

    /// <summary>
    /// Points the ambience at the current situation, at the tick's time. A change of folder shelves the old
    /// track and continues the new folder's shelved track, or starts its next one. A one-shot folder plays
    /// one track instead, and a change during it waits for it to end.
    /// </summary>
    public void Follow(GameStatus status, string? musicTrack, DateTimeOffset now = default)
    {
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _status = status;
            _track = musicTrack;
            audio.DropStaleMusic(now);

            var target = Situations.For(status, musicTrack, library());
            var oneShot = Situations.OneShot.Contains(target);
            var changed = false;

            if (!oneShot)
            {
                _spent = null;
            }

            if (_cue is not null)
            {
                if (now - _cueStart >= CueLimit)
                {
                    EndCue(now, fade: true);
                    changed = true;
                }
                else if (oneShot && !string.Equals(target, _cue, StringComparison.OrdinalIgnoreCase))
                {
                    StartCue(target, now);
                    changed = true;
                }
                else
                {
                    return;
                }
            }

            if (oneShot
                && _cue is null
                && !string.Equals(target, _spent, StringComparison.OrdinalIgnoreCase)
                && StartCue(target, now))
            {
                changed = true;
            }
            else if (_cue is null && Change(oneShot ? Situations.For(status) : target, now))
            {
                changed = true;
            }

            if (!changed)
            {
                return;
            }
        }

        Raise();
    }

    private bool Change(string situation, DateTimeOffset now)
    {
        if (!_ambience.Enter(situation)
            || string.Equals(_ambience.Folder(library()), audio.MusicFolder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var from = audio.MusicFolder;
        var to = _ambience.Folder(library());
        var faded = audio.ShelveMusic(now, from is null ? null : Crossfade.Between(from, to));
        _current = null;

        if (!_paused)
        {
            Continue(faded ? Crossfade.Between(from!, to) : null);
        }

        return true;
    }

    /// <summary>Plays one track of a one-shot folder, replacing the live one. False when muted or paused.</summary>
    private bool StartCue(string folder, DateTimeOffset now)
    {
        _spent = folder;

        if (_paused || audio.Mix.Music.Muted || _ambience.Draw(folder, library()) is not { } track)
        {
            return false;
        }

        var from = audio.MusicFolder;
        var length = from is null ? (TimeSpan?)null : Crossfade.Between(from, folder);
        var faded = audio.ShelveMusic(now, length);
        audio.PlayMusic(track, folder, afterGap: false, faded ? length : null);
        _current = track;
        _cue = folder;
        _cueStart = now;
        return true;
    }

    /// <summary>Hands the slot back to the situation current now, continuing its shelved track if it has one.</summary>
    private void EndCue(DateTimeOffset now, bool fade)
    {
        var cue = _cue!;
        _cue = null;
        _ambience.Enter(Underlying());
        var folder = _ambience.Folder(library());
        var length = fade ? Crossfade.Between(cue, folder) : (TimeSpan?)null;
        var faded = fade && audio.ShelveMusic(now, length);
        _current = null;

        if (!_paused)
        {
            Continue(faded ? length : null);
        }
    }

    /// <summary>Drops a live cue without playing anything; the stopped track is the caller's.</summary>
    private void LeaveCue()
    {
        if (_cue is not null)
        {
            _cue = null;
            _ambience.Enter(Underlying());
        }
    }

    /// <summary>The situation for the last status and track, skipping a one-shot folder.</summary>
    private string Underlying()
    {
        var status = _status ?? new GameStatus();
        var target = Situations.For(status, _track, library());
        return Situations.OneShot.Contains(target) ? Situations.For(status) : target;
    }

    /// <summary>A track ended on its own.</summary>
    public void TrackFinished()
    {
        lock (_gate)
        {
            _current = null;

            if (_cue is not null)
            {
                EndCue(default, fade: false);
            }
            else if (_running && !_paused)
            {
                StartNext(afterGap: true);
            }
        }

        Raise();
    }

    /// <summary>The mute setting changed: muting stops every track, unmuting starts one unless paused.</summary>
    public void MuteChanged(bool muted)
    {
        lock (_gate)
        {
            if (muted)
            {
                audio.StopMusic();
                _current = null;
                LeaveCue();
            }
            else if (_running && !_paused && !audio.Activity.MusicPlaying)
            {
                StartNext();
            }
        }

        Raise();
    }

    /// <summary>
    /// Whether Elite is running. Stopping stops every track; starting again leaves the next
    /// <see cref="Follow"/> to start one for the situation it finds.
    /// </summary>
    public void GameRunning(bool running)
    {
        lock (_gate)
        {
            if (running == _running)
            {
                return;
            }

            _running = running;

            if (!running)
            {
                audio.StopMusic();
                _current = null;
                _cue = null;
                _spent = null;
                _ambience = new Ambience(shuffle);
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
        if (!_running)
        {
            return NotRunning;
        }

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

        return Continue() ? "Music resumed." : NothingToPlay;
    }

    private string Skip()
    {
        if (!_running)
        {
            return NotRunning;
        }

        _paused = false;

        if (audio.Mix.Music.Muted)
        {
            return "Ambient music is muted in the Audio mixer.";
        }

        audio.SkipMusic();
        _current = null;
        LeaveCue();
        return StartNext() ? "Next track." : NothingToPlay;
    }

    private const string NotRunning = "Elite is not running.";

    private const string NothingToPlay = "There is no music to play. Drop tracks into data\\audio\\music.";

    /// <summary>Continues the folder's shelved track, or starts its next one. False when nothing plays.</summary>
    private bool Continue(TimeSpan? fadeIn = null)
    {
        if (audio.Mix.Music.Muted)
        {
            return false;
        }

        if (audio.UnshelveMusic(_ambience.Folder(library()), fadeIn) is { } shelved)
        {
            _current = shelved;
            return true;
        }

        return StartNext(fadeIn: fadeIn);
    }

    /// <summary>
    /// Starts the next track unless muted, after <see cref="AudioArbiter.MusicGap"/> when it follows a
    /// finished one. False when there is nothing to play.
    /// </summary>
    private bool StartNext(bool afterGap = false, TimeSpan? fadeIn = null)
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

        audio.PlayMusic(_current, _ambience.Playing, afterGap, fadeIn);
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
