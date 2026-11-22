using System.Globalization;
using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Podcasts;

/// <summary>
/// The episode in the podcast slot. <see cref="Playing"/> is false while the Commander has paused it; it can be
/// true while D47 speaks over it. <see cref="FromStartOnly"/>: the server sends it only from the start, so it
/// cannot resume or skip.
/// </summary>
public sealed record PodcastPlayerState(
    Uri Feed,
    string Show,
    PodcastEpisode Episode,
    bool Playing,
    bool PausedByCommander,
    TimeSpan Position,
    TimeSpan? Length,
    bool FromStartOnly);

/// <summary>
/// Plays podcast episodes in the arbiter's podcast slot, resuming each where the Commander left it, and keeps the
/// listening record in a file. Ambient music yields while an episode plays or is paused by the Commander. An
/// episode that plays to its end is marked finished and playback stops. Positions are saved on pause, stop, finish
/// and <see cref="SaveNow"/>, and every <see cref="SaveEvery"/> while playing; saves are written off the caller's
/// thread.
/// </summary>
public sealed class PodcastPlayer
{
    public static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan SkipBy = TimeSpan.FromSeconds(30);

    /// <summary>How long <see cref="Play"/> waits for the episode to open before answering without knowing.</summary>
    public static readonly TimeSpan OpenWait = TimeSpan.FromSeconds(15);

    public const string FromStartOnlySentence = "This episode can only play from the beginning.";

    private const string NothingPlaying = "No podcast is playing.";

    private readonly AudioArbiter _audio;
    private readonly AmbientMusic _music;
    private readonly Func<PodcastEpisode, MusicTrack> _open;
    private readonly IFileSystem _files;
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly Action<Action> _background;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Lock _writing = new();
    private readonly Lock _yielding = new();

    private PodcastListening _listening;
    private Playback? _now;
    private DateTimeOffset? _savedAt;
    private bool _wasPlaying;
    private string? _problem;

    /// <summary>The record has changed since it was last written.</summary>
    private bool _dirty;

    /// <param name="open">The episode as a track; nothing may be requested until the track is opened.</param>
    /// <param name="background">Runs a save off the caller's thread; the thread pool when null.</param>
    public PodcastPlayer(
        AudioArbiter audio,
        AmbientMusic music,
        Func<PodcastEpisode, MusicTrack> open,
        IFileSystem files,
        string path,
        ILogger logger,
        Action<Action>? background = null,
        TimeProvider? time = null)
    {
        _audio = audio;
        _music = music;
        _open = open;
        _files = files;
        _path = path;
        _logger = logger;
        _background = background ?? (work => _ = Task.Run(work));
        _time = time ?? TimeProvider.System;
        _listening = Load();

        audio.PodcastFinished += OnFinished;
    }

    /// <summary>Raised outside any lock after an episode starts, pauses, resumes, stops or finishes.</summary>
    public event Action<PodcastPlayerState?>? Changed;

    /// <summary>Why the last episode stopped before its end; null once another starts, or when none did.</summary>
    public string? Problem
    {
        get
        {
            lock (_gate)
            {
                return _problem;
            }
        }
    }

    /// <summary>A copy of the listening record as it is now.</summary>
    public PodcastListening Listening
    {
        get
        {
            lock (_gate)
            {
                return _listening;
            }
        }
    }

    /// <summary>The episode in the slot, or null when none is.</summary>
    public PodcastPlayerState? State
    {
        get
        {
            var playing = _audio.PodcastPlaying;
            var progress = _audio.PodcastProgress();

            lock (_gate)
            {
                if (_now is not { } now)
                {
                    return null;
                }

                var position = progress?.Elapsed ?? _listening.Find(now.Feed, now.Episode.Id)?.Position ?? TimeSpan.Zero;

                return new PodcastPlayerState(
                    now.Feed,
                    now.Show.Title,
                    now.Episode,
                    playing,
                    !playing,
                    position,
                    progress?.Length ?? now.Episode.Duration,
                    now.FromStartOnly);
            }
        }
    }

