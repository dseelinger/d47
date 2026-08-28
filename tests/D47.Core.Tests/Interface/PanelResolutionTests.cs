using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// How many pixels the big headset panel is rendered at (Phase 25, "The panel resizes and
/// zooms").
/// <para>
/// The claim worth a test is the one that keeps the three levers independent: every rung is the
/// same shape, so asking for more pixels never changes the proportions of the thing in the room.
/// </para>
/// </summary>
public class PanelResolutionTests
{
    /// <summary>
    /// The quad takes a width in metres and derives its height from the texture's aspect, so a
    /// rung with a different aspect would silently change the shape of the panel a Commander was
    /// only asking to make sharper.
    /// </summary>
    [Fact]
    public void EveryRungIsTheSameShape()
    {
        var wanted = (double)PanelResolution.Default.Width / PanelResolution.Default.Height;

        Assert.All(
            PanelResolution.Steps,
            step => Assert.Equal(wanted, (double)step.Width / step.Height, 6));
    }

    [Fact]
    public void TheDefaultIsOnTheLadder()
    {
        Assert.Contains(PanelResolution.Default, PanelResolution.Steps);
    }

    /// <summary>
    /// Settings are a hand-editable file, so an arbitrary size is something this has to answer
    /// for. Snapping beats rejecting: a rejected size presents as the setting silently not taking.
    /// </summary>
    [Theory]
    [InlineData(1030, 645, 1024, 640)]
    [InlineData(1300, 810, 1280, 800)]
    [InlineData(99999, 99999, 2560, 1600)]
    [InlineData(0, 0, 800, 500)]
    [InlineData(-40, -9, 800, 500)]
    public void AnythingElseSnapsToARung(int width, int height, int wantedWidth, int wantedHeight)
    {
        Assert.Equal((wantedWidth, wantedHeight), PanelResolution.Snap(width, height));
    }

    [Theory]
    [InlineData("1280x800", 1280, 800)]
    [InlineData("2560X1600", 2560, 1600)]
    [InlineData("nonsense", 1024, 640)]
    [InlineData("1280", 1024, 640)]
    [InlineData("x800", 1024, 640)]
    [InlineData(null, 1024, 640)]
    public void AFileSValueIsReadOrFallsBackToTheDefault(string? value, int width, int height)
    {
        Assert.Equal((width, height), PanelResolution.Parse(value));
    }

    /// <summary>
    /// The settings file is append-only, so a file written before this property existed has to
    /// mean something rather than fail. Empty means the default, which is what the panel has
    /// always been.
    /// </summary>
    [Fact]
    public void AFileWrittenBeforeThisExistedGetsTheDefault()
    {
        Assert.Equal(PanelResolution.Default, new VrSurfaceSettings().Resolution);
    }

    [Fact]
    public void ASetValueSurvivesTheRoundTrip()
    {
        var settings = new VrSurfaceSettings { Pixels = PanelResolution.Describe((1600, 1000)) };

        Assert.Equal((1600, 1000), settings.Resolution);
    }

    /// <summary>
    /// Mini is not on this ladder and is not meant to be: those numbers are a floor under a
    /// reduced content set rather than an aspect, which is why holding the aspect there left the
    /// transcript pane with nothing.
    /// </summary>
    [Fact]
    public void MiniIsNotARungOfThisLadder()
    {
        Assert.DoesNotContain(PanelResolution.Mini, PanelResolution.Steps);
    }
}
