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

/// <summary>
/// The two tabs the headset pokes on every tick do not ask to be redrawn when nothing on them
/// moved (#23, reported as <em>"flickering that is so bad as to make Engineers and Utilities tabs
/// unreadable — no flicker on other tabs"</em>).
/// <para>
/// <b>This is the fourth time this fault has shipped</b>, which is why it is worth a file of its
/// own rather than a line in an existing one. 0.22.2 was a carry marking the surface dirty every
/// frame, 0.24.0 was the aiming highlight doing it unconditionally, 0.39.1 was Utilities rebuilding
/// every timer row ten times a second — and this is the flag those rebuilds sat behind, which
/// 0.39.1 left in place. Every one of them re-rasterises the whole widget tree and hands SteamVR an
/// image identical to the last one.
/// </para>
/// <para>
/// Asserted on the dirty flag rather than on pixels, for the same reason
/// <see cref="TheVrScrollbarsTests.RestingAimDoesNotAskForARedraw"/> is: the flag is what the
/// serving loop reads to decide whether to submit at all.
/// </para>
/// </summary>
public class TheTickingTabsDoNotAskForARedrawTests
{
    private static readonly DateTimeOffset Instant =
        new(3310, 5, 17, 9, 41, 30, TimeSpan.Zero);

    /// <summary>A clock a test moves itself, so a tick only changes something when it means to.</summary>
    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = Instant;
    }

    /// <summary>
    /// Ten ticks inside one second do not dirty the headset's surface. The Engineers half, driven
    /// through the real <see cref="VrPanelSurface"/> because that is where the flag lives.
    /// </summary>
    [AvaloniaFact]
    public void AnEngineerRankingThatDidNotMoveDoesNotAskForARedraw()
    {
        var root = TempFolders.Create("d47-ticking-tabs-tests");

        // No game state at all, which is the stillest case there is: the stamp behind the ranking
        // cannot change, so every tick after the first has nothing to draw.
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

        Assert.True(panel.Nav.Select(PanelTab.Engineers), "the headset furnishes the Engineers tab");
        Dispatcher.UIThread.RunJobs();

        Settle(panel);

        // The ordinary case: the Commander is standing still, so the ranking behind this page has
        // not changed and there is nothing new to draw. Before #23 the flag went up on the
        // strength of the call rather than the answer, and this loop raised it thirty times.
        for (var tick = 0; tick < 30; tick++)
        {
            panel.TickEngineers();
        }

        Assert.False(
            panel.IsDirty,
            "an engineer ranking that has not moved does not ask to be redrawn");
    }

    /// <summary>
    /// The Utilities half. Asserted one level down, on <see cref="PanelView.TickClocks"/>, because
    /// the headset builds that page against the real wall clock and a test cannot move it —
    /// and because the change detection this fix adds is what lives there.
    /// </summary>
    [AvaloniaFact]
    public void AClockWhoseDigitsDidNotMoveDoesNotAskForARedraw()
    {
        var root = TempFolders.Create("d47-ticking-tabs-tests");
        var clock = new Clock();

        var alarms = new AlarmStore(
            Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableUtilities(new Timekeeper(alarms), alarms, () => clock.Now, () => TimeZoneInfo.Utc);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Utilities;
        Dispatcher.UIThread.RunJobs();

        panel.TickClocks();

        // Everything on this page reads to the minute — both clocks are HH:mm, and a timer's
        // countdown is whole minutes — so at 10 Hz the page is asked six hundred times for each
        // change it has. Thirty of those, well inside one minute.
        for (var tick = 0; tick < 30; tick++)
        {
            clock.Now = clock.Now.AddMilliseconds(100);

            Assert.False(
                panel.TickClocks(),
                "a clock whose digits did not change does not ask to be redrawn");
        }

        // And the other half of the bargain, because a fix that buys stillness by dropping real
        // changes is a clock that stopped.
        clock.Now = clock.Now.AddMinutes(2);

        Assert.True(panel.TickClocks(), "a minute that actually ticked is drawn");
    }

    /// <summary>
    /// Draws twice, which leaves the surface clean — a surface that has just been served has
    /// nothing outstanding, and the assertions above are about what happens next.
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
