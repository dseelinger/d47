using Avalonia;
using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

public class AStripOffEveryScreenIsCentredOnEliteTests
{
    private static readonly OverlayScreen Left = Screen(0, 0, 1920, 1080, 1.0);
    private static readonly OverlayScreen Right = Screen(1920, 0, 1920, 1080, 1.0);

    private static OverlayScreen Screen(int x, int y, int width, int height, double scaling) =>
        new(new PixelRect(x, y, width, height), new PixelRect(x, y, width, height - 40), scaling);

    [Fact]
    public void AStripWhollyOnAScreenIsLeftAlone()
    {
        var strip = new PixelRect(100, 100, 400, 300);

        Assert.True(OverlayGeometry.IsWhollyOnAScreen(strip, [Left, Right]));
    }

    [Fact]
    public void AStripOnTheSecondMonitorIsLeftAlone()
    {
        var strip = new PixelRect(2000, 100, 400, 300);

        Assert.True(OverlayGeometry.IsWhollyOnAScreen(strip, [Left, Right]));
    }

    [Fact]
    public void AStripWithItsCornerOnAScreenAndItsBodyOffIsLost()
    {
        var strip = new PixelRect(1800, 100, 400, 300);

        Assert.False(OverlayGeometry.IsWhollyOnAScreen(strip, [Left]));
    }

    [Fact]
    public void AStripWhollyOffEveryScreenIsLost()
    {
        var strip = new PixelRect(5000, 5000, 400, 300);

        Assert.False(OverlayGeometry.IsWhollyOnAScreen(strip, [Left, Right]));
    }

    [Fact]
    public void AStripAcrossTwoMonitorsIsLost()
    {
        var strip = new PixelRect(1800, 100, 400, 300);

        Assert.False(OverlayGeometry.IsWhollyOnAScreen(strip, [Left, Right]));
    }

    [Fact]
    public void ACentredStripHasItsMiddleAtTheMiddleOfTheWorkingArea()
    {
        var (position, width, height) = OverlayGeometry.Centre(400, 300, Right);

        Assert.Equal(new PixelPoint(1920 + 760, 370), position);
        Assert.Equal(400, width);
        Assert.Equal(300, height);
    }

    [Fact]
    public void ACentredStripIsCentredAtOneAndAHalfTimesScaling()
    {
        var screen = Screen(0, 0, 2880, 1620, 1.5);

        var (position, width, height) = OverlayGeometry.Centre(400, 300, screen);

        Assert.Equal(new PixelPoint((2880 - 600) / 2, (1580 - 450) / 2), position);
        Assert.Equal(400, width);
        Assert.Equal(300, height);
    }

    [Fact]
    public void AStripLargerThanTheWorkingAreaIsReducedToFitIt()
    {
        var (position, width, height) = OverlayGeometry.Centre(3840, 1040, Left);

        Assert.Equal(1920, width, 3);
        Assert.Equal(520, height, 3);
        Assert.Equal(new PixelPoint(0, 260), position);
    }
}