    /// <summary>
    /// Plays <paramref name="episode"/> of the show at <paramref name="feed"/> from its saved position, or the start,
    /// replacing any episode playing. Answers once the episode has opened, or after <see cref="OpenWait"/>.
    /// </summary>
    public async Task<string> Play(Uri feed, PodcastShow show, PodcastEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(episode);

        var playback = new Playback(feed, show, episode);
        var track = Watch(_open(episode), playback);
        var (previous, progress) = Heard();
        TimeSpan from;

        lock (_gate)
        {
            if (!ReferenceEquals(_now, previous))
            {
                previous = null;
            }

            if (previous is not null && progress is { } heard)
            {
                Remember(previous, heard.Elapsed);
            }

            playback.Replaces = _now?.Id ?? 0;
            from = _listening.Find(feed, episode.Id)?.Position ?? TimeSpan.Zero;
            _now = playback;
            _savedAt = null;
            _wasPlaying = true;
            _problem = null;
        }

        previous?.Opened.TrySetCanceled();
        FollowYield();

        try
        {
            var id = _audio.PlayPodcast(track, from);

            lock (_gate)
            {
                playback.Id = id;
            }
        }
        catch
        {
            lock (_gate)
            {
                if (ReferenceEquals(_now, playback))
                {
                    _now = null;
                }
            }

            FollowYield();
            throw;
        }

        if (previous is not null)
        {
            QueueSave();
        }

        Raise();

        bool canSeek;

        try
        {
            canSeek = await playback.Opened.Task.WaitAsync(OpenWait, _time).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return $"Playing {episode.Title}.";
        }
        catch (OperationCanceledException)
        {
            return $"{episode.Title} was stopped before it started.";
        }
        catch (Exception)
        {
            return $"{episode.Title} could not be played. {playback.Failure ?? "It could not be opened."}";
        }

        if (!canSeek)
        {
            return $"Playing {episode.Title} from the beginning. {FromStartOnlySentence}";
        }

        return from > TimeSpan.Zero ? $"Playing {episode.Title} from {Clock(from)}." : $"Playing {episode.Title}.";
    }

    /// <summary>The Commander's pause; the position is saved.</summary>
    public string Pause()
    {
        if (!_audio.PausePodcast())
        {
            return NothingPlaying;
        }

        var (now, progress) = Heard();

        lock (_gate)
        {
            if (now is null || !ReferenceEquals(_now, now))
            {
                return NothingPlaying;
            }

            if (progress is { } heard)
            {
                Remember(now, heard.Elapsed);
            }

            _wasPlaying = false;
            _savedAt = null;
        }

        QueueSave();
        Raise();
        return "Podcast paused.";
    }

    public string Resume()
    {
        lock (_gate)
        {
            if (_now is null)
            {
                return "No podcast is paused.";
            }
        }

        if (_audio.PodcastPlaying)
        {
            return "The podcast is already playing.";
        }

        if (!_audio.ResumePodcast())
        {
            return "No podcast is paused.";
        }

        lock (_gate)
        {
            _wasPlaying = true;
        }

        Raise();
        return "Podcast resumed.";
    }

    /// <summary>Stops the episode, saving its position, and lets ambient music back in.</summary>
    public string Stop()
    {
        var (now, progress) = Heard();

        lock (_gate)
        {
            if (now is null || !ReferenceEquals(_now, now))
            {
                return NothingPlaying;
            }

            if (progress is { } heard)
            {
                Remember(now, heard.Elapsed);
            }

            _now = null;
            _savedAt = null;
        }

        now.Opened.TrySetCanceled();
        _audio.StopPodcast();
        FollowYield();
        QueueSave();
        Raise();
        return "Podcast stopped.";
    }

    public string SkipBack() => Skip(-SkipBy);

    public string SkipForward() => Skip(SkipBy);

    /// <summary>
    /// Plays the earliest unfinished episode of the same show published after the one playing. With none, says so
    /// and stops.
    /// </summary>
    public Task<string> Next()
    {
        Playback? now;
        PodcastEpisode? next;

        lock (_gate)
        {
            now = _now;

            if (now is null)
            {
                return Task.FromResult(NothingPlaying);
            }

            next = now.Show.Episodes
                .Where(episode => episode.Published > now.Episode.Published
                                  && _listening.Find(now.Feed, episode.Id) is not { Finished: true })
                .MinBy(episode => episode.Published);
        }

        if (next is null)
        {
            Stop();
            return Task.FromResult($"There is no later unfinished episode of {now.Show.Title}.");
        }

        return Play(now.Feed, now.Show, next);
    }

