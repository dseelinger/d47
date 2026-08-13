using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class WindowFitTests
{
    /// <summary>
    /// The case the checklist names: 820x640 device-independent pixels is 1230x960 real ones at
    /// 150%, which is taller than a 1366x768 laptop screen outright. In device-independent
    /// units that same screen offers 911x485 of work area, so the height has to give.
    /// </summary>
    [Fact]
    public void TheDefaultSizeIsClampedOnAScaledLaptopScreen()
    {
        var (width, height) = WindowFit.Clamp(820, 640, 1366 / 1.5, 728 / 1.5);

        Assert.True(height < 640, $"640 should not survive a 485-unit work area; got {height}");
        Assert.True(height <= 728 / 1.5 * WindowFit.Fraction);

        // 820 is within a hair of the width limit too, which is the point: the default was
        // sized for a screen this one only resembles at 100%.
        Assert.True(width <= 1366 / 1.5 * WindowFit.Fraction);
    }

    /// <summary>
    /// 960 real pixels of a 1032-pixel work area is 93% — it fits, and it is still the thing
    /// being complained about. The margin is what makes a window read as a window.
    /// </summary>
    [Fact]
    public void ADefaultThatTechnicallyFitsIsStillTrimmedToLeaveAMargin()
    {
        var (_, height) = WindowFit.Clamp(820, 640, 1920 / 1.5, 1032 / 1.5);

        Assert.True(height < 640);
        Assert.True(height * 1.5 < 1032 * 0.95, "the window should not fill nearly the whole work area");
    }

    [Fact]
    public void ARoomyScreenLeavesTheDefaultAlone()
    {
        var (width, height) = WindowFit.Clamp(820, 640, 2560, 1400);

        Assert.Equal(820, width);
        Assert.Equal(640, height);
    }

    [Fact]
    public void AnUnreportedWorkAreaChangesNothing()
    {
        // Better a window at its default size than a window with no area at all.
        var (width, height) = WindowFit.Clamp(820, 640, 0, 0);

        Assert.Equal(820, width);
        Assert.Equal(640, height);
    }

    [Fact]
    public void ARememberedPositionOnAScreenThatStillExistsIsHonoured()
    {
        var screen = new FitRect(0, 0, 1920, 1032);
        var window = new FitRect(200, 100, 1230, 800);

        Assert.Equal(window, WindowFit.Reposition(window, [screen]));
    }

    /// <summary>
    /// The failure this prevents is silent and looks exactly like the app not starting: a
    /// window restored onto a monitor that has since been unplugged is a window nobody can
    /// reach with the mouse.
    /// </summary>
    [Fact]
    public void ARememberedPositionOnAScreenThatIsGoneFallsBackToCentring()
    {
        var screen = new FitRect(0, 0, 1920, 1032);
        var onAMissingSecondMonitor = new FitRect(2400, 100, 1230, 800);

        Assert.Null(WindowFit.Reposition(onAMissingSecondMonitor, [screen]));
    }

    [Fact]
    public void AWindowHangingMostlyOffTheEdgeStillCountsIfEnoughIsGrabbable()
    {
        var screen = new FitRect(0, 0, 1920, 1032);

        Assert.NotNull(WindowFit.Reposition(new FitRect(1800, 900, 800, 600), [screen]));
        Assert.Null(WindowFit.Reposition(new FitRect(1900, 900, 800, 600), [screen]));
    }

    [Fact]
    public void ASecondMonitorIsAValidHome()
    {
        var screens = new[] { new FitRect(0, 0, 1920, 1032), new FitRect(1920, 0, 2560, 1400) };
        var onTheSecond = new FitRect(2400, 100, 1230, 800);

        Assert.Equal(onTheSecond, WindowFit.Reposition(onTheSecond, screens));
    }
}
