using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A table declares its columns once as fields and draws the ones its stored arrangement names (#2515).</summary>
public class TablesAreBuiltFromOneListOfFieldsTests
{
    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
        """
        {"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[
         {"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},
         {"Name":"$int_powerdistributor_size5_class5_name;","Name_Localised":"Power Distributor","StorageSlot":3,"StarSystem":"Deciat","MarketID":3221636096,"TransferCost":212400,"TransferTime":840,"BuyPrice":3475688,"Hot":true},
         {"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":4,"StarSystem":"LHS 20","MarketID":3230448384,"TransferCost":38800,"TransferTime":1260,"BuyPrice":1178420,"Hot":false}
        ]}
        """,
    ];

    private static GameStateStore Store()
    {
        var store = new GameStateStore();

        foreach (var line in Journal)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static ViewStateStore ViewState() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static Window Show(Control page)
    {
        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = 1200,
            Height = 1400,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<TableGrid> Grids(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == StoredModulesPage.TableId)];

    private static string[] Keys(TableGrid grid) => [.. grid.Layout.Columns.Select(column => column.Key)];

    /// <summary>The header's cell names in column order.</summary>
    private static string[] HeadNames(TableGrid header) =>
        [.. header.Children.Where(child => child is not ColumnGrip).OrderBy(Grid.GetColumn).Select(child => child.Name ?? string.Empty)];

    [AvaloniaFact]
    public void WithNothingStoredStoredModulesDrawsItsFourColumns()
    {
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(ViewState())));

        var grids = Grids(window);
        Assert.Equal(3, grids.Count(grid => grid.IsHeader));

        foreach (var grid in grids)
        {
            Assert.Equal(["Module", "Class", "TransferCost", "TransferTime"], Keys(grid));
        }

        Assert.Equal(
            ["StoredHead.Module", "StoredHead.Class", "StoredHead.Transfercost", "StoredHead.Transfertime"],
            HeadNames(grids.First(grid => grid.IsHeader)));
        Assert.Contains("▲", Label(window, "StoredHead.Module"));
        window.Close();
    }

    [AvaloniaFact]
    public void EveryGroupDrawsTheStoredColumnsInTheirOrder()
    {
        var memory = new ColumnArrangementMemory(ViewState());
        memory.Remember(StoredModulesPage.TableId, ["Class", "Module", "Hot"]);
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: memory));

        var grids = Grids(window);
        Assert.Equal(3, grids.Count(grid => grid.IsHeader));
        Assert.Equal(3, grids.Count(grid => !grid.IsHeader));

        foreach (var grid in grids)
        {
            Assert.Equal(["Class", "Module", "Hot"], Keys(grid));
        }

        foreach (var header in grids.Where(grid => grid.IsHeader))
        {
            Assert.Equal(["StoredHead.Class", "StoredHead.Module", "StoredHead.Hot"], HeadNames(header));
        }

        var hot = grids.Where(grid => !grid.IsHeader)
            .Select(grid => grid.Children.OfType<TextBlock>().Single(cell => Grid.GetColumn(cell) == 2).Text)
            .ToList();
        Assert.Equal(["No", "Yes", "No"], hot);
        window.Close();
    }

    [AvaloniaFact]
    public void AWidthStoredForModuleAppliesAfterModuleMoves()
    {
        var views = ViewState();
        var widths = new ColumnWidthMemory(views);
        widths.Remember(StoredModulesPage.TableId, "Module", 300);
        var arrangement = new ColumnArrangementMemory(views);
        arrangement.Remember(StoredModulesPage.TableId, ["Class", "Module", "TransferCost"]);
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, widths, columnArrangements: arrangement));

        foreach (var header in Grids(window).Where(grid => grid.IsHeader))
        {
            Assert.Equal("Module", header.Layout.Columns[1].Key);
            Assert.Equal(300, header.ColumnDefinitions[1].ActualWidth, 0.5);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void StoredModulesSortedByClassKeepTheArrowWhenClassMovesAndLoseTheSortWhenItGoes()
    {
        var memory = new ColumnArrangementMemory(ViewState());
        var store = Store();
        var page = new StoredModulesPage(() => store.Active, columnArrangements: memory);
        var window = Show(page);

        void Redraw()
        {
            page.Filter("Shield");
            page.Filter(string.Empty);
            Dispatcher.UIThread.RunJobs();
        }

        var classHead = window.GetVisualDescendants().OfType<Button>().First(button => button.Name == "StoredHead.Class");
        classHead.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("▼", Label(window, "StoredHead.Class"));

        memory.Remember(StoredModulesPage.TableId, ["Module", "TransferCost", "Class"]);
        Redraw();

        var header = Grids(window).First(grid => grid.IsHeader);
        Assert.Equal(["StoredHead.Module", "StoredHead.Transfercost", "StoredHead.Class"], HeadNames(header));
        Assert.Contains("▼", Label(window, "StoredHead.Class"));
        Assert.DoesNotContain("▲", Label(window, "StoredHead.Module"));

        memory.Remember(StoredModulesPage.TableId, ["Module", "TransferCost"]);
        Redraw();

        Assert.Contains("▲", Label(window, "StoredHead.Module"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "StoredHead.Class");
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPageReadsTheHeadsetArrangement()
    {
        var views = ViewState();
        new ColumnArrangementMemory(views).Remember(StoredModulesPage.TableId, ["Class"]);
        new ColumnArrangementMemory(views, headset: true).Remember(StoredModulesPage.TableId, ["Hot", "Module"]);
        Assert.Equal(["Hot", "Module"], views.Load().ColumnArrangements[StoredModulesPage.TableId + "@headset"]);

        var root = TestSurface.MemoryFolder("d47-arranged-table-tests");
        var store = Store();
        CommanderGameState? State() => store.Active;
        var (checklists, _, _, ships, kit, _) = PlanBench.Over(root, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Classes.Add("headset");
        panel.EnableColumnArrangements(new ColumnArrangementMemory(views, headset: true));
        panel.EnableLoadout(ships, checklists, State, kit);

        var window = new Window { Content = panel, Width = 1280, Height = 1100 };
        window.Show();
        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(StoredModulesPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        var grids = Grids(window);
        Assert.NotEmpty(grids);

        foreach (var grid in grids)
        {
            Assert.Equal(["Hot", "Module"], Keys(grid));
        }

        window.Close();
    }

    private sealed record Line(string Name, int Size);

    private static readonly Line[] Lines = [new("Bravo", 2), new("Alpha", 3), new("Charlie", 1)];

    private static TableField<Line> Field(string key, string? label, Func<Line, string> text, SortColumn<Line>? sort = null) =>
        new(key, new TableColumn(key), row => new TextBlock { Text = text(row) }) { Label = label, Sort = sort };

    private static IReadOnlyList<TableField<Line>> LineFields =>
    [
        Field("Pick", null, _ => "o"),
        Field("Name", "Name", row => row.Name, SortColumn<Line>.Text("Name", row => row.Name)),
        Field("Size", "Size", row => row.Size.ToString(System.Globalization.CultureInfo.InvariantCulture), SortColumn<Line>.Figure("Size", row => (int?)row.Size, SortDirection.Descending)),
        Field("Market", "Market", _ => "m") with
        {
            More = [Field("Gold", "Gold", _ => "g"), Field("Silver", "Silver", _ => "s")],
        },
        Field("Go", null, _ => ">"),
    ];

    private static string Label(Visual root, string name)
    {
        var cell = root.GetVisualDescendants().OfType<Control>().First(control => control.Name == name);

        return cell is Button { Content: TextBlock label } ? label.Text! : ((TextBlock)cell).Text!;
    }

    [AvaloniaFact]
    public void AnEntryThatDrawsSeveralColumnsMovesThemTogether()
    {
        var memory = new ColumnArrangementMemory(ViewState());
        memory.Remember("Lines", ["Market", "Name"]);
        var table = new ArrangedTable<Line>("Lines", LineFields, memory, null);

        Assert.Equal(["Pick", "Market", "Gold", "Silver", "Name", "Go"], Keys(table.Header()));
        Assert.Equal(["Pick", "Market", "Name", "Go"], table.Shown.Select(field => field.Key));

        memory.Remember("Lines", ["Name", "Size", "Market"]);
        table.Refresh();

        Assert.Equal(["Pick", "Name", "Size", "Market", "Gold", "Silver", "Go"], Keys(table.Header()));
        Assert.Equal(
            ["o", "Alpha", "3", "m", "g", "s", ">"],
            table.Row(Lines[1]).Children.OfType<TextBlock>().OrderBy(Grid.GetColumn).Select(cell => cell.Text));
    }

    [AvaloniaFact]
    public void AMovedColumnKeepsItsSortAndALeftOutOneDropsIt()
    {
        var memory = new ColumnArrangementMemory(ViewState());
        var table = new ArrangedTable<Line>("Lines", LineFields, memory, null, sortedByDefault: "Name");
        var changed = 0;
        table.Changed += () => changed++;

        var header = table.Header();
        var window = Show(new StackPanel { Children = { header } });
        var size = header.Children.OfType<Button>().Single(button => button.Name == "LinesHead.Size");
        size.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, changed);
        Assert.Equal(["Alpha", "Bravo", "Charlie"], table.Sort(Lines).Select(line => line.Name));

        memory.Remember("Lines", ["Size", "Name", "Market"]);
        table.Refresh();
        var moved = new StackPanel { Children = { table.Header() } };
        window.Content = moved;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("▼", Label(moved, "LinesHead.Size"));
        Assert.DoesNotContain("▲", Label(moved, "LinesHead.Name"));
        Assert.Equal(["Alpha", "Bravo", "Charlie"], table.Sort(Lines).Select(line => line.Name));

        memory.Remember("Lines", ["Name", "Market"]);
        table.Refresh();
        var without = new StackPanel { Children = { table.Header() } };
        window.Content = without;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Bravo", "Alpha", "Charlie"], table.Sort(Lines).Select(line => line.Name));
        Assert.Contains("▲", Label(without, "LinesHead.Name"));
        window.Close();
    }

    [AvaloniaFact]
    public void TheSharedCellsReadAsTheTablesExpect()
    {
        Assert.Equal("Yes", TableCells.Flag(true).Text);
        Assert.Equal("No", TableCells.Flag(false).Text);
        Assert.Equal("—", TableCells.Text(null).Text);
        Assert.Equal("—", TableCells.Number(null).Text);
        Assert.Equal("1,250", TableCells.Number(1250).Text);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Right, TableCells.Number(5).HorizontalAlignment);

        var list = Assert.IsType<StackPanel>(TableCells.List(["a", "b", "c", "d", "e"]));
        Assert.Equal(["a · b · c", "+2"], list.Children.OfType<TextBlock>().Select(block => block.Text));
        Assert.Equal("a · b", Assert.IsType<TextBlock>(TableCells.List(["a", "b"])).Text);
    }
}
