using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Whether the native library loads at all, on whatever machine this is running on.
/// <para>
/// The whole test asserts nothing about a headset and never calls <c>VR_Init</c>. Reaching
/// the last line is the claim: the resolver found a library, the loader mapped it, and the
/// entry points resolved. Both calls used are flat C exports that need no initialisation and
/// contact no compositor, so this is safe to run with SteamVR closed and safe to run while
/// the Commander is in the middle of something.
/// </para>
/// <para>
/// Skipped rather than failed on a machine with no runtime, because that is a legitimate
/// machine and CI is one of them. What it would catch is the case that actually costs a day: a
/// runtime that is present, a binding that loads, and a version disagreement that presents as
/// the wrong function being called with no error.
/// </para>
/// </summary>
public class OpenVrRuntimeReachTests
{
    [Fact]
    public void TheBindingLoadsAndItsEntryPointsResolve()
    {
        Assert.SkipUnless(
            OpenVrLoader.Locate() is not null,
            "No SteamVR runtime on this machine, which is a supported configuration.");

        Assert.True(OpenVrLoader.Register());

        // Two flat exports, no session, no compositor. Their answers are not asserted — a
        // machine with the runtime installed and the headset unplugged is a real machine and
        // both of these answering false is the correct result there.
        _ = Valve.VR.OpenVR.IsRuntimeInstalled();
        _ = Valve.VR.OpenVR.IsHmdPresent();
    }
}
