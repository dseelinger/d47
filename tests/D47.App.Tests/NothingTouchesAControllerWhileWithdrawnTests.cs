using D47.App.Headset;
using D47.Core.Capabilities.Builtin;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The motion controllers are withdrawn, and this is what makes that testable rather than hopeful
/// (<a href="https://github.com/dseelinger/d47/issues/198">#198</a>).
/// <para>
/// <b>The claim is a negative, which is why none of it is behavioural.</b> What has to be true is
/// that with the row off, d47 passes no controller device index to any OpenVR function — and no
/// amount of running d47 on a machine with no headset demonstrates that, because nothing runs
/// there either way. So the assertions are about reachability: every road to a per-device call
/// goes through one method, that method reads the row first, and the roads that used to bypass it
/// are gone.
/// </para>
/// <para>
/// The one call that stays is <c>GetDeviceToAbsoluteTrackingPose</c>, because the head pose comes
/// out of it and captions, resting placement and re-anchor all need the head. What changes is its
/// shape: with the row off it is reached only through <c>ReadHead</c>, which asks for an array one
/// slot long and indexes the headset alone, at the serve's ten hertz rather than the aim loop's
/// ninety.
/// </para>
/// </summary>
public class NothingTouchesAControllerWhileWithdrawnTests
{
    /// <summary>
    /// Out of the box d47 does not touch them at all. The default is the withdrawal — turning
    /// them back on is the deliberate act, which is what makes a session with them off the
    /// shipped behaviour rather than an option nobody finds.
    /// </summary>
    [Fact]
    public void TheyAreOffOutOfTheBox()
    {
        Assert.False(new D47.Core.Configuration.D47Settings().Vr.Controllers);
    }

    /// <summary>
    /// <b>One choke point for every per-device call.</b> <c>GetTrackedDeviceClass</c> is asked of
    /// all sixty-four slots every frame, and per controller it is followed by the pose read, by
    /// <c>Note</c> and by <c>GripToTip</c>, which reads render-model string properties off the
    /// device itself. A second method reaching any of those would be a second road the row does
    /// not gate, and it would be invisible from outside.
    /// </summary>
    [Theory]
    [InlineData("CVRSystem", "GetTrackedDeviceClass")]
    [InlineData(nameof(SteamVrRuntime), "Note")]
    [InlineData(nameof(SteamVrRuntime), "GripToTip")]
    public void EveryPerDeviceCallIsMadeFromHandsAndHeadAndNowhereElse(string type, string call)
    {
        var callers = AssemblyCalls.Callers(typeof(SteamVrRuntime).Assembly, type, call);

        Assert.Equal(["SteamVrRuntime.HandsAndHead"], callers);
    }

    /// <summary>
    /// And that one method asks the row before it does any of it. The gate is a read of
    /// <c>Pointing</c> in the body itself rather than a promise made by its callers: a caller can
    /// be added, and this cannot be bypassed by adding one.
    /// </summary>
    [Fact]
    public void TheChokePointReadsTheRowItself()
    {
        Assert.True(
            AssemblyCalls.Calls(
                typeof(SteamVrRuntime).Assembly,
                nameof(SteamVrRuntime),
                nameof(SteamVrRuntime.HandsAndHead),
                "get_Pointing"),
            $"{nameof(SteamVrRuntime.HandsAndHead)} does not read {nameof(SteamVrRuntime.Pointing)}, "
            + "so the device loop runs whatever the row says");
    }

    /// <summary>
    /// <b>The other road to the device loop is gone rather than gated.</b> <c>Controllers()</c>
    /// wrapped <c>HandsAndHead</c> and threw the head away; nothing had called it for some time.
    /// A public method that reaches the loop is a public method somebody adds a caller to, and
    /// the point of a withdrawal is that there is nothing left to call.
    /// </summary>
    [Fact]
    public void ThereIsNoOtherWayToAskForTheControllers()
    {
        var named = typeof(SteamVrRuntime)
            .GetMethods()
            .Select(method => method.Name)
            .Where(name => name.Contains("Controller", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(named);
    }

    /// <summary>
    /// The ninety-hertz loop is the whole exposure — its cadence is what turned a session into
    /// 350,000 pose reads — so the host has to consult the row before it starts one, and again
    /// before it lets a running one stand.
    /// </summary>
    [Fact]
    public void TheHostAsksTheRowBeforeItRunsTheAimLoopOrReadsAGesture()
    {
        foreach (var method in new[] { "Serve", "Carry", "Configure" })
        {
            Assert.True(
                AssemblyCalls.Calls(typeof(VrHost).Assembly, nameof(VrHost), method, "get_Pointing")
                || AssemblyCalls.Calls(typeof(VrHost).Assembly, nameof(VrHost), method, "set_Pointing"),
                $"{nameof(VrHost)}.{method} neither reads nor writes {nameof(SteamVrRuntime.Pointing)}");
        }
    }

    /// <summary>
    /// <b>The aim loop has exactly one place that starts it</b>, and it is the one above that
    /// reads the row. A second start would be a ninety-hertz thread nothing gates, which is
    /// indistinguishable from the withdrawal not having happened.
    /// </summary>
    [Fact]
    public void OnlyTheServeEverStartsTheAimLoop()
    {
        Assert.Equal(
            ["VrHost.Serve"],
            AssemblyCalls.Callers(typeof(VrHost).Assembly, nameof(VrAimLoop), nameof(VrAimLoop.Start)));
    }

    /// <summary>
    /// The beam and the cursor go with it. Neither is a controller call — they are overlay quads
    /// — but a beam with nothing driving it is a visible artefact of a feature that is off, so
    /// they are built and taken down by the one method that knows which way the row is set.
    /// </summary>
    [Fact]
    public void TheGuidesAreBuiltOnlyWhereTheRowIsConsulted()
    {
        Assert.Equal(
            ["SteamVrRuntime.Guides"],
            AssemblyCalls.Callers(typeof(SteamVrRuntime).Assembly, nameof(SteamVrRuntime), "Sprite"));

        Assert.True(
            AssemblyCalls.Calls(typeof(SteamVrRuntime).Assembly, nameof(SteamVrRuntime), "Guides", "get_Pointing"),
            "Guides does not read Pointing, so the beam is built whatever the row says");
    }

    /// <summary>
    /// The row is reachable by voice with no model in the path, which is the route a Commander in
    /// a headset has: with the controllers withdrawn there is no Settings tab in there to open.
    /// </summary>
    [Fact]
    public void TheRowCanBeTurnedBackOnByVoice()
    {
        var row = Assert.Single(
            TestSurface.CreateFull().Registry.All.SelectMany(capability => capability.Descriptor.Settings),
            setting => setting.Key == VrCapability.ControllersKey);

        Assert.Contains(row.Commands, command => command.Value == "true");
        Assert.Contains(row.Commands, command => command.Value == "false");
    }
}
