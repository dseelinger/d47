using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The two tabs the headset pokes on every tick do not ask to be redrawn when nothing on them moved.</summary>
[Trait("Category", "Integration")]
public class TheTickingTabsDoNotAskForARedrawTests
{
    private static readonly DateTimeOffset Instant =
        new(3310, 5, 17, 9, 41, 30, TimeSpan.Zero);

    /// <summary>A clock a test moves itself, so a tick only changes something when it means to.</summary>
    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = Instant;
    }

    /// <summary>Ten ticks inside one second do not dirty the headset's surface.</summary>
    [AvaloniaFact]
    public void AnEngineerRankingThatDidNotMoveDoesNotAskForARedraw()
    {
        var root = TempFolders.Create("d47-ticking-tabs-tests");

        // No game state at all, which is the stillest case there is: the stamp behind the ranking cannot
        // change, so every tick after the first has nothing to draw.
        CommanderGameState? state = null;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(
            Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        var kit = new OnFootBuildStore(
            Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

        var (settings, _, _) = TestSurface.Create();

        using var panel = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            ships: new ShipPlanService(builds, checklists, () => state),
            gameState: () => state,
            onFoot: new OnFootPlanService(kit, checklists, () => state),
            unlocks: new EngineerPlanService(builds, kit, checklists, () => state));

        Assert.True(panel.Nav.Select(PanelTab.Assets), "the headset furnishes Asset Mgmt");
        Dispatcher.UIThread.RunJobs();

        Settle(panel);

        // The ordinary case: the Commander is standing still, so the ranking behind this page has not changed
        // and there is nothing new to draw.
        for (var tick = 0; tick < 30; tick++)
        {
            panel.TickEngineers();
        }

        Assert.False(
            panel.IsDirty,
            "an engineer ranking that has not moved does not ask to be redrawn");
    }

    /// <summary>
    /// Draws twice, which leaves the surface clean — a surface that has just been served has nothing
    /// outstanding, and the assertions above are about what happens next.
    /// </summary>
    private static void Settle(VrPanelSurface panel)
    {
        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        for (var pass = 0; pass < 2; pass++)
        {
            unsafe
            {
                fixed (byte* pixels = buffer)
                {
                    panel.Draw((IntPtr)pixels, width * 4);
                }
            }
        }

        Assert.False(panel.IsDirty, "a surface just drawn is clean");
    }
}
