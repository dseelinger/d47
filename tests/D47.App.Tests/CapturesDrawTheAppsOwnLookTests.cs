using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.App.Settings;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A capture taken through <see cref="AppLook.Capture"/> draws the theme the app draws, leaves
/// <see cref="Application.Current"/> as it found it, and exists for every panel tab.
/// </summary>
public class CapturesDrawTheAppsOwnLookTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    /// <summary>Red and blue swapped, so "My HUD colours" draws something Elite does not.</summary>
    private static readonly GuiColourMatrix Swapped = new(0, 0, 1, 0, 1, 0, 1, 0, 0);

    [AvaloniaFact]
    public void EachThemeDrawsADifferentCapture()
    {
        var captures = ThemeCatalog.Ids
            .Select(id => File.ReadAllBytes(AppLook.Capture(
                Transcript(), $"theme-{id}.png", id, matrix: id == ThemeCatalog.ElitePaletteId ? Swapped : null)))
            .ToList();

        for (var one = 0; one < captures.Count; one++)
        {
            for (var other = one + 1; other < captures.Count; other++)
            {
                Assert.False(
                    captures[one].SequenceEqual(captures[other]),
                    $"{ThemeCatalog.Ids[one]} and {ThemeCatalog.Ids[other]} drew the same capture");
            }
        }
    }

    [AvaloniaFact]
    public void ACaptureLeavesTheApplicationAsItFoundIt()
    {
        var application = Application.Current!;
        var styles = application.Styles.ToList();
        var resources = application.Resources.ToDictionary(pair => pair.Key, pair => pair.Value);
        var variant = application.RequestedThemeVariant;

        AppLook.Capture(Transcript(), "theme-restored.png", ThemeCatalog.Light);

        Assert.Equal(styles, application.Styles.ToList());
        Assert.Equal(resources, application.Resources.ToDictionary(pair => pair.Key, pair => pair.Value));
        Assert.Equal(variant, application.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void EveryPanelTabRendersToACapture()
    {
        foreach (var tab in Enum.GetValues<PanelTab>())
        {
            var panel = Panel();
            panel.Tab = tab;

            var path = AppLook.Capture(panel, $"tab-{tab.ToString().ToLowerInvariant()}.png");

            Assert.True(new FileInfo(path).Length > 0, $"the {tab} capture is empty");
        }
    }

    private static PanelView Transcript()
    {
        var model = new PanelViewModel();

        model.Append("Fixture One, docked. Fuel at 82 percent.\n");
        model.Append("\n> what is the beacon at Shinrarta Dezhra\n");
        model.Append("The beacon is quiet. Nothing on the board worth the detour.");

        return new PanelView { DataContext = model };
    }

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-22T19:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-08-22T19:01:00Z", "event":"Location", "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "StarPos":[55.72,17.59,27.16], "Docked":true, "StationName":"Jameson Memorial", "StationType":"Coriolis", "MarketID":128666762 }""",
                     """
                     { "timestamp":"2026-08-22T19:02:00Z", "event":"Loadout", "Ship":"anaconda",
                       "Ship_Localised":"Anaconda", "ShipID":51, "ShipName":"Flamebrand", "ShipIdent":"FB-01",
                       "MaxJumpRange":32.0,
                       "Modules":[
                         { "Slot":"Slot01_Size7", "Item":"int_shieldgenerator_size7_class5", "On":true } ] }
                     """,
                     """{ "timestamp":"2026-08-22T19:03:00Z", "event":"EngineerProgress", "Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5}] }""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>A panel with every tab furnished from the fixtures the tab tests use.</summary>
    private static PanelView Panel()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                ChecklistScope.Ship(51),
                "anaconda",
                [new BuildRequest("Slot01_Size7", "Reinforced Shields", 5)]),
            ["Slot01_Size7"]);

        var builds = new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(paths.Data, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);
        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);

        var alarms = new AlarmStore(Path.Combine(paths.Data, "alarms.json"), NullLogger<AlarmStore>.Instance);

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => state,
            () => null, () => null, null, null, NullLogger.Instance);

        var plans = new RoutePlanBook(Path.Combine(paths.Data, "route-plans.json"), NullLogger<RoutePlanBook>.Instance);

        var route = new NavRoute
        {
            Hops =
            [
                new RouteHop("Sol", "G") { Position = (0, 0, 0) },
                new RouteHop("Alpha Centauri", "K") { Position = (3, 2, 1) },
                new RouteHop("Shinrarta Dezhra", "K") { Position = (22, 9, 5) },
            ],
        };

        var panel = Transcript();

        panel.EnableLoadout(ships, checklists, () => state, onFoot);
        panel.EnableEngineers(unlocks, ships, () => state, onFoot, checklists: checklists);
        panel.EnableChecklist(checklists);
        panel.EnableRouting(new RoutingSurface(() => route, () => "Alpha Centauri", CapabilityRegistry.Build([]), plans, () => true));
        panel.EnableAdventures(new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, _ => { }, () => false, () => false, () => null, () => { }));
        panel.EnableUtilities(new Timekeeper(alarms), alarms, () => Now, () => TimeZoneInfo.Utc);

        var view = new SettingsView();
        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        return panel;
    }
}
