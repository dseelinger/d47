using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>The goals capability's surface.</summary>
public class GoalsCapabilityTests
{
    private static readonly ControlContext[] Modes =
    [
        ControlContext.None, ControlContext.Docked, ControlContext.Landed, ControlContext.NormalSpace,
        ControlContext.Supercruise, ControlContext.Hyperspace, ControlContext.Srv, ControlContext.OnFoot,
        ControlContext.Fighter,
    ];

    private static IReadOnlyList<string> Advertised(CapabilityRegistry registry, ControlContext context) =>
        [.. ToolProfiles.For(registry, context, actionsEnabled: true).Tools.Select(tool => tool.Name)];

    [Fact]
    public void ReadingTheArcsIsAdvertisedInEveryMode()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        Assert.All(Modes, mode => Assert.Contains("get_goals", Advertised(registry, mode)));
    }

    /// <summary>
    /// The two that write are unreachable from the model in every mode, by the same mechanism Phases 31
    /// to 33 used: a Protected tool is never advertised, and the registry refuses it again at the call.
    /// </summary>
    [Fact]
    public void NothingThatWritesIsAdvertisedInAnyMode()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        Assert.All(Modes, mode =>
        {
            var advertised = Advertised(registry, mode);

            Assert.DoesNotContain("get_goal_step", advertised);
            Assert.DoesNotContain("set_goal_aside", advertised);
        });
    }

    [Fact]
    public void OnlyTheReadbackIsUnprotected()
    {
        using var install = new TempInstall();

        var tools = TestSurface.For(install).Registry.Find(GoalsCapability.Id)!.Descriptor.Tools;

        Assert.Equal(3, tools.Count);
        Assert.False(tools.Single(tool => tool.Name == "get_goals").Protected);
        Assert.All(
            tools.Where(tool => tool.Name != "get_goals"),
            tool => Assert.True(tool.Protected));
    }

    /// <summary>The relief valve must stay shut.</summary>
    [Fact]
    public void AddingAnAdvertisedToolDidNotOpenTheReliefValve()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var srv = ToolProfiles.For(registry, ControlContext.Srv, actionsEnabled: true);

        Assert.True(
            srv.Bytes <= ToolProfiles.ComfortableBytes,
            $"The SRV profile is {srv.Bytes} bytes against {ToolProfiles.ComfortableBytes}.");

        // The controls themselves, because the way this failed in Phase 18 was as "the SRV's controls are
        // missing in the SRV" with nothing pointing at the size.
        Assert.Contains("control_srv", srv.Tools.Select(tool => tool.Name));
    }

    /// <summary>
    /// Reading the journals is a press on an Info row, so nothing on the tool surface can start a pass
    /// over hundreds of files.
    /// </summary>
    [Fact]
    public void TheBackfillIsARowAPersonPressesRatherThanATool()
    {
        using var install = new TempInstall();

        var row = Assert.Single(
            TestSurface.For(install).Registry.Find(GoalsCapability.Id)!.Descriptor.Settings);

        Assert.Equal(GoalsCapability.StoreKey, row.Key);
        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.Null(row.Binding?.Write);
    }
}
