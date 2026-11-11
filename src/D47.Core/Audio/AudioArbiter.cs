using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>What a sound is for, which is also what it outranks.</summary>
public enum AudioChannel
{
    /// <summary>The looping bed under a working turn (Phase 5, #18).</summary>
    Bed = 0,

    /// <summary>Situational ambience (Phase 12, "Ambient music"), started with <see cref="AudioArbiter.PlayMusic"/>.</summary>
    Music = 1,

    /// <summary>A short non-speech marker.</summary>
    Cue = 2,

    /// <summary>Invented radio traffic not addressed to the Commander; yields to an answer and an alert.</summary>
    Overheard = 3,

    /// <summary>An answer, spoken.</summary>
    Speech = 4,

    /// <summary>A journal-triggered danger callout (Phase 8).</summary>
    Alert = 5,
}

/// <summary>One thing to make audible: carries exactly one of <see cref="Clip"/> and <see cref="Arriving"/>.</summary>
public sealed record AudioRequest
{
    public required AudioChannel Channel { get; init; }

    public AudioClip? Clip { get; init; }

    /// <summary>Set in place of <see cref="Clip"/> for speech still arriving from its provider.</summary>
    public ArrivingClip? Arriving { get; init; }

    public string Name => Clip?.Name ?? Arriving?.Name ?? string.Empty;

    /// <summary>The scope a supersede applies to — the turn id, for speech.</summary>
    public string? Group { get; init; }

    public bool Loop { get; init; }

    /// <summary>The text this is speaking, if any.</summary>
    public string? Caption { get; init; }

    /// <summary>The picture name of the speaker, or null for the ship's AI.</summary>
    public string? Picture { get; init; }
}

/// <summary>What the arbiter is doing right now.</summary>
/// <param name="MusicPlaying">
/// Defaulted, so a caller that predates the ambience layer still constructs one of these.
/// </param>
/// <param name="Utterance">
/// Which clip <paramref name="Caption"/> belongs to, or null when nothing is playing.
/// </param>
/// <param name="Picture">The picture name of the speaker whose line is playing, or null.</param>
public sealed record AudioActivity(
    AudioChannel? Channel,
    string? Caption,
    bool BedPlaying,
    bool MusicPlaying = false,
    long? Utterance = null,
    string? Picture = null);

/// <summary>The one queue in front of every audible thing.</summary>
public sealed class AudioArbiter(IAudioSink sink, ILogger<AudioArbiter> logger) : IDisposable
{
    /// <summary>How long a silence separates two routine lines from different groups (#44).</summary>
    private static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(350);

    private static readonly AudioClip GapClip = new(
        "spoken-gap",
        new byte[(int)(AudioFormat.Standard.SampleRate * Gap.TotalSeconds) * AudioFormat.Standard.BytesPerFrame],
        AudioFormat.Standard);

    /// <summary>How many ambience tracks are held to continue later; one more drops the one held longest.</summary>
    public const int MostShelved = 4;

    /// <summary>How long a shelved track can wait to be continued before it is stopped.</summary>
    public static readonly TimeSpan ShelfLife = TimeSpan.FromMinutes(30);

    /// <summary>The silence between two ambience tracks, the Red Book CD pause (#522).</summary>
    public static readonly TimeSpan MusicGap = TimeSpan.FromSeconds(2);

    private static readonly AudioClip MusicGapClip = new(
        "music-gap",
        new byte[(int)(AudioFormat.Standard.SampleRate * MusicGap.TotalSeconds) * AudioFormat.Standard.BytesPerFrame],
        AudioFormat.Standard);

