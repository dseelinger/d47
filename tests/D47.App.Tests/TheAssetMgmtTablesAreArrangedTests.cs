using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Compare, a ship's slot table, Materials needed and the Engineers directory take the COLUMNS control and keep what it stores (#2524).</summary>
[Trait("Category", "Integration")]
public sealed class TheAssetMgmtTablesAreArrangedTests
{
    private const string Commander = """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static readonly string[] CompareLines =
    [
        Commander,
        """{"timestamp":"2026-08-18T09:01:00Z","event":"Loadout","Ship":"mandalay","ShipID":1,"ShipName":"Far Walker","ShipIdent":"FW-22","HullValue":200000000,"ModulesValue":88120000,"Rebuy":14406000,"UnladenMass":318.2,"CargoCapacity":32,"MaxJumpRange":71.8,"FuelCapacity":{"Main":32.0,"Reserve":0.5},"Modules":[]}""",
        """{"timestamp":"2026-08-18T09:02:00Z","event":"Loadout","Ship":"type9","ShipID":2,"ShipName":"Deep Hold","ShipIdent":"DH-09","HullValue":76555842,"ModulesValue":335884158,"Rebuy":20622000,"UnladenMass":1120.6,"CargoCapacity":400,"MaxJumpRange":18.4,"FuelCapacity":{"Main":64.0,"Reserve":0.77},"Modules":[]}""",
    ];

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static void Press(Visual root, string name)
    {
        var button = Named<Button>(root, name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Keys(Visual root, string id) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader).Layout.Columns.Select(column => column.Key)];

    private static string[] Keys(ArrangedTable<LoadoutPages.SlotLine> table) =>
        [.. table.Layout.Columns.Select(column => column.Key)];

    private static void Add(Visual root, string id, string key)
    {
        if (root.GetVisualDescendants().OfType<CheckBox>().All(box => box.Name != ColumnArranger.NameOf(id, $"Show.{key}")))
        {
            Press(root, ColumnArranger.NameOf(id, "Add"));
        }

        Named<CheckBox>(root, ColumnArranger.NameOf(id, $"Show.{key}")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static ComparePage Compare(ColumnArrangementMemory memory, CommanderGameState state) =>
        new(() => state, null, null, memory);

    [AvaloniaFact]
    public void CompareDrawsItsOwnColumnsAndAnAddedMovedAndRemovedColumnIsKept()
    {
        var views = Views();
        var state = JournalScript.State(CompareLines);
        var (window, page) = PageHost.Open(() => Compare(new ColumnArrangementMemory(views), state));

        Assert.Equal(["Ship", "Cargo", "Jump", "Unladen", "Fuel", "Value", "Rebuy", "Where"], Keys(page, ComparePage.TableId));
        Assert.NotNull(Named<Button>(page, ColumnArranger.NameOf(ComparePage.TableId, "Tile")));

        Press(page, ColumnArranger.NameOf(ComparePage.TableId, "Tile"));
        Press(page, "CompareHead.Choose.Fuel");
        Press(page, ColumnArranger.NameOf(ComparePage.TableId, "Remove"));
        Press(page, "CompareHead.Choose.Value");
        Press(page, ColumnArranger.NameOf(ComparePage.TableId, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(ComparePage.TableId, "Done"));

        var arranged = Keys(page, ComparePage.TableId);
        Assert.Equal(["Ship", "Cargo", "Jump", "Value", "Unladen", "Rebuy", "Where"], arranged);
        window.Close();

        var (againWindow, again) = PageHost.Open(() => Compare(new ColumnArrangementMemory(views), state));
        Assert.Equal(arranged, Keys(again, ComparePage.TableId));

        Press(again, ColumnArranger.NameOf(ComparePage.TableId, "Tile"));
        Press(again, ColumnArranger.NameOf(ComparePage.TableId, "Default"));
        Assert.Equal(["Ship", "Cargo", "Jump", "Unladen", "Fuel", "Value", "Rebuy", "Where"], Keys(again, ComparePage.TableId));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void ACompareSortFollowsItsColumnWhenItMoves()
    {
        var state = JournalScript.State(CompareLines);
        var (window, page) = PageHost.Open(() => Compare(new ColumnArrangementMemory(Views()), state));

        bool Sorted() =>
            page.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text is { } said && said.StartsWith("CARGO", StringComparison.Ordinal) && said.EndsWith('▼'));

        Press(page, "CompareHead.Cargo");
        Assert.True(Sorted());

        Press(page, ColumnArranger.NameOf(ComparePage.TableId, "Tile"));
        Press(page, "CompareHead.Choose.Cargo");
        Press(page, ColumnArranger.NameOf(ComparePage.TableId, "MoveRight"));
        Press(page, ColumnArranger.NameOf(ComparePage.TableId, "Done"));

        Assert.Equal("Cargo", Keys(page, ComparePage.TableId)[2]);
        Assert.True(Sorted());
        window.Close();
    }

    [AvaloniaFact]
    public void TheSlotTableDrawsItsFiveColumnsAndTheArrowMovesWithCurrent()
    {
        var views = Views();
        var table = LoadoutPages.NewSlotTable(null, new ColumnArrangementMemory(views));

        Assert.Equal(["Slot", "Current", "Mark", "Plan", "Draw"], Keys(table));
        Assert.Contains(table.Choices, choice => choice.Key == "Size" && !choice.Shown);

        table.Show("Size");
        table.Choose("Current");
        table.Move(left: false);

        var arranged = Keys(table);
        Assert.Equal(["Slot", "Plan", "Current", "Mark", "Draw", "Size"], arranged);

        var again = LoadoutPages.NewSlotTable(null, new ColumnArrangementMemory(views));
        Assert.Equal(arranged, Keys(again));

        again.Default();
        Assert.Equal(["Slot", "Current", "Mark", "Plan", "Draw"], Keys(again));
    }

    [AvaloniaFact]
    public void EverySlotTableHeaderPressesAndASizeColumnReadsTheSlotsSize()
    {
        var table = LoadoutPages.NewSlotTable(null, new ColumnArrangementMemory(Views()));
        table.Show("Size");

        var row = new LoadoutRow("k", "Slot", "Slot", null, false)
        {
            Parts = new LoadoutParts(4, "Hardpoint 1", new LoadoutSide(null, null, null, null, null, []), null, "empty"),
        };

        var header = LoadoutPages.SlotHeader(table);
        var button = Assert.IsType<Button>(LoadoutPages.SlotRow(row, () => { }, table));
        var cells = Assert.IsType<TableGrid>(button.Content);

        Assert.Equal(5, header.GetVisualDescendants().OfType<Button>().Count());
        Assert.Contains(cells.Children.OfType<TextBlock>(), cell => cell.Text == "4");
    }

    [AvaloniaFact]
    public void MaterialsNeededOffersGradeAndFoundAtAndKeepsTheGaugeLast()
    {
        var views = Views();
        var root = TestSurface.MemoryFolder("d47-materials-arranged-tests");
        var store = new GameStateStore();

        foreach (var line in new[]
        {
            Commander,
            """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""",
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[]}""",
        })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        CommanderGameState? State() => store.Active;

        var (checklists, _, _, ships, kit, _) = PlanBench.Over(root, State);
        var ship = ships.BuildFor(12, "anaconda", "Sacred Wings");
        ships.Plan(ship.Id, new D47.Core.Ships.SlotPlan("MainEngines") { Blueprint = "Dirty Drive Tuning", Grade = 5, Module = "Thrusters" });

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableColumnArrangements(new ColumnArrangementMemory(views));
        panel.EnableLoadout(ships, checklists, State, kit);

        var window = new Avalonia.Controls.Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();
        panel.Nav.Show(PanelTabNames.AssetsRoot);
        Assert.True(panel.Nav.Show(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        var id = MaterialsPage.NeededTable;
        Assert.Equal(["Material", "Ledger", "HeldNeed", "Short", "Gauge"], Keys(panel, id));

        Press(panel, ColumnArranger.NameOf(id, "Tile"));
        Add(panel, id, "Grade");
        Add(panel, id, "FoundAt");
        Press(panel, ColumnArranger.NameOf(id, "Done"));

        Assert.Equal(["Material", "Ledger", "HeldNeed", "Short", "Grade", "FoundAt", "Gauge"], Keys(panel, id));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheEngineersDirectoryKeepsItsArrangementPerSurface(bool headset)
    {
        var views = Views();
        var state = EngineersTabTests.State();
        var root = TestSurface.MemoryFolder("d47-engineers-arranged-tests");
        var unlocks = PlanBench.Over(root, () => state).Unlocks;
        var source = new EngineerSource(unlocks.Report);
        var id = EngineerDirectoryPage.TableId;

        EngineerDirectoryPage Make() =>
            new(source, new PanelNavigator(), null, headset, null, new ColumnArrangementMemory(views, headset));

        var (window, page) = PageHost.Open(Make);

        Assert.Equal(["Portrait", "Engineer", "Status", "Modules", "Planned", "Opens", "Distance"], Keys(page, id));

        Press(page, ColumnArranger.NameOf(id, "Tile"));
        Add(page, id, "ReferredBy");
        Press(page, $"{EngineerDirectoryPage.HeadName}.Choose.Distance");
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(page, id);
        Assert.Equal("Portrait", arranged[0]);
        Assert.Contains("ReferredBy", arranged);
        Assert.True(Array.IndexOf(arranged, "Distance") < Array.IndexOf(arranged, "Opens"));
        window.Close();

        Assert.NotNull(new ColumnArrangementMemory(views, headset).Remembered(id));
        Assert.Null(new ColumnArrangementMemory(views, !headset).Remembered(id));

        var (againWindow, again) = PageHost.Open(Make);
        Assert.Equal(arranged, Keys(again, id));
        againWindow.Close();
    }
}
