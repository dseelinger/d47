using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Through SteamVR, the video has an overlay of its own. The presenter sets its texture from a device of its own and
/// retries a refused one as <see cref="FrameDelivery"/> says; the serve only places it and shows or hides it.
/// </summary>
public class TheSteamVrVideoGoesUpFromItsOwnPresenterTests
{
    private const string VideoKey = "com.dseelinger.D47.video";

    private static readonly DateTimeOffset Start = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheVideoHasAnOverlayOfItsOwnThatStaysHiddenUntilATextureIsTaken()
    {
        using var rig = new Rig();
        rig.Video.Show();

        Assert.True(rig.Runtime.Serve(Start));

        Assert.Contains(VideoKey, rig.OpenVr.Live.Values);
        Assert.False(rig.OpenVr.Shown.GetValueOrDefault(rig.Handle));
        Assert.Empty(rig.OpenVr.TexturesSubmitted);
        Assert.Empty(rig.VideoDevice.Writes);
    }

    [Fact]
    public void APictureIsWrittenAndSetFromTheVideosOwnDeviceThenShownByTheServe()
    {
        using var rig = new Rig();
        rig.Video.Show();
        rig.Serve();

        rig.Clock.Step();

        var texture = Assert.Single(rig.OpenVr.TexturesSubmitted);
        Assert.Equal([texture], rig.VideoDevice.Writes);
        Assert.Contains(texture, rig.VideoDevice.Created);
        Assert.Empty(rig.Device.Created);
        Assert.Equal([rig.OpenVr.Adapter], rig.VideoDevice.Opens);

        rig.Serve();
        Assert.True(rig.OpenVr.Shown[rig.Handle]);
    }

    [Fact]
    public void WhileTheServeDoesNotWantTheVideoThePresenterOpensNothingAndSetsNothing()
    {
        using var rig = new Rig();
        rig.Video.Visible = false;
        rig.Video.Show();
        rig.Serve();

        rig.Clock.Step();

        Assert.Empty(rig.VideoDevice.Opens);
        Assert.Empty(rig.OpenVr.TexturesSubmitted);
    }

    [Fact]
    public void EachNewPictureGoesIntoTheOtherTextureAndTheSameOneIsNotSentAgain()
    {
        using var rig = new Rig();
        rig.Video.Show();
        rig.Serve();
        rig.Clock.Step();
        rig.Clock.Step();
        rig.Video.Show();
        rig.Clock.Step();

        Assert.Equal(2, rig.OpenVr.TexturesSubmitted.Count);
        Assert.NotEqual(rig.OpenVr.TexturesSubmitted[0], rig.OpenVr.TexturesSubmitted[1]);
    }

    [Fact]
    public void ARefusedPictureGoesAgainOnlyWhileTheOverlayIsDrawn()
    {
        using var rig = new Rig();
        rig.OpenVr.TextureRefused = true;
        rig.Video.Show();
        rig.Serve();
        rig.Clock.Step();

        // Not drawn: held, not retried.
        rig.Clock.Step();
        Assert.Single(rig.OpenVr.TexturesSubmitted);

        rig.OpenVr.Shown[rig.Handle] = true;
        rig.OpenVr.TextureRefused = false;
        rig.Clock.Step();

        Assert.Equal(2, rig.OpenVr.TexturesSubmitted.Count);
        Assert.Equal(rig.OpenVr.TexturesSubmitted[0], rig.OpenVr.TexturesSubmitted[1]);
        Assert.Single(rig.VideoDevice.Writes);
    }

    [Fact]
    public void AHiddenVideoTakesItsOverlayDown()
    {
        using var rig = new Rig();
        rig.Video.Show();
        rig.Serve();
        rig.Clock.Step();
        rig.Serve();

        rig.Video.Visible = false;
        rig.Serve();

        Assert.False(rig.OpenVr.Shown[rig.Handle]);
    }

    [Fact]
    public void StopEndsThePresenterBeforeTheSessionAndGivesBackEverythingItTook()
    {
        using var rig = new Rig();
        rig.Video.Show();
        rig.Serve();
        rig.Clock.Step();

        rig.Runtime.Stop();

        Assert.Equal(1, rig.Clock.Finishes);
        Assert.Empty(rig.OpenVr.Live);
        Assert.Empty(rig.VideoDevice.Live);
        Assert.False(rig.VideoDevice.IsOpen);

        var calls = rig.OpenVr.Calls.Select(call => call.Function).ToList();
        Assert.True(calls.IndexOf(nameof(FakeOpenVr.DestroyOverlay)) < calls.IndexOf(nameof(FakeOpenVr.Shutdown)));
    }

    private sealed class Rig : IDisposable
    {
        public Rig()
        {
            Runtime = new SteamVrRuntime(
                [new FakeSurface()],
                NullLogger<SteamVrRuntime>.Instance,
                OpenVr,
                Device,
                video: Video,
                videoTextures: VideoDevice,
                videoClock: Clock);

            Assert.Equal(VrStartOutcome.Started, Runtime.Start().Outcome);
            Assert.Equal(1, Clock.Runs);
            Handle = OpenVr.Live.Single(live => live.Value == VideoKey).Key;
        }

        public FakeOpenVr OpenVr { get; } = new() { Adapter = 2 };

        public FakeTextureDevice Device { get; } = new();

        public FakeTextureDevice VideoDevice { get; } = new();

        public FakeVideo Video { get; } = new();

        public ManualVideoClock Clock { get; } = new();

        public SteamVrRuntime Runtime { get; }

        public ulong Handle { get; }

        private int _ticks;

        public void Serve() => Assert.True(Runtime.Serve(Start + TimeSpan.FromMilliseconds(100 * _ticks++)));

        public void Dispose() => Runtime.Stop();
    }
}
