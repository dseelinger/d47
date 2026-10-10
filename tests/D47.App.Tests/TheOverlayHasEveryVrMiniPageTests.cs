using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Windowing;
using D47.Core.Storage;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheOverlayHasEveryVrMiniPageTests
{
    /// <summary>Both surfaces are furnished from the same services and must list the same destinations.</summary>
    [AvaloniaFact]
    public void TheOverlayListsTheSameDestinationsAsTheHeadset()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var root = TestSurface.MemoryFolder("d47-overlay-pages");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), new MemoryFileSystem(), NullLogger<OnFootBuildStore>.Instance);
        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);
        var adventures = AdventureFixture.Surface(paths);
        var commanders = new CommanderRoster(() => null, () => 0, () => null, _ => { });

        var headset = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            settingsPage: () => new Avalonia.Controls.TextBlock { Text = "settings" },
            checklists: checklists,
            ships: ships,
            gameState: () => state,
            onFoot: onFoot,
            unlocks: unlocks,
            adventures: adventures,
            commanders: commanders);

        var overlay = new OverlayPanel(
            new PanelViewModel(),
            settings,
            viewState,
            NullLogger<OverlayPanel>.Instance,
            services: new MiniPanelServices
            {
                Checklists = checklists,
                Ships = ships,
                GameState = () => state,
                OnFoot = onFoot,
                Unlocks = unlocks,
                Adventures = adventures,
                Commanders = commanders,
            });

        Dispatcher.UIThread.RunJobs();

        var expected = Keys(headset.Nav);
        var actual = Keys(overlay.Nav);

        Assert.True(expected.Count > 10, $"The fixture furnished too little to compare: {string.Join(", ", expected)}");
        Assert.Equal(expected, actual);

        overlay.Close();
    }

    private static List<string> Keys(PanelNavigator nav) =>
        [.. nav.Destinations
            .Where(destination => destination.Tab is not (PanelTab.Settings or PanelTab.Search))
            .Select(destination => $"{destination.Tab}/{destination.Root.Key}")];

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-22T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-22T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-08-22T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }
}
