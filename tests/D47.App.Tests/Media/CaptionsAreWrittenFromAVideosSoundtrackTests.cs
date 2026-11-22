using D47.App.Media;
using D47.Core.Audio;
using D47.Core.Listening;
using D47.Core.Viewing;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>
/// A second, sound-only reader gives the captioner a video's soundtrack as 16 kHz mono, and the writer holds the
/// video until the captions are ahead of it (#2677).
/// </summary>
[Trait("Category", "Integration")]
public sealed class CaptionsAreWrittenFromAVideosSoundtrackTests : IDisposable
{
    /// <summary>Eight seconds with a stereo AAC soundtrack.</summary>
    private static readonly string Clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "clip.mp4");

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly Lazy<bool> Decodes = new(() =>
    {
        using var frames = VideoFrames.Open(Clip);
        return frames is not null;
    });

    private readonly PulledSink _sink = new(1);
    private readonly AudioArbiter _audio;
    private readonly List<IDisposable> _owned = [];
    private VideoPlayback? _video;

    public CaptionsAreWrittenFromAVideosSoundtrackTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }

        _video?.Stop();
        _sink.Dispose();
    }

    private static void SkipWhereItCannotDecode()
    {
        if (!Decodes.Value)
        {
            Assert.Skip("Media Foundation cannot decode the fixture here.");
        }
    }

    private SoundtrackReader Reader()
    {
        SkipWhereItCannotDecode();
        var reader = SoundtrackReader.Open(Clip, out var problem);
        Assert.True(reader is not null, problem);
        _owned.Add(reader);
        return reader;
    }

    private VideoPlayback PlayingVideo()
    {
        SkipWhereItCannotDecode();
        _video = new VideoPlayback(_audio, background: work => work());
        _video.Play(Clip);
        Until(() => _video.State is not VideoState.Opening, "it opened");
        return _video;
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

    [Fact]
    public void TheWholeSoundtrackIsReadAsSixteenKilohertzMono()
    {
        var stretch = Reader().Read(TimeSpan.Zero, CaptionRun.Stretch)!;

        Assert.True(stretch.Ended);
        Assert.InRange(stretch.From.TotalSeconds, 0, 0.1);
        Assert.InRange(stretch.Length.TotalSeconds, 7.5, 8.5);
        Assert.True(stretch.Samples.Max(Math.Abs) > 0.01f, "The soundtrack read as silence.");
    }

    [Fact]
    public void AStretchFromTheMiddleStartsThereAndTheNextReadCarriesOnFromInsideIt()
    {
        var reader = Reader();

        var middle = reader.Read(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2))!;

        Assert.False(middle.Ended);
        Assert.InRange(middle.From.TotalSeconds, 2.999, 3.1);
        Assert.InRange(middle.Length.TotalSeconds, 1.99, 2.01);

        var rest = reader.Read(TimeSpan.FromSeconds(4.5), CaptionRun.Stretch)!;

        Assert.True(rest.Ended);
        Assert.Equal(TimeSpan.FromSeconds(4.5), rest.From);
        Assert.InRange(rest.Length.TotalSeconds, 3, 4);
    }

    [Fact]
    public void AHeldVideoStandsStillAsStalledAndPlaysOnWhenLetGo()
    {
        var video = PlayingVideo();
        Until(() => video.Position > TimeSpan.FromMilliseconds(300), "it plays");

        video.Hold(true);
        var held = video.Position;
        Thread.Sleep(200);

        Assert.Equal(VideoState.Stalled, video.State);
        Assert.True(video.Held);
        Assert.Equal(held, video.Position);

        video.Hold(false);

        Assert.Equal(VideoState.Playing, video.State);
        Until(() => video.Position > held + TimeSpan.FromMilliseconds(100), "it plays on");
    }

    [Fact]
    public void AVideoHeldAndPausedStaysPausedWhenLetGo()
    {
        var video = PlayingVideo();

        video.Hold(true);
        video.Pause(VideoPause.Commander);
        video.Hold(false);

        Assert.Equal(VideoState.Paused, video.State);
    }

    [Fact]
    public void TheWriterHoldsTheVideoUntilTheCaptionsAreWrittenAndThenLetsItGo()
    {
        var video = PlayingVideo();
        var captioner = new OneCuePerStretch();
        var writer = new CaptionWriter(
            Clip,
            TimeSpan.Zero,
            captioner,
            () => video.State is VideoState.Ended ? null : video.Position,
            video.Hold,
            NullLogger.Instance);
        _owned.Add(writer);

        writer.Start();

        Until(() => captioner.Stretches > 0 || video.Held, "the writer started");
        Until(() => writer.Finished, "the captions reach the end");
        Until(() => !video.Held, "the video is let go");

        Assert.Null(writer.Problem);
        Assert.Equal(TimeSpan.FromSeconds(1), writer.Cues[0].Start);
        Assert.InRange(writer.WrittenTo.TotalSeconds, 7.5, 8.5);

        writer.Dispose();
        Until(() => captioner.Released, "the captioner is released once writing stops");
    }

    private sealed class OneCuePerStretch : ISoundtrackCaptioner
    {
        private int _stretches;

        public int Stretches => Volatile.Read(ref _stretches);

        public bool Released { get; private set; }

        public string? Unavailable => null;

        public async Task<IReadOnlyList<SubtitleCue>> CaptionAsync(Utterance sound, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _stretches);
            Assert.Equal(ISoundtrackCaptioner.SampleRate, sound.SampleRate);
            await Task.Delay(300, cancellationToken);
            return [new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Words in the soundtrack.")];
        }

        public void Release() => Released = true;
    }
}
