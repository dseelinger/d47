using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Podcasts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Media;

/// <summary>
/// Plays one video at a time, from a file or an HTTPS URL, its soundtrack on <see cref="AudioChannel.Video"/> and its
/// pictures following the soundtrack's position. Opening and decoding run on the video's own thread. While
/// <c>pauseWhileSpeaking</c> is true, D47 speaking an answer or an alert pauses the video, which plays on once D47 is
/// quiet; otherwise speech ducks it. Push-to-talk pauses it until D47 is quiet again. Stop or Cancel, a story starting,
/// and another Media item starting pause it until <see cref="Resume"/>. It does not start or resume while a story plays.
/// Ambient music yields and chatter is refused while it plays.
/// </summary>
public sealed class VideoPlayback
{
    public static readonly TimeSpan SkipBy = TimeSpan.FromSeconds(30);

    private readonly AudioArbiter _audio;
    private readonly Func<bool> _pauseWhileSpeaking;
    private readonly StoryRunPlayer? _story;
    private readonly AmbientMusic? _music;
    private readonly Action<Action> _background;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly Lock _publishing = new();

    private Viewing? _viewing;
    private VideoPause _pause;

    /// <param name="pauseWhileSpeaking">Whether D47 speaking pauses the video; true when null.</param>
    /// <param name="background">Runs work off the calling thread; the thread pool when null.</param>
    public VideoPlayback(
        AudioArbiter audio,
        Func<bool>? pauseWhileSpeaking = null,
        StoryRunPlayer? story = null,
        AmbientMusic? music = null,
        Action<Action>? background = null,
        TimeProvider? time = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(audio);

        _audio = audio;
        _pauseWhileSpeaking = pauseWhileSpeaking ?? (static () => true);
        _story = story;
        _music = music;
        _background = background ?? (work => _ = Task.Run(work));
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;

        _audio.ActivityChanged += activity => _background(() => OnActivity(activity));
        _audio.Silenced += () => Pause(VideoPause.Interrupted);
        _audio.VideoFinished += OnFinished;

        if (story is not null)
        {
            story.Starting += () => Pause(VideoPause.Commander);
        }
    }

    /// <summary>Raised when the video, its state or its pause changed; on any thread. A stall starting is not raised.</summary>
    public event Action? Changed;

    /// <summary>Raised on the calling thread as a video starts or resumes.</summary>
    public event Action? Starting;

    /// <summary>A story is playing, or paused by anything but the Commander, so a video does not start or resume.</summary>
    public bool StoryPlaying => _story?.Now is { Pause: not StoryRunPause.Commander };

    /// <summary>The file or URL being played, or null.</summary>
    public string? Source
    {
        get
        {
            lock (_gate)
            {
                return _viewing?.Source;
            }
        }
    }

    public VideoPause PausedFor
    {
        get
        {
            lock (_gate)
            {
                return _viewing is null ? VideoPause.None : _pause;
            }
        }
    }

    /// <summary>What the video is doing, or null with none.</summary>
    public VideoState? State
    {
        get
        {
            lock (_gate)
            {
                if (_viewing is not { } viewing)
                {
                    return null;
                }

                return viewing.Decoder.Failure is not null ? VideoState.Failed
                    : viewing.Ended ? VideoState.Ended
                    : viewing.Id is null ? VideoState.Opening
                    : _pause != VideoPause.None ? VideoState.Paused
                    : viewing.Decoder.Stalled ? VideoState.Stalled
                    : VideoState.Playing;
            }
        }
    }

    public VideoFailure? Failure => Current?.Decoder.Failure;

    /// <summary>The picture to show now, or null before the first is decoded.</summary>
    public VideoFrame? Frame => Current?.Decoder.Frame;

    /// <summary>The length the file gives, once open.</summary>
    public TimeSpan? Length => Current?.Decoder.Length;

    /// <summary>Where the soundtrack is, which the picture follows.</summary>
    public TimeSpan Position
    {
        get
        {
            if (Current is not { } viewing)
            {
                return TimeSpan.Zero;
            }

            if (viewing.Ended && viewing.Decoder.Length is { } length)
            {
                return length;
            }

            return viewing.Decoder.Expected
                ?? (viewing.Id is not null ? _audio.VideoProgress()?.Elapsed : null)
                ?? viewing.Decoder.From;
        }
    }

