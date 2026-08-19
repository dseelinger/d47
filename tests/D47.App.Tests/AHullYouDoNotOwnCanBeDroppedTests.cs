using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A hull the Commander intends to buy can be dropped again (remediation.md 11, item 7).
/// <para>
/// Reported plainly: the button that plans a hull you do not own is on the index, and nothing
/// undid it — a Python planned by mistake, or changed your mind about, was on the fleet list for
/// good.
/// </para>
/// <para>
/// An owned ship is a different matter and stays: it comes out of the journal, so it is not d47's
/// to remove. That is the same rule the checklist already draws between a computed line and a
/// written one.
/// </para>
/// </summary>
public class AHullYouDoNotOwnCanBeDroppedTests
{
    private static (PanelView Panel, ShipPlanService Ships) Fleet()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-drop-hull-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        ships.Intend("Python");

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => null, null);

        var window = new Window { Content = panel, Width = 1200, Height = 700 };

        window.Show();
        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return (panel, ships);
    }

    private static IReadOnlyList<string> Shown(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button? Named(PanelView panel, string label) =>
        panel.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => (button.Content as string) == label);

    private static Button Option(PanelView panel, string label) =>
        panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(text => text.Text == label));

    private static void OpenTheHull(PanelView panel, ShipPlanService ships)
    {
        var build = ships.Store.Builds.Single();

        panel.Nav.Drill(
            new NavCrumb(LoadoutPages.ShipPrefix + build.Id, "Python")
            {
                Level = LoadoutPages.ShipPrefix,
            });
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The affordance exists at all, which it did not.</summary>
    [AvaloniaFact]
    public void APlannedHullOffersToBeDropped()
    {
        var (panel, ships) = Fleet();

        OpenTheHull(panel, ships);

        Assert.NotNull(Named(panel, "Drop this hull"));
    }

    /// <summary>And asks before it acts, because there is no way back from it.</summary>
    [AvaloniaFact]
    public void DroppingAsksFirst()
    {
        var (panel, ships) = Fleet();

        OpenTheHull(panel, ships);

        Named(panel, "Drop this hull")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(ships.Store.Builds);

        Option(panel, "Keep it").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(ships.Store.Builds);
    }

    [AvaloniaFact]
    public void AndDropsItWhenTheAnswerIsYes()
    {
        var (panel, ships) = Fleet();

        OpenTheHull(panel, ships);

        Named(panel, "Drop this hull")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Option(panel, "Drop it").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(ships.Store.Builds);
    }

    /// <summary>
    /// An owned ship is a fact about the Commander's fleet rather than something they wrote, so
    /// there is nothing to drop and no button offering to.
    /// </summary>
    [Fact]
    public void AnOwnedShipIsNotDroppable()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-drop-owned-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var owned = ships.BuildFor(12, "Python", "Reaper");
        var mode = new ShipsMode(ships, checklists, () => null);

        Assert.Null(mode.DropLabel(owned.Id));

        // And the intended one beside it still is, so this is a rule about the ship rather than
        // about the page being cautious.
        ships.Intend("Anaconda");

        var intended = ships.Store.Builds.Single(build => !build.IsOwned);

        Assert.Equal("Drop this hull", mode.DropLabel(intended.Id));
    }
    /// <summary>
    /// And it leaves the fleet list (remediation.md 13, item 1).
    /// <para>
    /// The store lost the build and the list beside it went on showing the hull, which is the
    /// only place the Commander looks to find out whether the drop took. A wide panel keeps the
    /// index on screen next to the ship, so the row was still there to press.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void AndTheHullLeavesTheFleetList()
    {
        var (panel, ships) = Fleet();

        OpenTheHull(panel, ships);

        Assert.Contains(Shown(panel), line => line.Contains("Python", StringComparison.Ordinal));

        Named(panel, "Drop this hull")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Option(panel, "Drop it").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(ships.Store.Builds);

        // The index says the fleet is empty, rather than listing a hull nothing has a plan for.
        Assert.DoesNotContain(
            Shown(panel), line => line.Contains("Python (Python)", StringComparison.Ordinal));

        // And going back to it does not bring the row back either — the level was cached.
        panel.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            Shown(panel), line => line.Contains("Python (Python)", StringComparison.Ordinal));
    }

    /// <summary>
    /// And it leaves the list on a narrow panel too, which is the case that was broken
    /// (remediation.md 13, item 1).
    /// <para>
    /// One pane, so opening the ship scrolls the index out of the strip — which the strip caches
    /// rather than rebuilds. The cached page had unsubscribed on the way out and heard nothing
    /// afterwards, so coming back showed a hull that no longer had a plan behind it.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void AndLeavesItOnANarrowPanelToo()
    {
        var (panel, ships) = Fleet();

        ((Window)panel.Parent!).Width = 700;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, panel.GetVisualDescendants().OfType<DrillView>().Single().Panes);

        OpenTheHull(panel, ships);

        Named(panel, "Drop this hull")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Option(panel, "Drop it").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        panel.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Shown(panel), line => line.Contains("Python", StringComparison.Ordinal));
        Assert.Contains(
            Shown(panel), line => line.Contains("have not seen your fleet", StringComparison.Ordinal));
    }

}