    /// <summary>
    /// Called by the tick. Queues a save every <see cref="SaveEvery"/> while an episode plays, and one when it has
    /// been paused by something other than <see cref="Pause"/>. Does no I/O.
    /// </summary>
    public void Tick(DateTimeOffset now)
    {
        var playing = _audio.PodcastPlaying;
        var save = false;
        var paused = false;

        lock (_gate)
        {
            if (_now is not { Id: not 0 })
            {
                return;
            }

            if (!playing)
            {
                paused = _wasPlaying;
                save = paused;
                _wasPlaying = false;
                _savedAt = null;
            }
            else
            {
                _wasPlaying = true;

                if (_savedAt is not { } saved)
                {
                    _savedAt = now;
                }
                else if (now - saved >= SaveEvery)
                {
                    _savedAt = now;
                    save = true;
                }
            }
        }

        if (save)
        {
            QueueSave(capture: true);
        }

        if (paused)
        {
            _background(Raise);
        }
    }

    /// <summary>Writes the listening record now, with the playing episode's position, when it has changed; for exit.</summary>
    public void SaveNow()
    {
        Capture(null);

        // Waits for a write already running.
        lock (_writing)
        {
            lock (_gate)
            {
                if (!_dirty)
                {
                    return;
                }
            }

            Write();
        }
    }

    private string Skip(TimeSpan by)
    {
        Playback? now;

        lock (_gate)
        {
            now = _now;
        }

        if (now is null)
        {
            return NothingPlaying;
        }

        if (now.FromStartOnly)
        {
            return FromStartOnlySentence;
        }

        if (_audio.PodcastProgress() is not { } progress)
        {
            return "The episode has not started yet.";
        }

        var to = progress.Elapsed + by;

        if (to < TimeSpan.Zero)
        {
            to = TimeSpan.Zero;
        }

        if (progress.Length is { } length && to > length)
        {
            to = length;
        }

        if (!_audio.SeekPodcast(to))
        {
            return "The episode cannot skip yet.";
        }

        return by < TimeSpan.Zero ? "Back 30 seconds." : "Forward 30 seconds.";
    }

    /// <summary>
    /// The arbiter's slot emptied on its own. Until <see cref="AudioArbiter.PlayPodcast"/> has returned the new
    /// episode's id, a finish is the new episode's unless it carries the id of the episode it replaced.
    /// </summary>
    private void OnFinished(long id)
    {
        Playback ended;
        string? problem = null;

        lock (_gate)
        {
            if (_now is not { } now || (now.Id != 0 ? now.Id != id : now.Replaces == id))
            {
                return;
            }

            ended = now;
            _now = null;
            _savedAt = null;

            if (ended.ReachedEnd)
            {
                _listening = _listening.With(ended.Feed, ended.Episode.Id, TimeSpan.Zero, finished: true);
                _dirty = true;
            }
            else
            {
                problem = ended.Failure ?? "The episode stopped before its end.";
                _problem = problem;
            }
        }

        if (problem is not null)
        {
            _logger.LogInformation("Podcast episode stopped before its end: {Problem}", problem);
            ended.Opened.TrySetException(new IOException(problem));
        }

        // Off this thread: the arbiter may still hold its lock here, and the music takes its own before the arbiter's.
        _background(FollowYield);
        QueueSave();
        _background(Raise);
    }

    /// <summary>Holds ambient music while an episode is in the slot. Reads the slot when it runs, so calls may land in any order.</summary>
    private void FollowYield()
    {
        lock (_yielding)
        {
            bool yielding;

            lock (_gate)
            {
                yielding = _now is not null;
            }

            _music.Yield(yielding);
        }
    }

    /// <summary>The episode in the slot and how far it has played, read in that order.</summary>
    private (Playback? Playback, TrackProgress? Progress) Heard()
    {
        Playback? now;

        lock (_gate)
        {
            now = _now;
        }

        return (now, now is null ? null : _audio.PodcastProgress());
    }

    /// <summary>Records <paramref name="position"/> for the episode, keeping whether it is finished. Under <see cref="_gate"/>.</summary>
    private void Remember(Playback playback, TimeSpan position)
    {
        var finished = _listening.Find(playback.Feed, playback.Episode.Id)?.Finished ?? false;
        _listening = _listening.With(playback.Feed, playback.Episode.Id, position, finished);
        _dirty = true;
    }

