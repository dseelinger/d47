using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

/// <summary>The startup splash is shown for a normal launch and closed once, on whichever ending comes first.</summary>
public class ASplashShowsUntilTheMainWindowOpensTests
{
    private sealed class FakeSurface : ISplashSurface
    {
        public int Shown { get; private set; }

        public int Closed { get; private set; }

        public int Disposed { get; private set; }

        public void Show() => Shown++;

        public void Close() => Closed++;

        public List<double> Fractions { get; } = [];

        public void Progress(double fraction) => Fractions.Add(fraction);

        public void Dispose() => Disposed++;
    }

    [Fact]
    public void ANormalLaunchShowsIt()
    {
        var surface = new FakeSurface();

        using var splash = SplashScreen.Open([], ownsInstance: true, () => surface);

        Assert.NotNull(splash);
        Assert.Equal(1, surface.Shown);
        Assert.Equal(0, surface.Closed);
    }

    [Fact]
    public void TheSelfTestDoesNotShowIt()
    {
        var made = false;

        var splash = SplashScreen.Open([SelfTest.Flag], ownsInstance: true, () =>
        {
            made = true;
            return new FakeSurface();
        });

        Assert.Null(splash);
        Assert.False(made);
    }

#if DEBUG
    [Fact]
    public void TheHullGpuCaptureDoesNotShowIt() =>
        Assert.False(SplashScreen.Wanted([Panel.HullGpuCapture.Flag], ownsInstance: true));
#endif

    [Fact]
    public void ASecondCopyThatOnlySurfacesTheRunningOneDoesNotShowIt() =>
        Assert.False(SplashScreen.Wanted([], ownsInstance: false));

    [Fact]
    public void TheMainWindowOpeningClosesItOnce()
    {
        var surface = new FakeSurface();
        using var splash = SplashScreen.Open([], ownsInstance: true, () => surface);

        SplashScreen.Current!.Close();
        SplashScreen.Current!.Close();

        Assert.Equal(1, surface.Closed);
        Assert.Equal(1, surface.Disposed);
    }

    [Fact]
    public void DisposingItClosesItWhenTheWindowNeverOpened()
    {
        var surface = new FakeSurface();
        var splash = SplashScreen.Open([], ownsInstance: true, () => surface);

        splash!.Dispose();

        Assert.Equal(1, surface.Closed);
        Assert.Null(SplashScreen.Current);
    }

    [Fact]
    public void TheNineStagesInOrderReportOneNinthToNineNinths()
    {
        var surface = new FakeSurface();
        using var splash = SplashScreen.Open([], ownsInstance: true, () => surface);

        foreach (var stage in Enum.GetValues<BootStage>())
        {
            splash!.Reached(stage);
        }

        Assert.Equal(Enumerable.Range(1, 9).Select(n => n / 9.0), surface.Fractions);
    }

    [Fact]
    public void ARepeatedOrEarlierStageReportsNothing()
    {
        var surface = new FakeSurface();
        using var splash = SplashScreen.Open([], ownsInstance: true, () => surface);

        splash!.Reached(BootStage.Services);
        splash.Reached(BootStage.Services);
        splash.Reached(BootStage.Stores);

        Assert.Equal([4 / 9.0], surface.Fractions);
    }

    [Fact]
    public void AStageReachedAfterCloseReportsNothing()
    {
        var surface = new FakeSurface();
        using var splash = SplashScreen.Open([], ownsInstance: true, () => surface);

        splash!.Close();
        splash.Reached(BootStage.Foundations);

        Assert.Empty(surface.Fractions);
    }
}
