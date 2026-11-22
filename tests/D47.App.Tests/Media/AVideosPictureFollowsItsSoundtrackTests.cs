using D47.App.Media;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>
/// One reader feeds a video's picture and its soundtrack; the picture follows the soundtrack's position through seeks
/// and pauses, and a file whose sound cannot be decoded says so (#2668).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AVideosPictureFollowsItsSoundtrackTests : IDisposable
{
    /// <summary>Eight seconds at 25 frames a second, a keyframe every second, with a stereo AAC soundtrack.</summary>
    private static readonly string Clip = Fixture("clip.mp4");

    private static readonly TimeSpan FrameLength = TimeSpan.FromMilliseconds(40);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>Whether Media Foundation decodes the fixture's H.264 here; a Server SKU or an N edition may not.</summary>
    private static readonly Lazy<bool> Decodes = new(() =>
    {
        using var frames = VideoFrames.Open(Clip);
        return frames is not null;
    });

    private readonly PulledSink _sink;
    private readonly AudioArbiter _audio;
    private readonly List<VideoPlayback> _videos = [];

    public AVideosPictureFollowsItsSoundtrackTests()
        : this(speed: 1)
    {
    }

    private AVideosPictureFollowsItsSoundtrackTests(int speed)
    {
        _sink = new PulledSink(speed);
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    public void Dispose()
    {
        foreach (var video in _videos)
        {
            video.Stop();
        }

        _sink.Dispose();
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", name);

    /// <summary>A playback stopped when the test ends; skips the test where Media Foundation cannot decode the fixture.</summary>
    private VideoPlayback Video(Func<bool>? pauseWhileSpeaking = null, Func<bool>? pauseForPushToTalk = null)
    {
        if (!Decodes.Value)
        {
            Assert.Skip("Media Foundation cannot decode the fixture here.");
        }

        var video = new VideoPlayback(_audio, pauseWhileSpeaking, background: work => work(), pauseForPushToTalk: pauseForPushToTalk);
        _videos.Add(video);
        return video;
    }

    private static void Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting until {what}.");
            Thread.Sleep(10);
        }
    }

    private static void Opened(VideoPlayback video)
    {
        Until(() => video.State is not VideoState.Opening, "it opened");
        Assert.NotEqual(VideoState.Failed, video.State);
    }

    [Fact]
    public void ASeekToTheMiddleShowsAFrameWithinASecondOfIt()
    {
        var video = Video();
        video.Play(Clip);
        Opened(video);

        var middle = TimeSpan.FromSeconds(4);
        Assert.True(video.Seek(middle));

        Until(() => video.Frame is { } frame && (frame.Time - middle).Duration() < TimeSpan.FromSeconds(1), "a frame near the middle shows");
        Assert.Equal(320, video.Frame!.Width);
        Assert.Equal(180, video.Frame.Height);
    }

    [Fact]
    public void AfterASeekAndAPauseThePictureIsTheFrameAtTheSoundtracksPosition()
    {
        var video = Video();
        video.Play(Clip);
        Opened(video);

        video.Seek(TimeSpan.FromSeconds(5.3));
        Until(() => video.Frame is { } frame && frame.Time >= TimeSpan.FromSeconds(5), "the seek lands");
        AssertInStep(video);

        Thread.Sleep(300);
        video.Pause(VideoPause.Commander);
        Assert.Equal(VideoState.Paused, video.State);
        AssertInStep(video);
    }

    /// <summary>Waits for the decoder's next present, then checks the frame shown is the one the position falls in.</summary>
    private static void AssertInStep(VideoPlayback video)
    {
        Until(
            () => video.Frame is { } frame && video.Position >= frame.Time && video.Position < frame.Time + FrameLength + TimeSpan.FromMilliseconds(20),
            $"the frame shown ({video.Frame?.Time}) is the one at the soundtrack's position ({video.Position})");
    }

    [Fact]
    public void PlayingFromAPlaceStartsThere()
    {
        var video = Video();
        var place = TimeSpan.FromSeconds(5);
        video.Play(Clip, place);
        Opened(video);

        Until(() => video.Frame is { } frame && (frame.Time - place).Duration() < TimeSpan.FromSeconds(1), "a frame near the place shows");
        Until(() => video.Position > place, "it plays on from the place");
        Assert.True(video.Position < place + TimeSpan.FromSeconds(2), $"It was at {video.Position}.");
    }

    [Fact]
    public void ResumingAfterAPausePlaysOnFromWhereItWas()
    {
        var video = Video();
        video.Play(Clip);
        Opened(video);
        Until(() => video.Position > TimeSpan.FromMilliseconds(300), "it plays");

        video.Pause(VideoPause.Commander);
        var held = video.Position;
        Thread.Sleep(200);
        Assert.Equal(held, video.Position);

        video.Resume();
        Assert.Equal(VideoState.Playing, video.State);
        Until(() => video.Position > held + TimeSpan.FromMilliseconds(100), "it plays on");
    }

    [Fact]
    public void AVideoPlaysToItsEnd()
    {
        using var test = new AVideosPictureFollowsItsSoundtrackTests(speed: 8);
        var video = test.Video();
        video.Play(Clip);
        Opened(video);

        Until(() => video.State == VideoState.Ended, "it ends");
        Assert.True(video.Frame!.Time > TimeSpan.FromSeconds(7.5), $"The last frame shown was at {video.Frame.Time}.");
    }

    [Fact]
    public void AVideoWithNoSoundtrackPlaysToItsEndOnSilence()
    {
        using var test = new AVideosPictureFollowsItsSoundtrackTests(speed: 4);
        var video = test.Video();
        video.Play(Fixture("no-sound.mp4"));
        Opened(video);

        Until(() => video.State == VideoState.Ended, "it ends");
        Assert.True(video.Frame!.Time > TimeSpan.FromSeconds(1.5), $"The last frame shown was at {video.Frame.Time}.");
    }

    [Fact]
    public void ASoundtrackWindowsCannotDecodeFailsNamingTheSoundtrack()
    {
        var video = Video();
        video.Play(Fixture("undecodable-sound.mkv"));

        Until(() => video.State is not VideoState.Opening, "it opened or failed");

        if (video.State != VideoState.Failed)
        {
            Assert.Skip("This machine has a WavPack decoder, so the fixture's soundtrack decodes.");
        }

        Assert.Equal(VideoFailure.Soundtrack, video.Failure);
        Assert.Empty(_sink.Started);
    }

    [Fact]
    public void AFileThatIsNotThereFailsAsUnreachable()
    {
        var video = Video();
        video.Play(Fixture("missing.mp4"));

        Until(() => video.State is not VideoState.Opening, "it failed");

        Assert.Equal(VideoState.Failed, video.State);
        Assert.Equal(VideoFailure.Unreachable, video.Failure);
    }

    [Fact]
    public void D47SpeakingPausesTheVideoUntilItIsQuiet()
    {
        var video = Video();
        video.Play(Clip);
        Opened(video);
        var soundtrack = _sink.Started.Single().Id;

        _audio.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = new AudioClip("reply", new byte[960], AudioFormat.Standard) });

        Assert.Equal(VideoPause.Speaking, video.PausedFor);
        Assert.True(_sink.IsPaused(soundtrack));

        _sink.Finish(_sink.Started.Last().Id);

        Assert.Equal(VideoPause.None, video.PausedFor);
        Assert.False(_sink.IsPaused(soundtrack));
    }

    [Fact]
    public void ChatterNeitherPausesTheVideoNorPlaysOverIt()
    {
        var video = Video();
        video.Play(Clip);
        Opened(video);

        _audio.Enqueue(new AudioRequest { Channel = AudioChannel.Overheard, Clip = new AudioClip("chatter", new byte[960], AudioFormat.Standard) });

        Assert.Equal(VideoPause.None, video.PausedFor);
        Assert.Single(_sink.Started);
    }

    [Fact]
    public void WithPauseWhileSpeakingOffSpeechDucksTheVideoInstead()
    {
        var video = Video(pauseWhileSpeaking: () => false);
        video.Play(Clip);
        Opened(video);
        var soundtrack = _sink.Started.Single().Id;

        _audio.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = new AudioClip("reply", new byte[960], AudioFormat.Standard) });

        Assert.Equal(VideoPause.None, video.PausedFor);
        Assert.False(_sink.IsPaused(soundtrack));
        Assert.Equal(AudioMix.Default.Video.GainWhile(true), _sink.Gains[soundtrack], 3);
    }

    [Fact]
    public void PushToTalkPausesTheVideoUntilTheReplyEnds()
    {
        var video = Video();
        video.Play(Clip);
        Opened(video);

        video.Pause(VideoPause.PushToTalk);
        _audio.Listening = true;
        video.Settled();
        Assert.Equal(VideoPause.PushToTalk, video.PausedFor);

        _audio.Listening = false;
        _audio.TurnInFlight = true;
        video.Settled();
        Assert.Equal(VideoPause.PushToTalk, video.PausedFor);

        _audio.TurnInFlight = false;
        video.Settled();
        Assert.Equal(VideoPause.None, video.PausedFor);
    }

    [Fact]
    public void PushToTalkDoesNotPauseTheVideoWhileThatSettingIsOff()
    {
        var on = false;
        var video = Video(pauseForPushToTalk: () => on);
        video.Play(Clip);
        Opened(video);

        video.Pause(VideoPause.PushToTalk);
        Assert.Equal(VideoPause.None, video.PausedFor);

        on = true;
        video.Pause(VideoPause.PushToTalk);
        Assert.Equal(VideoPause.PushToTalk, video.PausedFor);
    }

    [Fact]
    public void SayingStopPausesTheVideoUntilTheCommanderResumesIt()
    {
        var video = Video();
        video.Play(Clip);
        Opened(video);

        _audio.Silence();
        video.Settled();

        Assert.Equal(VideoPause.Interrupted, video.PausedFor);
    }
}
