using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Materials tiles and rows and Statistics rows take focus, show Tile2 under keyboard focus, and open on Enter (#1516).</summary>
public class MaterialsTilesAndStatisticsRowsTakeKeyboardFocusTests
{
    private static (Window Window, PanelView Panel) OpenMaterials()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var root = TestSurface.MemoryFolder("d47-materials-keys-tests");
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), new MemoryFileSystem(), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var ship = ships.BuildFor(12, "anaconda", "Sacred Wings");
        ships.Plan(ship.Id, new SlotPlan("MainEngines") { Blueprint = "Dirty Drive Tuning", Grade = 5, Module = "Thrusters" });

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, State, kit);

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Assets;
        panel.Nav.SelectRoot(LoadoutPages.GapRoot);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static List<Border> Rows(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Border>()
            .Where(c => c.Classes.Contains(ListRow.OpensClass) && c.IsEffectivelyVisible)
            .OrderBy(c => c.TranslatePoint(default, panel)!.Value.Y)];

    private static Color Fill(Border row) => ((ISolidColorBrush)row.Background!).Color;

    private static Color Theme(string key) => ((ISolidColorBrush)Application.Current!.FindResource(key)!).Color;

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void APlanRowTakesFocusDrawsTile2ThenEnterOpensItsMaterial()
    {
        var (window, panel) = OpenMaterials();
        var rows = Rows(panel);
        Assert.NotEmpty(rows);

        var row = rows[0];
        Assert.True(row.Focusable);
        Assert.Equal(Theme(ThemeManager.TileKey), Fill(row));

        row.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Theme(ThemeManager.Tile2Key), Fill(row));

        var before = panel.Nav.Trail.Count;
        Press(window, Key.Enter);
        Assert.True(panel.Nav.Trail.Count > before);

        window.Close();
    }

    [AvaloniaFact]
    public void FocusByPointerDoesNotKeepTile2()
    {
        var (window, panel) = OpenMaterials();
        var row = Rows(panel)[0];

        row.Focus(NavigationMethod.Pointer);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Theme(ThemeManager.TileKey), Fill(row));

        window.Close();
    }

    [AvaloniaFact]
    public void AStatisticsRowOpensItsSectionOnEnter()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var root = TestSurface.MemoryFolder("d47-statistics-keys-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));
        var json = """{"event":"Statistics","Combat":{"Bounties_Claimed":1842},"Crime":{"Fines":3}}""";
        state.Apply(new JournalEvent(DateTimeOffset.UtcNow, "Statistics", JsonDocument.Parse(json).RootElement));

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCommanderName(() => state.Identity.Name);
        panel.EnableStanding(() => state);
        panel.EnableStatistics(() => state);
        panel.EnableChecklist(checklists);
        panel.EnableSearch();

        var window = new Window { Content = panel, Width = 1280, Height = 1050 };
        window.Show();

        panel.Tab = PanelTab.Commander;
        panel.Nav.SelectRoot(PanelTab.Commander, StatisticsPage.RootKey);
        panel.FindControl<TextBox>("SearchInput")!.Text = "bounties";
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetVisualDescendants().OfType<StatisticsPage>().Single();
        var row = page.GetVisualDescendants().OfType<Border>().First(c => c.Classes.Contains(ListRow.OpensClass));
        Assert.True(row.Focusable);

        row.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        Press(window, Key.Enter);

        var sidebar = page.GetVisualDescendants().OfType<Sidebar>().Single();
        Assert.Equal("Combat", sidebar.Selected);

        window.Close();
    }
}