    /// <summary>Records the playing episode's position, when it is still <paramref name="expected"/> or any when null.</summary>
    private void Capture(Playback? expected)
    {
        var progress = _audio.PodcastProgress();

        lock (_gate)
        {
            if (_now is { } now && progress is { } heard && (expected is null || ReferenceEquals(now, expected)))
            {
                Remember(now, heard.Elapsed);
            }
        }
    }

    private void QueueSave(bool capture = false)
    {
        Playback? playing;

        lock (_gate)
        {
            playing = _now;
        }

        _background(() =>
        {
            if (capture && playing is not null)
            {
                Capture(playing);
            }

            Write();
        });
    }

    /// <summary>Writes the record as it is when the write runs, so writes finishing out of order leave the latest.</summary>
    private void Write()
    {
        lock (_writing)
        {
            string text;

            lock (_gate)
            {
                text = _listening.ToJson();
                _dirty = false;
            }

            try
            {
                _files.WriteText(_path, text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogWarning(ex, "The podcast listening record could not be saved to {Path}", _path);

                lock (_gate)
                {
                    _dirty = true;
                }
            }
        }
    }

    private PodcastListening Load()
    {
        string? text;

        try
        {
            text = _files.ReadText(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The podcast listening record at {Path} could not be read", _path);
            return new PodcastListening();
        }

        if (text is null)
        {
            return new PodcastListening();
        }

        var read = PodcastListening.FromJson(text);

        if (read.Problem is { } problem)
        {
            _logger.LogWarning("{Problem} {Path}", problem, _path);
        }

        return read.Listening;
    }

    private void Raise()
    {
        var state = State;
        Changed?.Invoke(state);
    }

    private static MusicTrack Watch(MusicTrack track, Playback playback) =>
        track with
        {
            Open = () =>
            {
                IPcmStream inner;

                try
                {
                    inner = track.Open();
                }
                catch (Exception ex)
                {
                    playback.Failure ??= Sentence(ex);
                    playback.Opened.TrySetException(ex);
                    throw;
                }

                playback.FromStartOnly = !inner.CanSeek;
                playback.Opened.TrySetResult(inner.CanSeek);
                return new Watched(inner, playback);
            },
        };

    private static string Sentence(Exception ex) =>
        ex is AudioDecodeException or IOException ? ex.Message : "The episode could not be read.";

    private static string Clock(TimeSpan time) =>
        time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);

    /// <summary>One start of one episode, and what its stream has shown.</summary>
    private sealed class Playback(Uri feed, PodcastShow show, PodcastEpisode episode)
    {
        public Uri Feed { get; } = feed;

        public PodcastShow Show { get; } = show;

        public PodcastEpisode Episode { get; } = episode;

        /// <summary>The arbiter's playback id, or 0 until <see cref="AudioArbiter.PlayPodcast"/> returns. Under the player's lock.</summary>
        public long Id { get; set; }

        /// <summary>The playback id of the episode this one replaced, or 0. Under the player's lock.</summary>
        public long Replaces { get; set; }

        /// <summary>Completes with whether the stream can seek once it opens, or faults when it cannot open.</summary>
        public TaskCompletionSource<bool> Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The stream read to its end, and no seek has moved it back since.</summary>
        public volatile bool ReachedEnd;

        public volatile bool FromStartOnly;

        public volatile string? Failure;
    }

    /// <summary>Passes the stream through, recording its end and its failures on the playback.</summary>
    private sealed class Watched(IPcmStream inner, Playback playback) : IPcmStream
    {
        public TimeSpan? Length => inner.Length;

        public bool CanSeek => inner.CanSeek;

        public int Read(Span<byte> buffer)
        {
            try
            {
                var read = inner.Read(buffer);

                if (read == 0 && !buffer.IsEmpty)
                {
                    playback.ReachedEnd = true;
                }

                return read;
            }
            catch (Exception ex)
            {
                playback.Failure ??= Sentence(ex);
                throw;
            }
        }

        public void Seek(TimeSpan to)
        {
            try
            {
                inner.Seek(to);
                playback.ReachedEnd = false;
            }
            catch (Exception ex)
            {
                playback.Failure ??= Sentence(ex);
                throw;
            }
        }

        public void Dispose() => inner.Dispose();
    }
}
