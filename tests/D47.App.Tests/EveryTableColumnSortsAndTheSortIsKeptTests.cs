using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
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
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every labelled table column sorts, and a table's sort is kept between visits and sessions (#2575).</summary>
public sealed class EveryTableColumnSortsAndTheSortIsKeptTests
{
    private sealed record Row(string Name, int Size);

    private static ViewStateStore ViewState() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static TableField<Row> Field(string key, SortColumn<Row>? sort, string? label = "Label") =>
        new(key, new TableColumn(key), _ => new Border()) { Label = label, Sort = sort };

    [Fact]
    public void ASortStoredOnAStoreIsReadBackByANewMemoryOnTheSameStore()
    {
        var views = ViewState();

        new ColumnSortMemory(views).Remember("Table.One", ("Size", SortDirection.Descending));

        var again = new ColumnSortMemory(views);
        Assert.Equal(("Size", SortDirection.Descending), again.Remembered("Table.One"));
        Assert.Null(again.Remembered("Table.Two"));
    }

    [Fact]
    public void TheHeadsetsSortAndTheDesktopsAreStoredApart()
    {
        var views = ViewState();

        new ColumnSortMemory(views).Remember("Table.One", ("Name", SortDirection.Ascending));
        new ColumnSortMemory(views, headset: true).Remember("Table.One", ("Size", SortDirection.Descending));

        Assert.Equal(("Name", SortDirection.Ascending), new ColumnSortMemory(views).Remembered("Table.One"));
        Assert.Equal(("Size", SortDirection.Descending), new ColumnSortMemory(views, headset: true).Remembered("Table.One"));
    }

    [Fact]
    public void ReturningATableToItsDefaultOrderIsStoredAsNoSort()
    {
        var views = ViewState();
        var memory = new ColumnSortMemory(views);

        memory.Remember("Table.One", ("Name", SortDirection.Ascending));
        memory.Remember("Table.One", null);

        Assert.Null(new ColumnSortMemory(views).Remembered("Table.One"));
        Assert.Empty(views.Load().ColumnSorts);
    }

