using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// How many pixels the big headset panel is rendered at.
/// </summary>
public class PanelResolutionTests
{
    /// <summary>
    /// The quad takes a width in metres and derives its height from the texture's aspect, so a rung
    /// with a different aspect would silently change the shape of the panel a Commander was only asking
    /// to make sharper.
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
    /// The settings file is append-only, so a file written before this property existed has to mean
    /// something rather than fail.
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

    /// <summary>Mini is not on this ladder and is not meant to be: those numbers are a floor under a reduced content set rather than an aspect.</summary>
    [Fact]
    public void MiniIsNotARungOfThisLadder()
    {
        Assert.DoesNotContain(PanelResolution.Mini, PanelResolution.Steps);
    }
}
