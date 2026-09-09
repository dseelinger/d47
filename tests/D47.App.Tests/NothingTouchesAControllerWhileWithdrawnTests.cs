using D47.App.Headset;
using D47.Core.Capabilities.Builtin;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>The motion controllers are withdrawn, and this is what makes that testable rather than
/// hopeful.</summary>
public class NothingTouchesAControllerWhileWithdrawnTests
{
    /// <summary>Out of the box d47 does not touch them at all.</summary>
    [Fact]
    public void TheyAreOffOutOfTheBox()
    {
        Assert.False(new D47.Core.Configuration.D47Settings().Vr.Controllers);
    }

    /// <summary>One choke point for every per-device call.</summary>
    [Theory]
    [InlineData("CVRSystem", "GetTrackedDeviceClass")]
    [InlineData(nameof(SteamVrRuntime), "Note")]
    [InlineData(nameof(SteamVrRuntime), "GripToTip")]
    public void EveryPerDeviceCallIsMadeFromHandsAndHeadAndNowhereElse(string type, string call)
    {
        var callers = AssemblyCalls.Callers(typeof(SteamVrRuntime).Assembly, type, call);

        Assert.Equal(["SteamVrRuntime.HandsAndHead"], callers);
    }

    /// <summary>And that one method asks the row before it does any of it.</summary>
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

    /// <summary>The other road to the device loop is gone rather than gated.</summary>
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
    /// The ninety-hertz loop is the whole exposure — its cadence is what turned a session into 350,000
    /// pose reads — so the host has to consult the row before it starts one, and again before it lets a
    /// running one stand.
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
    /// The aim loop has exactly one place that starts it, and it is the one above that reads the row.
    /// </summary>
    [Fact]
    public void OnlyTheServeEverStartsTheAimLoop()
    {
        Assert.Equal(
            ["VrHost.Serve"],
            AssemblyCalls.Callers(typeof(VrHost).Assembly, nameof(VrAimLoop), nameof(VrAimLoop.Start)));
    }

    /// <summary>The beam and the cursor go with it.</summary>
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
    /// The row is reachable by voice with no model in the path, which is the route a Commander in a
    /// headset has: with the controllers withdrawn there is no Settings tab in there to open.
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
