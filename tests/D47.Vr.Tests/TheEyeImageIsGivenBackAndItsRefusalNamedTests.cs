using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Valve.VR;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The input trace's headset still (#601): the compositor's view is kept for the session and goes back
/// to SteamVR before the session ends, and a still that could not be taken says why.
/// </summary>
public class TheEyeImageIsGivenBackAndItsRefusalNamedTests
{
    [Fact]
    public void TheViewIsKeptForTheSessionAndGivenBackAtStop()
    {
        var (openVr, runtime) = Attached();
        var copied = new List<IntPtr>();

        Assert.Null(runtime.MirrorLeftEye(0x10, copied.Add));
        Assert.Null(runtime.MirrorLeftEye(0x10, copied.Add));

        Assert.Equal([openVr.MirrorView, openVr.MirrorView], copied);
        Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.GetMirrorTextureD3D11), (ulong)EVREye.Eye_Left));
        Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.ReleaseMirrorTextureD3D11)));

        runtime.Stop();

        var released = openVr.Calls.FindIndex(call => call.Function == nameof(FakeOpenVr.ReleaseMirrorTextureD3D11));
        var shutdown = openVr.Calls.FindIndex(call => call.Function == nameof(FakeOpenVr.Shutdown));

        Assert.InRange(released, 0, shutdown - 1);
    }

    [Fact]
    public void AnotherDeviceGetsAViewOfItsOwn()
    {
        var (openVr, runtime) = Attached();

        try
        {
            Assert.Null(runtime.MirrorLeftEye(0x10, _ => { }));
            Assert.Null(runtime.MirrorLeftEye(0x20, _ => { }));

            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.GetMirrorTextureD3D11)));
            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.ReleaseMirrorTextureD3D11)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void TheViewIsReleasedWhenTheCopyThrows()
    {
        var (openVr, runtime) = Attached();

        try
        {
            Assert.Throws<InvalidOperationException>(
                () => runtime.MirrorLeftEye(0x10, _ => throw new InvalidOperationException("copy failed")));

            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.ReleaseMirrorTextureD3D11), (ulong)openVr.MirrorView));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ARefusalIsNamedAndNothingIsCopied()
    {
        var (openVr, runtime) = Attached();
        openVr.MirrorRefused = EVRCompositorError.IsNotSceneApplication;

        try
        {
            var copied = false;

            Assert.Equal(
                "SteamVR refused the eye image: IsNotSceneApplication",
                runtime.MirrorLeftEye(0x10, _ => copied = true));
            Assert.False(copied);
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.ReleaseMirrorTextureD3D11)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AfterTheSessionEndsNothingReachesOpenVr()
    {
        var (openVr, runtime) = Attached();
        runtime.Stop();

        var shutdown = openVr.Calls.Count;

        Assert.Null(runtime.HeadsetAdapter());
        Assert.Equal("the SteamVR session has ended", runtime.MirrorLeftEye(0x10, _ => { }));
        Assert.Equal(shutdown, openVr.Calls.Count);
    }

    [Fact]
    public void TheAdapterIsTheOneSteamVrNames()
    {
        var (openVr, runtime) = Attached();
        openVr.Adapter = 2;

        try
        {
            Assert.Equal(2, runtime.HeadsetAdapter());
        }
        finally
        {
            runtime.Stop();
        }
    }

    private static (FakeOpenVr OpenVr, SteamVrRuntime Runtime) Attached()
    {
        var openVr = new FakeOpenVr();
        var runtime = new SteamVrRuntime([new FakeSurface()], NullLogger<SteamVrRuntime>.Instance, openVr);

        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
        return (openVr, runtime);
    }
}
