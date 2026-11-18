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
}