    [AvaloniaFact]
    public void ALabelledFieldWithNoSortStopsTheTableBeingBuilt()
    {
        using var look = AppLook.Put();

        var thrown = Assert.Throws<ArgumentException>(() => new ArrangedTable<Row>(
            "Test.Table",
            [Field("Name", SortColumn<Row>.Text("Name", row => row.Name), "Name"), Field("Size", null, "Size")],
            null,
            null));

        Assert.Contains("Test.Table", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Size", thrown.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ALabelledFieldWhoseSortDoesNotPressStopsTheTableBeingBuilt()
    {
        using var look = AppLook.Put();

        var thrown = Assert.Throws<ArgumentException>(() => new ArrangedTable<Row>(
            "Test.Table", [Field("Size", SortColumn<Row>.Plain("Size"), "Size")], null, null));

        Assert.Contains("Size", thrown.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AControlColumnNeedsNoSort()
    {
        using var look = AppLook.Put();

        var table = new ArrangedTable<Row>(
            "Test.Table",
            [Field("Name", SortColumn<Row>.Text("Name", row => row.Name), "Name"), Field("Open", null, null)],
            null,
            null);

        Assert.Equal(2, table.Shown.Count);
    }

    [AvaloniaFact]
    public void ATableWithAStoredSortStartsSortedByItAndStoresEachPress()
    {
        using var look = AppLook.Put();
        var views = ViewState();
        var memory = new ColumnArrangementMemory(views);
        IReadOnlyList<TableField<Row>> fields =
        [
            Field("Name", SortColumn<Row>.Text("Name", row => row.Name), "Name"),
            Field("Size", SortColumn<Row>.Figure<int>("Size", row => row.Size, SortDirection.Descending), "Size"),
        ];

        var first = new ArrangedTable<Row>("Test.Table", fields, memory, null);
        Assert.Null(first.SortedBy);

        first.Header();
        var cell = Assert.IsType<Button>(first.Header().Children.OfType<Control>().Last());
        cell.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(("Size", SortDirection.Descending), first.SortedBy);

        var second = new ArrangedTable<Row>("Test.Table", fields, new ColumnArrangementMemory(views), null);
        Assert.Equal(("Size", SortDirection.Descending), second.SortedBy);

        var rows = new Row[] { new("a", 1), new("b", 3), new("c", 2) };
        Assert.Equal(["b", "c", "a"], second.Sort(rows).Select(row => row.Name));

        Assert.Equal(("Size", SortDirection.Descending), new ColumnSortMemory(views).Remembered("Test.Table"));

        var headset = new ArrangedTable<Row>("Test.Table", fields, new ColumnArrangementMemory(views, headset: true), null);
        Assert.Null(headset.SortedBy);
    }

    [AvaloniaFact]
    public void ASortOnAColumnThatNoLongerExistsFallsBackToTheDefaultOrder()
    {
        using var look = AppLook.Put();
        var views = ViewState();
        new ColumnSortMemory(views).Remember("Test.Table", ("Gone", SortDirection.Ascending));

        var table = new ArrangedTable<Row>(
            "Test.Table",
            [Field("Name", SortColumn<Row>.Text("Name", row => row.Name), "Name")],
            new ColumnArrangementMemory(views),
            null);

        Assert.Null(table.SortedBy);
        Assert.Equal(["b", "a"], table.Sort([new Row("b", 0), new Row("a", 0)]).Select(row => row.Name));
    }

    // The panel tests below build real pages.

    private const string Id = StoredModulesPage.TableId;

    private static GameStateStore Store()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
        {
            """{"timestamp":"2026-10-04T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
            """{"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[{"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},{"Name":"$int_powerdistributor_size5_class5_name;","Name_Localised":"Power Distributor","StorageSlot":3,"BuyPrice":3475688,"Hot":true},{"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":4,"BuyPrice":1178420,"Hot":false}]}""",
        })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

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

    private static void Press(Visual root, string name)
    {
        var button = root.GetVisualDescendants().OfType<Button>().First(control => control.Name == name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Modules(Visual root)
    {
        var rows = root.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == Id && !grid.IsHeader).ToList();
        var at = rows[0].Layout.Columns.ToList().FindIndex(column => column.Key == "Module");

        return [.. rows.Select(row => row.Children.OfType<TextBlock>().Single(cell => Grid.GetColumn(cell) == at).Text!)];
    }

    [AvaloniaFact]
    public void ATableOpenedAgainOnTheSameStoreIsStillSortedAndOneOpenedOnTheHeadsetsIsNot()
    {
        var views = ViewState();
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views)));

        Press(window, "StoredHead.Class");
        Assert.Equal(["Cargo Rack", "Shield Generator", "Power Distributor"], Modules(window));
        window.Close();

        var again = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views)));
        Assert.Equal(["Cargo Rack", "Shield Generator", "Power Distributor"], Modules(again));
        Assert.Contains(again.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "CLASS ▼");
        again.Close();

        var headset = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views, headset: true)));
        Assert.Equal(["Cargo Rack", "Power Distributor", "Shield Generator"], Modules(headset));
        headset.Close();
    }

    [AvaloniaFact]
    public void ATableReturnedToItsDefaultOrderIsInThatOrderWhenOpenedAgain()
    {
        var views = ViewState();
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views)));

        Press(window, "StoredHead.Class");
        Press(window, "StoredHead.Class");
        Press(window, "StoredHead.Class");
        window.Close();

        Assert.Empty(views.Load().ColumnSorts);

        var again = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views)));
        Assert.Equal(["Cargo Rack", "Power Distributor", "Shield Generator"], Modules(again));
        again.Close();
    }

    private static List<string> StationNames(Control root) =>
        [.. root.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains(StarSystemPage.StationRowClass) && AutomationProperties.GetName(border) is { Length: > 0 })
            .Select(border => AutomationProperties.GetName(border)!)];

    private static List<string> FactionNames(Control root) =>
        [.. root.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Child is Grid { ColumnDefinitions.Count: 6 } && border.BorderThickness.Left == 3)
            .Select(border => border.GetVisualDescendants().OfType<TextBlock>().First().Text!)];

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void TheStationsTableSortedByAnAddedColumnIsStillSortedWhenThePageIsAttachedAgain()
    {
        var profile = TheSystemPageListsItsStationsTests.Ltt7786Profile();
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown(arrangements: new ColumnArrangementMemory(ViewState()));
        TheSystemPageListsItsStationsTests.Section(panel, "Stations");

        Press(panel, ColumnArranger.NameOf(StarSystemPage.StationsTable, "Tile"));
        Press(panel, ColumnArranger.NameOf(StarSystemPage.StationsTable, "Add"));
        var box = panel.GetVisualDescendants().OfType<CheckBox>().First(control => control.Name == ColumnArranger.NameOf(StarSystemPage.StationsTable, "Show.Services"));
        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Press(panel, ColumnArranger.NameOf(StarSystemPage.StationsTable, "Done"));

        Press(panel, "StationHead.Services");

        List<int> Counts() => [.. StationNames(panel).Select(name => profile.Stations.First(station => station.Name == name).Services.Count)];

        var sorted = Counts();
        Assert.True(sorted.Count > 1);
        Assert.Equal(sorted.OrderByDescending(count => count), sorted);
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "SERVICES ▼");

        var page = panel.GetVisualDescendants().OfType<StarSystemPage>().Single();
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        Assert.False(page.IsAttachedToVisualTree());

        window.Content = panel;
        Dispatcher.UIThread.RunJobs();
        Assert.True(page.IsAttachedToVisualTree());
        TheSystemPageListsItsStationsTests.Section(panel, "Stations");

        Assert.Equal(sorted, Counts());
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "SERVICES ▼");

        window.Close();
    }

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void TheMinorFactionsTableIsStillSortedWhenAnotherSystemOpens()
    {
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown(arrangements: new ColumnArrangementMemory(ViewState()));
        var page = panel.GetVisualDescendants().OfType<StarSystemPage>().Single();
        var byInfluence = FactionNames(panel);
        Assert.True(byInfluence.Count > 1);

        Press(panel, "FactionHead.Faction");
        var named = FactionNames(panel);
        Assert.Equal(byInfluence.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase), named);

        Assert.True(page.OpenSystem(1234567890));
        Dispatcher.UIThread.RunJobs();
        UiWait.Until(() => FactionNames(panel).Count == named.Count);

        Assert.Equal(named, FactionNames(panel));
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "FACTION ▲");

        window.Close();
    }
}
