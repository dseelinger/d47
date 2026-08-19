using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// An intended hull does not stop the rest of the fleet being opened
/// (remediation.md 11, item 15).
/// <para>
/// Reported with a picture: a planned Python open on the right, the fleet list on the left, and
/// pressing any of the owned ships in it doing nothing.
/// </para>
/// </summary>
public class AnIntendedHullDoesNotBlockTheFleetTests
{
    private static (PanelView Panel, ShipPlanService Ships) Fleet()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-intended-hull-tests"));

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

        // Two owned ships and one hull the Commander only intends to buy.
        ships.BuildFor(12, "CobraMkV", "Reaper");
        ships.BuildFor(13, "Type8", "Cartage");
        ships.Intend("Python");

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => null, null);

        var window = new Window { Content = panel, Width = 1400, Height = 700 };

        window.Show();
        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return (panel, ships);
    }

    private static NavCrumb Crumb(ShipBuild build, string word) =>
        new(LoadoutPages.ShipPrefix + build.Id, word) { Level = LoadoutPages.ShipPrefix };

    /// <summary>The report: with the intended hull open, an owned ship still opens.</summary>
    [AvaloniaFact]
    public void AnOwnedShipStillOpensWhileTheIntendedOneIsShowing()
    {
        var (panel, ships) = Fleet();

        var intended = ships.Store.Builds.Single(build => !build.IsOwned);
        var owned = ships.Store.Builds.First(build => build.IsOwned);

        panel.Nav.Drill(Crumb(intended, "Python"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ships", "Python"], panel.Nav.Trail.Select(crumb => crumb.Word));

        panel.Nav.Drill(Crumb(owned, "Reaper"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ships", "Reaper"], panel.Nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>And the other way round, which is the same claim from the other side.</summary>
    [AvaloniaFact]
    public void AndTheIntendedOneOpensWhileAnOwnedShipIsShowing()
    {
        var (panel, ships) = Fleet();

        var intended = ships.Store.Builds.Single(build => !build.IsOwned);
        var owned = ships.Store.Builds.First(build => build.IsOwned);

        panel.Nav.Drill(Crumb(owned, "Reaper"));
        Dispatcher.UIThread.RunJobs();

        panel.Nav.Drill(Crumb(intended, "Python"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ships", "Python"], panel.Nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>
    /// Every ship in the fleet is drawn as its own row with its own key, so no two of them can be
    /// one entry — an intended hull keys on its build like the rest.
    /// </summary>
    [Fact]
    public void EveryShipHasItsOwnKey()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-intended-keys-tests"));

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

        ships.BuildFor(12, "CobraMkV", "Reaper");
        ships.BuildFor(13, "Type8", "Cartage");
        ships.Intend("Python");

        var mode = new ShipsMode(ships, checklists, () => null);
        var keys = mode.Items().Select(row => row.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, keys.Length);
    }
}