    private Viewing? Current
    {
        get
        {
            lock (_gate)
            {
                return _viewing;
            }
        }
    }

    /// <summary>Starting a video pauses <paramref name="book"/>, and the book starting pauses the video, each as the Commander's pause.</summary>
    public void TakesTurnsWith(BookPlayer book)
    {
        ArgumentNullException.ThrowIfNull(book);

        Starting += () =>
        {
            if (book.Now is not null)
            {
                book.Pause(StoryRunPause.Commander);
            }
        };
        book.Starting += () => Pause(VideoPause.Commander);
    }

    /// <summary>Starting a video pauses <paramref name="podcast"/>, and the podcast starting pauses the video, each as the Commander's pause.</summary>
    public void TakesTurnsWith(PodcastPlayer podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);

        Starting += () =>
        {
            if (podcast.State is { Playing: true })
            {
                podcast.Pause();
            }
        };
        podcast.Starting += () => Pause(VideoPause.Commander);
    }

    /// <summary>
    /// Plays <paramref name="source"/>, a file path or an HTTPS URL, from <paramref name="from"/>, replacing any video.
    /// False while a story plays. It opens on the video's own thread; <see cref="State"/> is Opening until then.
    /// </summary>
    public bool Play(string source, TimeSpan from = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (StoryPlaying)
        {
            return false;
        }

        Starting?.Invoke();
        Viewing viewing;

        lock (_gate)
        {
            Close();
            viewing = new Viewing(source, new VideoDecoder(source, from, _time, _logger));
            _viewing = viewing;
            _pause = SpeakingNow() ? VideoPause.Speaking : VideoPause.None;
            viewing.Decoder.Opened += () => OnOpened(viewing);
            viewing.Decoder.Changed += () => Raise();
            viewing.Decoder.Start();
        }

        Raise();
        return true;
    }

    /// <summary>Pauses the video. A Commander pause replaces any other reason.</summary>
    public void Pause(VideoPause reason)
    {
        lock (_gate)
        {
            if (_viewing is not { Ended: false } || reason == VideoPause.None)
            {
                return;
            }

            if (_pause != VideoPause.None)
            {
                if (reason != VideoPause.Commander || _pause == VideoPause.Commander)
                {
                    return;
                }

                _pause = reason;
            }
            else
            {
                _pause = reason;
                _audio.PauseVideo();
            }
        }

        Raise();
    }

    /// <summary>Plays on from where it was paused, for any pause reason. Nothing happens while a story plays.</summary>
    public void Resume() => ResumeWhen(static _ => true);

    /// <summary>Off the calling thread, plays on after a push-to-talk or speaking pause once D47 is not listening, not in a turn and not speaking.</summary>
    public void Settled() => _background(ResumeIfQuiet);

    /// <summary>Moves to <paramref name="to"/>, paused or playing as it was; after the end, plays again from there. False with no video.</summary>
    public bool Seek(TimeSpan to)
    {
        Viewing viewing;

        lock (_gate)
        {
            if (_viewing is not { } current)
            {
                return false;
            }

            viewing = current;
        }

        if (to < TimeSpan.Zero)
        {
            to = TimeSpan.Zero;
        }

        if (viewing.Decoder.Length is { } length && to > length)
        {
            to = length;
        }

        if (viewing.Ended || viewing.Id is null)
        {
            return Play(viewing.Source, to);
        }

        viewing.Decoder.ExpectSeek(to);

        if (!_audio.SeekVideo(to))
        {
            viewing.Decoder.CancelSeek();
            return false;
        }

        Raise();
        return true;
    }

    /// <summary>Moves by <paramref name="by"/>, such as <see cref="SkipBy"/> or its negative.</summary>
    public bool Skip(TimeSpan by) => Seek(Position + by);

    /// <summary>Ends the video and closes its reader.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_viewing is null)
            {
                return;
            }

            Close();
        }

        Raise();
    }

    private bool SpeakingNow() =>
        _pauseWhileSpeaking() && _audio.Activity.Channel is AudioChannel.Speech or AudioChannel.Alert;

    /// <summary>Stops the soundtrack and the reader. Called holding <see cref="_gate"/>.</summary>
    private void Close()
    {
        if (_viewing is not { } viewing)
        {
            return;
        }

        _viewing = null;
        _pause = VideoPause.None;

        if (viewing.Id is not null)
        {
            _audio.StopVideo();
        }

        viewing.Decoder.Dispose();
    }

    /// <summary>On the video's thread: starts the soundtrack, paused when the video is.</summary>
    private void OnOpened(Viewing viewing)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_viewing, viewing) || viewing.Decoder.Soundtrack is not { } soundtrack)
            {
                return;
            }

            var track = new MusicTrack(Path.GetFileName(viewing.Source), () => soundtrack, ReadAhead: VideoDecoder.SoundtrackAhead);
            viewing.Decoder.Clock = () => ReferenceEquals(Current, viewing) ? _audio.VideoProgress() : null;
            viewing.Starting = true;
            viewing.Id = _audio.PlayVideo(track, viewing.Decoder.From);
            viewing.Starting = false;

            if (!viewing.Ended && _pause != VideoPause.None)
            {
                _audio.PauseVideo();
            }
        }

        Raise();
    }

    private void OnFinished(long id)
    {
        lock (_gate)
        {
            // A sink with no output finishes the soundtrack inside PlayVideo, before its id is known.
            if (_viewing is not { } viewing || (viewing.Id != id && !viewing.Starting))
            {
                return;
            }

            viewing.Ended = true;
            _pause = VideoPause.None;
        }

        Raise();
    }

    private void OnActivity(AudioActivity activity)
    {
        if (activity.Channel is AudioChannel.Speech or AudioChannel.Alert)
        {
            if (_pauseWhileSpeaking())
            {
                Pause(VideoPause.Speaking);
            }
        }
        else if (activity.Channel is null)
        {
            ResumeIfQuiet();
        }
    }

    private void ResumeIfQuiet()
    {
        if (PausedFor is not (VideoPause.PushToTalk or VideoPause.Speaking)
            || _audio.Listening
            || _audio.TurnInFlight
            || _audio.IsSpeaking)
        {
            return;
        }

        ResumeWhen(static reason => reason is VideoPause.PushToTalk or VideoPause.Speaking);
    }

    private void ResumeWhen(Func<VideoPause, bool> reason)
    {
        var paused = PausedFor;

        if (paused == VideoPause.None || !reason(paused) || StoryPlaying)
        {
            return;
        }

        Starting?.Invoke();

        lock (_gate)
        {
            if (_viewing is not { Ended: false } || _pause == VideoPause.None || !reason(_pause))
            {
                return;
            }

            if (SpeakingNow())
            {
                _pause = VideoPause.Speaking;
            }
            else
            {
                _pause = VideoPause.None;
                _audio.ResumeVideo();
            }
        }

        Raise();
    }

    /// <summary>Tells the arbiter and the ambient music, off the calling thread, whether a video plays, then raises <see cref="Changed"/>.</summary>
    private void Raise()
    {
        _background(Publish);
        Changed?.Invoke();
    }

    /// <summary>Reads the state when it runs, so calls may land in any order.</summary>
    private void Publish()
    {
        lock (_publishing)
        {
            bool watching;

            lock (_gate)
            {
                watching = _viewing is { Ended: false } viewing
                    && viewing.Decoder.Failure is null
                    && _pause is not (VideoPause.Commander or VideoPause.Interrupted);
            }

            _audio.VideoPlays(watching);
            _music?.Yield(MusicYield.Video, watching);
        }
    }

    private sealed class Viewing(string source, VideoDecoder decoder)
    {
        public string Source { get; } = source;

        public VideoDecoder Decoder { get; } = decoder;

        /// <summary>The soundtrack's playback id, once open.</summary>
        public long? Id { get; set; }

        public bool Ended { get; set; }

        /// <summary>Inside <see cref="AudioArbiter.PlayVideo"/>.</summary>
        public bool Starting { get; set; }
    }
}
