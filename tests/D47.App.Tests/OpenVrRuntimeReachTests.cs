using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>Whether the native library loads at all, on whatever machine this is running on.</summary>
public class OpenVrRuntimeReachTests
{
    [Fact]
    public void TheBindingLoadsAndItsEntryPointsResolve()
    {
        Assert.SkipUnless(
            OpenVrLoader.Locate() is not null,
            "No SteamVR runtime on this machine, which is a supported configuration.");

        Assert.True(OpenVrLoader.Register());

        // Two flat exports, no session, no compositor.
        _ = Valve.VR.OpenVR.IsRuntimeInstalled();
        _ = Valve.VR.OpenVR.IsHmdPresent();
    }
}
