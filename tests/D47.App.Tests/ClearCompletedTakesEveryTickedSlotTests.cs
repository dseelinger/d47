using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship's page offers Clear completed while a slot draws the tick, and it clears exactly those slots.</summary>
public class ClearCompletedTakesEveryTickedSlotTests
{
    private const int ShipId = 12;

    private const string Plain =
        """{"timestamp":"2026-08-18T09:05:00Z","event":"Loadout","Ship":"python","ShipID":12,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}""";

    private const string Engineered =
        """{"timestamp":"2026-08-18T09:10:00Z","event":"Loadout","Ship":"python","ShipID":12,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,"Engineer":"Felicity Farseer","EngineerID":300100}},{"Slot":"PowerPlant","Item":"int_powerplant_size7_class5","On":true}]}""";

    private sealed record Bench(GameStateStore Game, ChecklistService Checklists, ShipPlanService Ships, string BuildId);

    private static Bench Set(string name)
    {
        var game = new GameStateStore();

        Apply(game, """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(game, Plain);

        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder(name));
        var checklists = TestSurface.Checklists(paths.Data);
        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => game.Active);
        var build = ships.BuildFor(ShipId, "python");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        ships.Plan(build.Id, new SlotPlan("PowerPlant", Module: "Power Plant") { Variant = "int_powerplant_size7_class5" });
        ships.Plan(build.Id, new SlotPlan("FrameShiftDrive", "Increased Range", 5));

        return new Bench(game, checklists, ships, build.Id);
    }

    private static void Apply(GameStateStore game, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        game.Apply(parsed!);
    }

    private static string[] Ticked(ShipsMode mode, string id) =>
        [.. mode.Slots(id).Where(row => row.Parts?.Met == true).Select(row => row.Key[(id.Length + 1)..])];

    [Fact]
    public void ThePressClearsTheTickedSlotsAndNoOthersInOneWrite()
    {
        var (game, checklists, ships, id) = Set("d47-clear-completed-mode");
        var mode = new ShipsMode(ships, checklists, () => game.Active);

        Assert.Null(mode.ClearMetLabel(id));

        Apply(game, Engineered);

        Assert.Equal("Clear completed", mode.ClearMetLabel(id));
        Assert.Equal(["MainEngines", "PowerPlant"], Ticked(mode, id).Order(StringComparer.Ordinal));
        Assert.Equal(Ticked(mode, id).Order(StringComparer.Ordinal), ships.MetSlots(ships.Store.Find(id)!).Order(StringComparer.Ordinal));

        var writes = 0;

        ships.Store.Changed += () => writes++;

        Assert.Equal("Cleared 2 completed slots. What they put on your checklist is kept.", mode.ClearMet(id));
        Assert.Equal(1, writes);

        var after = ships.Store.Find(id)!;

        Assert.Null(after.For("MainEngines"));
        Assert.Null(after.For("PowerPlant"));
        Assert.NotNull(after.For("FrameShiftDrive"));
        Assert.Empty(Ticked(mode, id));
        Assert.Null(mode.ClearMetLabel(id));
    }

    private static Button? Named(PanelView panel, string label) =>
        panel.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => (button.Content as string) == label);

    [AvaloniaFact]
    public void ThePressComesAndGoesWithoutLeavingThePage()
    {
        var (game, checklists, ships, id) = Set("d47-clear-completed-page");

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => game.Active, null);

        var window = new Window { Content = panel, Width = 1400, Height = 800 };

        window.Show();
        panel.Nav.Show(PanelTabNames.AssetsRoot);
        Dispatcher.UIThread.RunJobs();

        panel.Nav.Drill(new NavCrumb(LoadoutPages.ShipPrefix + id, "Python") { Level = LoadoutPages.ShipPrefix });
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Named(panel, "Clear completed"));

        Apply(game, Engineered);

        // In the app the journal invalidates the mode; here a store write is what moves the page.
        ships.Move(id, "FrameShiftDrive", 2);
        Dispatcher.UIThread.RunJobs();

        Named(panel, "Clear completed")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(ships.Store.Find(id)!.For("MainEngines"));
        Assert.NotNull(ships.Store.Find(id)!.For("FrameShiftDrive"));
        Assert.Null(Named(panel, "Clear completed"));
        Assert.Contains(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "Cleared 2 completed slots. What they put on your checklist is kept.");
    }
}