    private readonly Lock _gate = new();
    private bool _listening;
    private readonly List<Pending> _queue = [];
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);

    private long _nextId = 1;
    private Playing? _current;
    private Playing? _bed;
    private long? _music;
    private MusicTrack? _musicTrack;
    private string? _musicFolder;
    private bool _musicHeld;
    private bool _musicFadedOut;

    /// <summary>Paused tracks held under their folder, longest held first, at most one per folder.</summary>
    private readonly List<Shelved> _shelf = [];

    /// <summary>The track to stream once the gap in the music slot finishes, or null when no gap is playing.</summary>
    private MusicTrack? _musicAfterGap;
    private bool _subscribed;
    private AudioMix _mix = AudioMix.Default;

    /// <summary>The group of the most recently started routine or alert line, or null before the first.</summary>
    private string? _lastSpokenGroup;

    /// <summary>The group a playback holds the speech channels for, or null when none does.</summary>
    private string? _playbackGroup;
    private bool _playbackPaused;

    private sealed record Pending(long Id, AudioRequest Request);

    private sealed record Playing(long Id, AudioRequest Request);

    private sealed record Shelved(long Id, MusicTrack Track, string Folder, DateTimeOffset At);

    /// <summary>Raised when everything was cut off — the Commander said stop.</summary>
    public event Action? Silenced;

    /// <summary>Raised whenever what is audible changes.</summary>
    public event Action<AudioActivity>? ActivityChanged;

    /// <summary>Raised when an ambience track reached its end on its own.</summary>
    public event Action? MusicFinished;

    /// <summary>Raised when a queued request played to its own end; not for one stopped, dropped or superseded.</summary>
    public event Action<AudioRequest>? Played;

    /// <summary>Raised when an alert or <see cref="Silence"/> paused a playing playback.</summary>
    public event Action? PlaybackPaused;

    public AudioActivity Activity
    {
        get
        {
            lock (_gate)
            {
                return Snapshot();
            }
        }
    }

    /// <summary>Per-category level, mute and ducking (Phase 12, "#96 Ambient audio mixer").</summary>
    public AudioMix Mix
    {
        get
        {
            lock (_gate)
            {
                return _mix;
            }
        }

        set
        {
            lock (_gate)
            {
                _mix = value;
                Relevel();
            }
        }
    }

    /// <summary>Whether the microphone is open; categories with a duck value drop while it is.</summary>
    public bool Listening
    {
        get
        {
            lock (_gate)
            {
                return _listening;
            }
        }

        set
        {
            lock (_gate)
            {
                if (_listening == value)
                {
                    return;
                }

                _listening = value;
                Relevel();
            }
        }
    }

    /// <summary>
    /// Holds the speech channels for <paramref name="group"/>, or resumes a paused hold: Speech and Overheard
    /// in any other group, queued or playing, are dropped now and refused until <see cref="PausePlayback"/>
    /// or <see cref="EndPlayback"/>.
    /// </summary>
    public void HoldSpeechFor(string group)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);
        AudioActivity activity;

        lock (_gate)
        {
            _playbackGroup = group;
            _playbackPaused = false;

            _queue.RemoveAll(pending => IsOtherSpeech(pending.Request, group));

            if (_current is { } playing && IsOtherSpeech(playing.Request, group))
            {
                sink.Stop(playing.Id);
                _current = null;
            }

            Pump();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>Pauses the hold: its group is dropped and refused until it resumes, and other speech plays again.</summary>
    public void PausePlayback()
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_playbackGroup is null || _playbackPaused)
            {
                return;
            }

            PauseHeld();
            Pump();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>Ends the hold and drops what its group has queued or playing.</summary>
    public void EndPlayback()
    {
        string group;

        lock (_gate)
        {
            if (_playbackGroup is not { } held)
            {
                return;
            }

            group = held;
            _playbackGroup = null;
            _playbackPaused = false;
        }

        DropGroup(group);
    }

    private static bool IsOtherSpeech(AudioRequest request, string group) =>
        request.Channel is AudioChannel.Speech or AudioChannel.Overheard && request.Group != group;

    private void PauseHeld()
    {
        var group = _playbackGroup;
        _playbackPaused = true;
        _queue.RemoveAll(pending => pending.Request.Group == group);

        if (_current is { } playing && playing.Request.Group == group)
        {
            sink.Stop(playing.Id);
            _current = null;
        }
    }

    /// <summary>Whether anything is queued or playing, the bed aside.</summary>
    public bool IsSpeaking
    {
        get
        {
            lock (_gate)
            {
                return _current is not null;
            }
        }
    }

    /// <summary>Subscribes to the sink.</summary>
    public AudioArbiter Start()
    {
        lock (_gate)
        {
            if (!_subscribed)
            {
                sink.Finished += OnSinkFinished;
                _subscribed = true;
            }
        }

        return this;
    }

    public void Enqueue(AudioRequest request)
    {
        if ((request.Clip is null) == (request.Arriving is null))
        {
            throw new ArgumentException("A request carries exactly one of a clip and an arriving clip.", nameof(request));
        }

        if (request.Arriving is not null && (request.Loop || request.Channel == AudioChannel.Bed))
        {
            throw new ArgumentException("An arriving clip plays once and cannot loop.", nameof(request));
        }

        AudioActivity activity;
        var paused = false;

        lock (_gate)
        {
            if (request.Group is { } closed && _closed.Contains(closed))
            {
                logger.LogDebug("{Group} is closed; {Clip} is not queued", closed, request.Name);
                return;
            }

            if (_playbackGroup is { } pausedGroup && _playbackPaused && request.Group == pausedGroup)
            {
                logger.LogDebug("Playback for {Group} is paused; {Clip} is not queued", pausedGroup, request.Name);
                return;
            }

            if (_playbackGroup is { } held && !_playbackPaused)
            {
                if (IsOtherSpeech(request, held))
                {
                    logger.LogDebug("Playback holds speech for {Group}; {Clip} is not queued", held, request.Name);
                    return;
                }

                if (request.Channel == AudioChannel.Alert)
                {
                    PauseHeld();
                    paused = true;
                }
            }

            if (request.Channel == AudioChannel.Bed)
            {
                StartBed(request);
                activity = Snapshot();
            }
            else if (request.Channel == AudioChannel.Music)
            {
                throw new ArgumentException("Music is streamed; start it with PlayMusic.", nameof(request));
            }
            else
            {
                var id = _nextId++;
                _queue.Add(new Pending(id, request));

                // Highest channel first; within a channel, arrival order.
                _queue.Sort(static (left, right) =>
                {
                    var byChannel = right.Request.Channel.CompareTo(left.Request.Channel);
                    return byChannel != 0 ? byChannel : left.Id.CompareTo(right.Id);
                });

                // Only an alert cuts in mid-playback, and the interrupted item is dropped rather than
                // resumed: half a sentence spoken before an interdiction warning does not become worth
                // finishing afterwards.
                if (_current is { } playing
                    && request.Channel == AudioChannel.Alert
                    && playing.Request.Channel < AudioChannel.Alert)
                {
                    logger.LogDebug(
                        "{Channel} supersedes {Interrupted} mid-playback",
                        request.Channel,
                        playing.Request.Channel);

                    sink.Stop(playing.Id);
                    _current = null;
                }

                Pump();
                activity = Snapshot();
            }
        }

        if (paused)
        {
            PlaybackPaused?.Invoke();
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>Drops everything belonging to one group, playing or pending.</summary>
    public void DropGroup(string group)
    {
        AudioActivity activity;

        lock (_gate)
        {
            _queue.RemoveAll(pending => pending.Request.Group == group);

            if (_current is { } playing && playing.Request.Group == group)
            {
                sink.Stop(playing.Id);
                _current = null;
            }

            Pump();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>
    /// Drops a group and refuses anything more in it until <see cref="OpenGroup"/>. A caller that
    /// synthesises ahead of playback has work in flight when it decides to stop, and that work arrives
    /// after the drop; this is what turns it away (#61).
    /// </summary>
    public void CloseGroup(string group)
    {
        lock (_gate)
        {
            _closed.Add(group);
        }

        DropGroup(group);
    }

    /// <summary>Lets a closed group back in.</summary>
    public void OpenGroup(string group)
    {
        lock (_gate)
        {
            _closed.Remove(group);
        }
    }

    /// <summary>Shut up (Phase 5). A playing playback is paused, not ended.</summary>
    public void Silence()
    {
        AudioActivity activity;
        var hadSomething = false;
        var paused = false;

        lock (_gate)
        {
            hadSomething = _current is not null || _bed is not null || _music is not null || _queue.Count > 0;

            if (_playbackGroup is not null && !_playbackPaused)
            {
                _playbackPaused = true;
                paused = true;
            }

            _queue.Clear();
            _current = null;
            _bed = null;
            _lastSpokenGroup = null;

            // Silence is silence.
            ClearLive();
            _shelf.Clear();
            sink.StopAll();
            activity = Snapshot();
        }

        if (hadSomething)
        {
            logger.LogInformation("Silenced");
        }

        if (paused)
        {
            PlaybackPaused?.Invoke();
        }

        Silenced?.Invoke();
        ActivityChanged?.Invoke(activity);
    }

    /// <summary>
    /// The audible consequence of the loop moving: the state's own cue, and the bed running for exactly
    /// as long as the turn does.
    /// </summary>
    public void EnterState(
        LoopState state,
        CueLibrary cues,
        bool cueEnabled = true,
        bool bedEnabled = true)
    {
        if (cueEnabled)
        {
            Enqueue(new AudioRequest { Channel = AudioChannel.Cue, Clip = cues.For(state) });
        }

        if (state == LoopState.Thinking && bedEnabled)
        {
            Enqueue(new AudioRequest
            {
                Channel = AudioChannel.Bed,
                Clip = cues.Bed(),
                Loop = true,
            });
        }
        else
        {
            StopBed();
        }
    }

    /// <summary>
    /// Streams one ambience track from a folder in the music slot, replacing whatever was live and any track
    /// shelved for that folder. With <paramref name="afterGap"/>, <see cref="MusicGap"/> of silence plays in
    /// the slot first. With <paramref name="fadeIn"/>, the track fades in from silence over that time.
    /// </summary>
    public void PlayMusic(
        MusicTrack track,
        string folder = Situations.General,
        bool afterGap = false,
        TimeSpan? fadeIn = null)
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_music is { } playing)
            {
                sink.Stop(playing);
            }

            DropShelved(folder);

            var id = _nextId++;
            _music = id;
            _musicTrack = track;
            _musicFolder = folder;
            _musicHeld = false;
            _musicFadedOut = false;

            if (afterGap)
            {
                _musicAfterGap = track;
                sink.Play(new PlaybackRequest(id, MusicGapClip, Loop: false, GainFor(AudioChannel.Music)));
            }
            else
            {
                _musicAfterGap = null;
                sink.Play(PlaybackRequest.Stream(id, track, GainFor(AudioChannel.Music)));

                if (fadeIn is { } length)
                {
                    sink.Fade(id, 0, TimeSpan.Zero);
                    sink.Fade(id, 1, length);
                }
            }

            activity = Snapshot();
        }

        logger.LogInformation("Music started: {Folder}, {Source}, {File}", folder, MusicSource(track), MusicFile(track));
        ActivityChanged?.Invoke(activity);
    }

    private static string MusicSource(MusicTrack track) => track.OwnPath is null ? "library" : "own";

    private static string MusicFile(MusicTrack track) =>
        track.OwnPath is { } own ? Path.GetFileName(own) : track.PackFile ?? track.Name;

    /// <summary>
    /// Holds the live track, paused, under its folder to continue later; a gap is stopped instead. Drops
    /// tracks shelved longer than <see cref="ShelfLife"/>, then the longest shelved beyond
    /// <see cref="MostShelved"/>. With <paramref name="fadeOut"/>, the track fades out and is held at the
    /// end of the fade instead of being paused. A one-shot folder's track is never shelved: it fades out
    /// and finishes unheard, or stops. True when it faded.
    /// </summary>
    public bool ShelveMusic(DateTimeOffset now, TimeSpan? fadeOut = null)
    {
        AudioActivity activity;
        var faded = false;

        lock (_gate)
        {
            DropStale(now);

            if (_music is not { } playing)
            {
                return false;
            }

            if (_musicAfterGap is not null || _musicTrack is null || _musicFolder is null
                || (Situations.OneShot.Contains(_musicFolder) && (_musicHeld || fadeOut is null)))
            {
                sink.Stop(playing);
            }
            else if (Situations.OneShot.Contains(_musicFolder))
            {
                sink.Fade(playing, 0, fadeOut!.Value);
                faded = true;
            }
            else
            {
                if (!_musicHeld && fadeOut is { } length)
                {
                    sink.Fade(playing, 0, length, holdAtEnd: true);
                    faded = true;
                }
                else if (!_musicHeld)
                {
                    sink.Pause(playing);
                }

                DropShelved(_musicFolder);
                _shelf.Add(new Shelved(playing, _musicTrack, _musicFolder, now));

                while (_shelf.Count > MostShelved)
                {
                    sink.Stop(_shelf[0].Id);
                    _shelf.RemoveAt(0);
                }
            }

            ClearLive();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
        return faded;
    }

    /// <summary>
    /// Continues the track shelved for a folder as the live track, replacing whatever was live, fading it in
    /// over <paramref name="fadeIn"/> from where its fade is. Null when that folder has none.
    /// </summary>
    public MusicTrack? UnshelveMusic(string folder, TimeSpan? fadeIn = null)
    {
        AudioActivity activity;
        Shelved shelved;

        lock (_gate)
        {
            var index = _shelf.FindIndex(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                return null;
            }

            shelved = _shelf[index];
            _shelf.RemoveAt(index);

            if (_music is { } playing)
            {
                sink.Stop(playing);
            }

            ClearLive();
            _music = shelved.Id;
            _musicTrack = shelved.Track;
            _musicFolder = shelved.Folder;
            sink.SetGain(shelved.Id, GainFor(AudioChannel.Music));
            sink.Resume(shelved.Id);
            sink.Fade(shelved.Id, 1, fadeIn ?? TimeSpan.Zero);
            activity = Snapshot();
        }

        logger.LogInformation(
            "Music resumed: {Folder}, {Source}, {File}", shelved.Folder, MusicSource(shelved.Track), MusicFile(shelved.Track));
        ActivityChanged?.Invoke(activity);
        return shelved.Track;
    }

    /// <summary>Stops every track shelved longer than <see cref="ShelfLife"/>.</summary>
    public void DropStaleMusic(DateTimeOffset now)
    {
        lock (_gate)
        {
            DropStale(now);
        }
    }

    /// <summary>The folder of the live track, held or not, or null when the music slot is empty.</summary>
    public string? MusicFolder
    {
        get
        {
            lock (_gate)
            {
                return _musicFolder;
            }
        }
    }

    /// <summary>Stops the live ambience track and every shelved one, without touching anything else.</summary>
    public void StopMusic()
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_music is null && _shelf.Count == 0)
            {
                return;
            }

            foreach (var shelved in _shelf)
            {
                sink.Stop(shelved.Id);
            }

            _shelf.Clear();

            if (_music is { } playing)
            {
                sink.Stop(playing);
            }

            ClearLive();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>Stops the live ambience track and leaves the shelved ones.</summary>
    public void SkipMusic()
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_music is not { } playing)
            {
                return;
            }

            sink.Stop(playing);
            ClearLive();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>Holds the ambience track at its position. False when there is no track to hold.</summary>
    public bool PauseMusic()
    {
        lock (_gate)
        {
            if (_music is not { } playing)
            {
                return false;
            }

            sink.Pause(playing);
            _musicHeld = true;
            return true;
        }
    }

    /// <summary>
    /// Fades the live track out and holds it in the slot at the end of the fade, so it neither ages nor is
    /// shelved. A gap is stopped instead. False when nothing faded.
    /// </summary>
    public bool FadeOutMusic(TimeSpan fadeOut)
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_music is not { } playing || _musicHeld)
            {
                return false;
            }

            if (_musicAfterGap is null)
            {
                sink.Fade(playing, 0, fadeOut, holdAtEnd: true);
                _musicHeld = true;
                _musicFadedOut = true;
                return true;
            }

            sink.Stop(playing);
            ClearLive();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
        return false;
    }

    /// <summary>
    /// Continues a held ambience track; one held by <see cref="FadeOutMusic"/> fades back in over
    /// <paramref name="fadeIn"/>. False when there is no track to continue.
    /// </summary>
    public bool ResumeMusic(TimeSpan? fadeIn = null)
    {
        lock (_gate)
        {
            if (_music is not { } playing)
            {
                return false;
            }

            sink.Resume(playing);

            if (_musicFadedOut)
            {
                sink.Fade(playing, 1, fadeIn ?? TimeSpan.Zero);
                _musicFadedOut = false;
            }

            _musicHeld = false;
            return true;
        }
    }

    /// <summary>
    /// How far the live track has played and how long it is: null with no live track, and no length and no time
    /// played during the gap before it. Shelved tracks are not reported.
    /// </summary>
    public TrackProgress? MusicProgress()
    {
        lock (_gate)
        {
            if (_music is not { } playing)
            {
                return null;
            }

            return _musicAfterGap is null ? sink.Progress(playing) : new TrackProgress(TimeSpan.Zero, null);
        }
    }

    /// <summary>How far the playing speech request of <paramref name="group"/> has played and how long it is; null when none of that group is playing.</summary>
    public TrackProgress? SpeechProgress(string group)
    {
        lock (_gate)
        {
            return _current is { } playing && playing.Request.Group == group ? sink.Progress(playing.Id) : null;
        }
    }

    /// <summary>
    /// Moves the live track to <paramref name="to"/>, leaving it paused, held or fading as it was. False with no live
    /// track, during the gap before it, and while its length is not known.
    /// </summary>
    public bool SeekMusic(TimeSpan to)
    {
        lock (_gate)
        {
            if (_music is not { } playing || _musicAfterGap is not null || sink.Progress(playing)?.Length is null)
            {
                return false;
            }

            return sink.Seek(playing, to);
        }
    }

    /// <summary>Whether an ambience track is held by <see cref="PauseMusic"/> or <see cref="FadeOutMusic"/>.</summary>
    public bool MusicHeld
    {
        get
        {
            lock (_gate)
            {
                return _music is not null && _musicHeld;
            }
        }
    }

    private void ClearLive()
    {
        _music = null;
        _musicTrack = null;
        _musicFolder = null;
        _musicHeld = false;
        _musicFadedOut = false;
        _musicAfterGap = null;
    }

    private void DropShelved(string folder)
    {
        var index = _shelf.FindIndex(shelved => string.Equals(shelved.Folder, folder, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            sink.Stop(_shelf[index].Id);
            _shelf.RemoveAt(index);
        }
    }

    private void DropStale(DateTimeOffset now)
    {
        for (var i = _shelf.Count - 1; i >= 0; i--)
        {
            if (now - _shelf[i].At > ShelfLife)
            {
                sink.Stop(_shelf[i].Id);
                _shelf.RemoveAt(i);
            }
        }
    }

    public void StopBed()
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_bed is null)
            {
                return;
            }

            sink.Stop(_bed.Id);
            _bed = null;
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    private void StartBed(AudioRequest request)
    {
        if (_bed is not null)
        {
            sink.Stop(_bed.Id);
        }

        var id = _nextId++;
        _bed = new Playing(id, request);
        sink.Play(new PlaybackRequest(id, request.Clip, Loop: true, Gain: GainFor(AudioChannel.Bed)));
    }

    /// <summary>Start the head of the queue if nothing is playing, and re-level the rest either way.</summary>
    private void Pump()
    {
        if (_current is null && _queue.Count > 0)
        {
            var next = _queue[0];

            if (NeedsGap(next.Request))
            {
                // A cue, not speech: it must not show up as a spoken row in the flight recorder or claim the
                // synthesis note waiting for the real line behind it. Claimed for that line's group up front,
                // so the gap is not inserted twice for the same transition once it finishes and Pump runs
                // again.
                var gap = new AudioRequest { Channel = AudioChannel.Cue, Clip = GapClip, Group = next.Request.Group };

                _current = new Playing(_nextId++, gap);
                _lastSpokenGroup = next.Request.Group;
                sink.Play(new PlaybackRequest(_current.Id, gap.Clip, Loop: false, GainFor(AudioChannel.Cue)));
            }
            else
            {
                _queue.RemoveAt(0);
                _current = new Playing(next.Id, next.Request);

                var gain = GainFor(next.Request.Channel);

                sink.Play(next.Request.Arriving is { } arriving
                    ? PlaybackRequest.Streaming(next.Id, arriving, gain)
                    : new PlaybackRequest(next.Id, next.Request.Clip, next.Request.Loop, gain));
            }
        }

        Relevel();
    }

    /// <summary>
    /// Whether a short silence belongs ahead of this line: routine speech following a routine line that
    /// finished on its own from a different group, so an answer to a direct command and an unrelated callout
    /// landing right after it do not read as one continuous sentence (#44). A line that was cut off rather
    /// than finished — Silence, a dropped group, an alert superseding it — leaves no group to compare against,
    /// so nothing here holds up whatever plays next. An alert itself is never held up either; it already cuts
    /// in mid-playback.
    /// </summary>
    private bool NeedsGap(AudioRequest request) =>
        request.Channel is AudioChannel.Speech or AudioChannel.Overheard
        && _lastSpokenGroup is not null
        && _lastSpokenGroup != request.Group;

    /// <summary>Re-states the gain of everything already playing.</summary>
    private void Relevel()
    {
        if (_bed is { } bed)
        {
            sink.SetGain(bed.Id, GainFor(AudioChannel.Bed));
        }

        if (_music is { } music)
        {
            sink.SetGain(music, GainFor(AudioChannel.Music));
        }

        foreach (var shelved in _shelf)
        {
            sink.SetGain(shelved.Id, GainFor(AudioChannel.Music));
        }

        if (_current is { } playing)
        {
            sink.SetGain(playing.Id, GainFor(playing.Request.Channel));
        }
    }

    /// <summary>
    /// What a category plays at right now: its level, muted or not, times its duck factor when
    /// something is being said over it or the microphone is open.
    /// </summary>
    private float GainFor(AudioChannel channel)
    {
        var speaking = _current?.Request.Channel is AudioChannel.Speech or AudioChannel.Alert or AudioChannel.Overheard;

        // Nothing ducks under itself.
        return _mix.For(channel).GainWhile((speaking || _listening) && AudioMix.Ducks(channel));
    }

    private void OnSinkFinished(long playbackId)
    {
        AudioActivity activity;
        var trackEnded = false;
        AudioRequest? played = null;

        lock (_gate)
        {
            if (_music == playbackId && _musicAfterGap is { } next)
            {
                var id = _nextId++;
                _music = id;
                _musicAfterGap = null;
                sink.Play(PlaybackRequest.Stream(id, next, GainFor(AudioChannel.Music)));
                activity = Snapshot();
            }
            else if (_music == playbackId)
            {
                ClearLive();
                trackEnded = true;
                activity = Snapshot();
            }
            else if (_shelf.FindIndex(shelved => shelved.Id == playbackId) is >= 0 and var index)
            {
                // A shelved track that ended, or failed to open, is not the live track ending.
                _shelf.RemoveAt(index);
                return;
            }
            else if (_current?.Id == playbackId)
            {
                // Only a line that ran to its own end sets what the next gap check compares against — one
                // Silence, DropGroup or an alert cut off does not count as a finished thought.
                if (_current.Request.Channel is AudioChannel.Speech or AudioChannel.Alert or AudioChannel.Overheard)
                {
                    _lastSpokenGroup = _current.Request.Group;
                }

                played = _current.Request;
                _current = null;
                Pump();
                activity = Snapshot();
            }
            else
            {
                // A completion for something already stopped is normal: Silence and supersede both race the
                // sink by design, and the loser is this callback.
                return;
            }
        }

        ActivityChanged?.Invoke(activity);

        // Outside the lock, because the answer to it is another Enqueue.
        if (trackEnded)
        {
            MusicFinished?.Invoke();
        }

        if (played is not null)
        {
            Played?.Invoke(played);
        }
    }

    private AudioActivity Snapshot() =>
        new(
            _current?.Request.Channel,
            _current?.Request.Caption,
            _bed is not null,
            _music is not null,
            _current?.Id,
            _current?.Request.Picture);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_subscribed)
            {
                sink.Finished -= OnSinkFinished;
                _subscribed = false;
            }
        }
    }
}
