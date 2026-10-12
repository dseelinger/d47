using D47.App.Headset;
using D47.App.Panel;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The headset avatar plays its clip on a quad of its own, at true speed on the 10 Hz tick, and steps aside
/// for anything drawn over it (#1130).
/// </summary>
public class TheHeadsetAvatarPlaysOnItsOwnQuadTests
{
    private const double Fps = 24;

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(145)]
    public void TheFrameForATimeIsTheFrameTheDesktopStepsTo(int count)
    {
        var (index, direction) = (0, 1);

        for (var step = 0; step < count * 5; step++)
        {
            var elapsed = TimeSpan.FromSeconds((step + 0.5) / Fps);

            Assert.Equal(index, VrAvatarSurface.FrameAt(elapsed, Fps, count, decoded: true));

            (index, direction) = AvatarView.Step(index, direction, count);
        }
    }

    [Fact]
    public void TwoServesInsideOneClipFrameShowTheSameFrame()
    {
        var first = VrAvatarSurface.FrameAt(TimeSpan.FromMilliseconds(1005), Fps, 145, decoded: true);
        var second = VrAvatarSurface.FrameAt(TimeSpan.FromMilliseconds(1035), Fps, 145, decoded: true);

        Assert.Equal(first, second);
    }

    [Fact]
    public void WhileDecodingItPlaysForwardAndWaitsAtTheNewestFrame()
    {
        Assert.Equal(3, VrAvatarSurface.FrameAt(TimeSpan.FromSeconds(3.5 / Fps), Fps, 10, decoded: false));
        Assert.Equal(9, VrAvatarSurface.FrameAt(TimeSpan.FromSeconds(2), Fps, 10, decoded: false));
    }

    [Fact]
    public void AClipOfOneFrameOrNoRateStillHasAFrame()
    {
        Assert.Equal(0, VrAvatarSurface.FrameAt(TimeSpan.FromSeconds(9), Fps, 1, decoded: true));
        Assert.Equal(0, VrAvatarSurface.FrameAt(TimeSpan.FromSeconds(-1), Fps, 145, decoded: true));
        Assert.Equal(3, VrAvatarSurface.FrameAt(TimeSpan.FromSeconds(3.5), 0, 145, decoded: true));
    }

    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    public void TheQuadIsUpOnlyOverAnUncoveredClip(bool panelVisible, bool hasFrames, bool drawnOnPanel, bool covered, bool shows) =>
        Assert.Equal(shows, VrAvatarSurface.Shows(panelVisible, hasFrames, drawnOnPanel, covered));

    [Fact]
    public void TheClipGoesUpAsATexture() =>
        Assert.True(((IVrSurfaceSource)new VrAvatarSurface(null!)).UploadsAsTexture);

    [Fact]
    [Trait("Category", "Gate")]
    public void TheHostServesTheAvatarOnTheVrTickAfterThePanel()
    {
        var host = AppSource.Files.Single(file => file.Name == "VrHost.cs").Text.ReplaceLineEndings("\n");
        var surface = AppSource.Files.Single(file => file.Name == "VrAvatarSurface.cs").Text;

        Assert.Contains("[panel, avatar, captions]", host, StringComparison.Ordinal);
        Assert.Contains("_avatar.Advance(now);", host, StringComparison.Ordinal);
        Assert.Contains("tick.Add(\"vr\", host.OnTick);", host, StringComparison.Ordinal);
        Assert.DoesNotContain("Timer", surface, StringComparison.Ordinal);
    }
}
