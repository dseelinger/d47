using D47.Core.Journal;

namespace D47.Core.Audio;

/// <summary>What the Commander can do to the ambient music by hand.</summary>
public enum MusicAction
{
    Pause,
    Resume,
    Next,
    Previous,
}

/// <summary>What the audio player tool does to the player.</summary>
public enum AudioPlayerAction
{
    Open,
    Close,
    Toggle,
}

/// <summary>
/// What the transport controls show: the track, whether it is playing, whether the Commander paused it, and
/// whether it faded out with nothing happening.
/// </summary>
public sealed record MusicState(string? Track, bool Playing, bool Paused, bool Idle = false, string? Folder = null);

/// <summary>
/// The ambience layer and its transport: follows the situation, starts the next track, and holds the
/// Commander's pause. Pause is session state; the mute setting is separate and saved. Nothing plays
/// until <see cref="GameRunning"/> says Elite is running. A one-shot folder plays one cue that keeps the
/// slot to its end, then hands back to the situation current at that moment.
/// After <see cref="IdleWatch.Threshold"/> with nothing happening the track fades out and is held, and it
/// fades back in on the next activity.
/// </summary>
public sealed class AmbientMusic(
    AudioArbiter audio,
    Func<CueLibrary> library,
    Random? shuffle = null,
    MusicRejects? rejects = null,
    Func<string, bool>? deleteFile = null)
{
    public static readonly TimeSpan IdleFadeOut = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan WakeFadeIn = TimeSpan.FromSeconds(3);

    private readonly Lock _gate = new();

    private Ambience? _drawn;
    private readonly HashSet<string> _deletedOwn = new(StringComparer.OrdinalIgnoreCase);
    private MusicTrack? _current;
    private bool _paused;
    private bool _running;
    private string? _cue;
    private string? _spent;
    private GameStatus? _status;
    private string? _track;
    private bool _atSettlement;
    private bool _inGroundConflict;
    private readonly IdleWatch _watch = new();
    private string? _target;
    private bool _idle;

    /// <summary>1 when a voice turn or a transport key happened since the last <see cref="Follow"/>.</summary>
    private int _stirred;

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
    /// Points the ambience at the current situation, at the tick's time, given this tick's journal events. A
    /// change of folder shelves the old track and continues the new folder's shelved track, or starts its next
    /// one. A one-shot folder plays one track instead, and a change during it waits for it to end. Only the
    /// tick calls it, and it is the only source of time for going idle. <paramref name="atSettlement"/> and
    /// <paramref name="inGroundConflict"/> are <see cref="SettlementVisit"/>'s state.
    /// </summary>
    public void Follow(
        GameStatus status,
        string? musicTrack,
        DateTimeOffset now = default,
        IReadOnlyList<JournalEvent>? events = null,
        bool atSettlement = false,
        bool inGroundConflict = false)
    {
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _status = status;
            _track = musicTrack;
            _atSettlement = atSettlement;
            _inGroundConflict = inGroundConflict;
            audio.DropStaleMusic(now);

            var target = Situations.For(status, musicTrack, library(), atSettlement, inGroundConflict);
            var oneShot = Situations.OneShot.Contains(target);
            var changed = Idle(target, events ?? [], now);

            if (!oneShot)
            {
                _spent = null;
            }

            if (_cue is not null)
            {
                if (oneShot && !string.Equals(target, _cue, StringComparison.OrdinalIgnoreCase))
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
            else if (_cue is null && Change(oneShot ? Situations.For(status, library(), atSettlement, inGroundConflict) : target, now))
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

    /// <summary>Feeds the idle watch and fades out when idle starts or back in when it ends. True when either happened.</summary>
    private bool Idle(string target, IReadOnlyList<JournalEvent> events, DateTimeOffset now)
    {
        if (Interlocked.Exchange(ref _stirred, 0) == 1)
        {
            _watch.Stir(now);
        }

        _watch.Observe(events, now);

        if (!string.Equals(target, _target, StringComparison.OrdinalIgnoreCase))
        {
            _target = target;
            _watch.Stir(now);
        }

        var idle = _watch.IsIdle(now);

        if (idle == _idle || _cue is not null)
        {
            return false;
        }

        if (idle)
        {
            _idle = true;

            if (!_paused && !audio.FadeOutMusic(IdleFadeOut) && !audio.Activity.MusicPlaying)
            {
                _current = null;
            }
        }
        else
        {
            Wake();
        }

        return true;
    }

    /// <summary>Ends idle, fading the held track back in, or starting one when none is held. A pause stays.</summary>
    private void Wake()
    {
        _idle = false;

        if (_paused || audio.Mix.Music.Muted)
        {
            return;
        }

        if (audio.MusicHeld)
        {
            audio.ResumeMusic(WakeFadeIn);
        }
        else if (!audio.Activity.MusicPlaying)
        {
            Continue(WakeFadeIn);
        }
    }

    /// <summary>A voice turn started. Safe from any thread; the next <see cref="Follow"/> counts it.</summary>
    public void TurnStarted() => Interlocked.Exchange(ref _stirred, 1);

    private bool Change(string situation, DateTimeOffset now)
    {
        if (!CurrentAmbience.Enter(situation)
            || string.Equals(CurrentAmbience.Folder(library()), audio.MusicFolder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var from = audio.MusicFolder;
        var to = CurrentAmbience.Folder(library());
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

        if (_paused || audio.Mix.Music.Muted || CurrentAmbience.Draw(folder, library()) is not { } track)
        {
            return false;
        }

        var from = audio.MusicFolder;
        var length = from is null ? (TimeSpan?)null : Crossfade.Between(from, folder);
        var faded = audio.ShelveMusic(now, length);
        audio.PlayMusic(track, folder, afterGap: false, faded ? length : null);
        _current = track;
        _cue = folder;
        return true;
    }

    /// <summary>Hands the slot back to the situation current now, continuing its shelved track if it has one.</summary>
    private void EndCue()
    {
        _cue = null;
        CurrentAmbience.Enter(Underlying());
        _current = null;

        if (!_paused)
        {
            Continue(null);
        }
    }

    /// <summary>Drops a live cue without playing anything; the stopped track is the caller's.</summary>
    private void LeaveCue()
    {
        if (_cue is not null)
        {
            _cue = null;
            CurrentAmbience.Enter(Underlying());
        }
    }

    /// <summary>The situation for the last status and track, skipping a one-shot folder.</summary>
    private string Underlying()
    {
        var status = _status ?? new GameStatus();
        var target = Situations.For(status, _track, library(), _atSettlement, _inGroundConflict);
        return Situations.OneShot.Contains(target) ? Situations.For(status, library(), _atSettlement, _inGroundConflict) : target;
    }

    /// <summary>A track ended on its own.</summary>
    public void TrackFinished()
    {
        lock (_gate)
        {
            _current = null;

            if (_cue is not null)
            {
                EndCue();
            }
            else if (_running && !_paused && !_idle)
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
            else if (_running && !_paused && !_idle && !audio.Activity.MusicPlaying)
            {
                StartNext();
            }
        }

        Raise();
    }

    /// <summary>
    /// The library lost tracks: stops every track, held ones included, and starts the next one from the
    /// library as it is now unless paused, idle, muted or Elite is not running.
    /// </summary>
    public void Restart()
    {
        lock (_gate)
        {
            audio.StopMusic();
            _current = null;
            LeaveCue();

            if (_running && !_paused && !_idle && !audio.Mix.Music.Muted)
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
            _watch.Reset();
            _target = null;
            _idle = false;

            if (!running)
            {
                audio.StopMusic();
                _current = null;
                _cue = null;
                _spent = null;
                _drawn = null;
            }
        }

        Raise();
    }

    private Ambience CurrentAmbience => _drawn ??= new Ambience(shuffle, IsRejected);

    private bool IsRejected(MusicTrack track) =>
        IsRejected(rejects, track) || (track.OwnPath is { } path && _deletedOwn.Contains(path));

    private static bool IsRejected(MusicRejects? rejects, MusicTrack track) =>
        rejects is not null
        && track is { PackFolder: { } folder, PackFile: { } file }
        && rejects.Contains(folder, file);

    /// <summary>
    /// Moves the playing track from the Commander's own folder to the Recycle Bin and skips it. A library
    /// track is queued for deletion and skipped when <paramref name="queueLibrary"/> is true, and refused
    /// otherwise. Says what happened.
    /// </summary>
    public string ThumbsDown(bool queueLibrary)
    {
        string said;

        lock (_gate)
        {
            var live = _current is not null && audio.Activity.MusicPlaying;

            if (!live)
            {
                return "No music is playing.";
            }

            if (_current is { OwnPath: { } own } ownTrack)
            {
                said = DeleteOwn(ownTrack.Name, own);
            }
            else if (!queueLibrary || rejects is null)
            {
                return "Thumbs down on library tracks is for the library's maintainer.";
            }
            else if (_current is { PackFolder: { } folder, PackFile: { } file })
            {
                var added = rejects.Add(folder, file);
                var queued = rejects.Count;

                Interlocked.Exchange(ref _stirred, 1);
                _idle = false;
                var next = Skip();

                said = $"{(added ? "Queued" : "Already queued")} {Path.GetFileNameWithoutExtension(file)}. "
                       + $"{queued} track{(queued == 1 ? " is" : "s are")} queued for deletion. {next}";
            }
            else
            {
                return "Thumbs down is not available for this track.";
            }
        }

        Raise();
        return said;
    }

    private string DeleteOwn(string name, string path)
    {
        Interlocked.Exchange(ref _stirred, 1);
        _idle = false;
        audio.SkipMusic();
        _deletedOwn.Add(path);

        var deleted = false;

        try
        {
            deleted = deleteFile?.Invoke(path) == true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reported below as a file that could not be deleted.
        }

        if (!deleted)
        {
            _deletedOwn.Remove(path);
        }

        var next = Skip();
        return deleted
            ? $"Deleted {name} and moved it to the Recycle Bin. {next}"
            : $"Could not delete {name}; it may be locked or read-only. {next}";
    }

    /// <summary>How far the live track has played and how long it is; see <see cref="AudioArbiter.MusicProgress"/>.</summary>
    public TrackProgress? Progress() => audio.MusicProgress();

    /// <summary>Moves the live track to <paramref name="to"/>, counted as Commander activity; see <see cref="AudioArbiter.SeekMusic"/>.</summary>
    public bool Seek(TimeSpan to)
    {
        Interlocked.Exchange(ref _stirred, 1);
        return audio.SeekMusic(to);
    }

    /// <summary>Carries out one transport action and says what happened. Any action ends idle.</summary>
    public string Control(MusicAction action)
    {
        string said;

        lock (_gate)
        {
            Interlocked.Exchange(ref _stirred, 1);
            var waking = _idle;
            _idle = false;

            said = action switch
            {
                MusicAction.Pause => Pause(),
                MusicAction.Resume => Resume(waking ? WakeFadeIn : null),
                MusicAction.Previous => Back(),
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

    private string Resume(TimeSpan? fadeIn)
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
            audio.ResumeMusic(fadeIn);
            return "Music resumed.";
        }

        if (audio.Activity.MusicPlaying)
        {
            return "The music is already playing.";
        }

        return Continue(fadeIn) ? "Music resumed." : NothingToPlay;
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

    private string Back()
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

        var inCue = _cue is not null;
        audio.SkipMusic();

        if (inCue)
        {
            _current = null;
            LeaveCue();
            return Continue() ? "Previous track." : NothingToPlay;
        }

        var (track, restarted) = CurrentAmbience.Previous(library(), _current);
        _current = track;

        if (track is null)
        {
            return NothingToPlay;
        }

        audio.PlayMusic(track, CurrentAmbience.Playing, afterGap: false, fadeIn: null);
        return restarted ? "Restarted this track." : "Previous track.";
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

        if (audio.UnshelveMusic(CurrentAmbience.Folder(library()), fadeIn) is { } shelved)
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

        _current = CurrentAmbience.Next(library());

        if (_current is null)
        {
            return false;
        }

        audio.PlayMusic(_current, CurrentAmbience.Playing, afterGap, fadeIn);
        return true;
    }

    /// <summary>Asks the arbiter too, since "stop" silences the track without passing through here.</summary>
    private MusicState Snapshot()
    {
        var live = _current is not null && audio.Activity.MusicPlaying;
        var idle = live && _idle && !_paused;
        return new MusicState(live ? _current!.Name : null, live && !_paused && !_idle, _paused, idle, live ? audio.MusicFolder : null);
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
