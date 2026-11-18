using Avalonia;
using Avalonia.Automation;
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
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>The System page's Stations, Powerplay, Factions and Market tables take the COLUMNS control and keep what it stores (#2519).</summary>
[Trait("Category", "Integration")]
public sealed class TheSystemPageTablesAreArrangedTests
{
    private const long Ltt7786 = 633608311522;

    private const long Market = 3700000000;

    private static readonly DateTimeOffset At = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly PowerplayNeighbourhood Neighbours = new(
        2,
        [
            new PowerplayNeighbour("Alioth", 9.5, "Zachary Hudson", "Fortified", 0.4, ["Zachary Hudson", "Li Yong-Rui"]),
            new PowerplayNeighbour("Sol", 14, "Jerome Archer", "Exploited", 0.1, ["Jerome Archer"]),
        ]);

    private static readonly StarSystemProfile Station = new()
    {
        Name = "LTT 7786",
        SystemAddress = Ltt7786,
        Stations =
        [
            new StationProfile
            {
                Name = "Smith Port",
                MarketId = Market,
                Kind = StationKind.Starport,
                Type = "Orbis Starport",
                Market = new StationMarket(
                    [
                        new MarketCommodity("Gold", "gold", "Metals", 9000, 0, 12, 0),
                        new MarketCommodity("Algae", "algae", "Foods", 100, 80, 500, 40),
                    ],
                    At),
            },
        ],
    };

    private sealed class Systems(StarSystemProfile profile) : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(profile);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(Neighbours);
    }

    private sealed record Table(string Id, string Head, string[] Defaults, string Remove, string? Add, string Move, Func<ColumnArrangementMemory, (Control Root, Window Window)> Open);

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static (Control Root, Window Window) OnPage(ColumnArrangementMemory memory, string section)
    {
        var systems = new Systems(TheSystemPageListsItsStationsTests.Ltt7786Profile());
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableSearch();
        panel.EnableColumnArrangements(memory);
        panel.EnableStarSystem(new StarSystemSurface(
            systems,
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Ltt7786 }));

        var window = new Window { Content = panel, Width = 1280, Height = 1180 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        Until(() => Shows(panel, "LAST REPORTED"));

        if (section == "Powerplay")
        {
            TheSystemPageListsItsStationsTests.Section(panel, section);
            Until(() => !Shows(panel, "Asking for Powerplay systems near LTT 7786."));
        }
        else if (section != "Overview")
        {
            TheSystemPageListsItsStationsTests.Section(panel, section);
        }

        return (panel, window);
    }

    private static (Control Root, Window Window) OnMarket(ColumnArrangementMemory memory)
    {
        var page = new SystemStationPage(
            new StarSystemSurface(new Systems(Station), () => null), Ltt7786, Market, "Smith Port", Station)
        {
            ColumnArrangements = memory,
        };
        page.Show(SystemStationPage.Section.Market);

        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (page, window);
    }

    private static readonly Table[] Tables =
    [
        new(
            StarSystemPage.StationsTable,
            "StationHead",
            ["Station", "Type", "Faction", "Government", "Economy", "Pad", "Arrival", "More"],
            "Government",
            "Market",
            "Pad",
            memory => OnPage(memory, "Stations")),
        new(
            StarSystemPage.PowerplayTable,
            "NeighbourHead",
            ["System", "Distance", "State", "Power", "Progress"],
            "State",
            "Powers",
            "Progress",
            memory => OnPage(memory, "Powerplay")),
        new(
            StarSystemPage.FactionsTable,
            "FactionHead",
            ["Faction", "Government", "Allegiance", "Influence", "Active", "Pending"],
            "Allegiance",
            null,
            "Influence",
            memory => OnPage(memory, "Overview")),
        new(
            SystemStationPage.MarketTable,
            "MarketHead",
            ["Commodity", "Category", "Buy", "Sell", "Supply", "Demand"],
            "Category",
            null,
            "Demand",
            OnMarket),
    ];

    public static TheoryData<string, bool> EveryTable()
    {
        var data = new TheoryData<string, bool>();

        foreach (var headset in new[] { false, true })
        {
            foreach (var table in Tables)
            {
                data.Add(table.Id, headset);
            }
        }

        return data;
    }

    private static bool Shows(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(block => block.IsEffectivelyVisible && block.Text == text);

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

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void WithNothingStoredEachTableDrawsItsOwnColumnsAndOffersTheColumnsTile(string id, bool headset)
    {
        var table = Tables.Single(known => known.Id == id);
        var (root, window) = table.Open(new ColumnArrangementMemory(Views(), headset));

        Assert.Equal(table.Defaults, Keys(root, id));
        Assert.NotNull(Named<Button>(root, ColumnArranger.NameOf(id, "Tile")));

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void AnAddedMovedAndRemovedColumnIsKeptWhenThePageOpensAgain(string id, bool headset)
    {
        var views = Views();
        var table = Tables.Single(known => known.Id == id);
        var (first, firstWindow) = table.Open(new ColumnArrangementMemory(views, headset));

        Press(first, ColumnArranger.NameOf(id, "Tile"));
        Press(first, $"{table.Head}.Choose.{table.Remove}");
        Press(first, ColumnArranger.NameOf(id, "Remove"));
        Press(first, $"{table.Head}.Choose.{table.Move}");
        Press(first, ColumnArranger.NameOf(id, "MoveLeft"));

        if (table.Add is { } add)
        {
            Press(first, ColumnArranger.NameOf(id, "Add"));
            Named<CheckBox>(first, ColumnArranger.NameOf(id, $"Show.{add}")).IsChecked = true;
            Dispatcher.UIThread.RunJobs();
        }

        Press(first, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(first, id);
        Assert.DoesNotContain(table.Remove, arranged);
        Assert.True(Array.IndexOf(arranged, table.Move) < Array.IndexOf(table.Defaults, table.Move));

        if (table.Add is { } added)
        {
            Assert.Contains(added, arranged);
        }

        firstWindow.Close();

        var (again, againWindow) = table.Open(new ColumnArrangementMemory(views, headset));
        Assert.Equal(arranged, Keys(again, id));

        Press(again, ColumnArranger.NameOf(id, "Tile"));
        Press(again, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(table.Defaults, Keys(again, id));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void TheStationsArrowStaysAtTheRightWhateverIsArranged()
    {
        var table = Tables[0];
        var (root, window) = table.Open(new ColumnArrangementMemory(Views()));

        Press(root, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(root, ColumnArranger.NameOf(table.Id, "Add"));
        Named<CheckBox>(root, ColumnArranger.NameOf(table.Id, "Show.Services")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Press(root, ColumnArranger.NameOf(table.Id, "MoveLeft"));

        Assert.Equal("More", Keys(root, table.Id)[^1]);

        window.Close();
    }

    [AvaloniaFact]
    public void AStationsAddedColumnsReadAsTheSharedCellsDo()
    {
        var table = Tables[0];
        var (root, window) = table.Open(new ColumnArrangementMemory(Views()));

        Press(root, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(root, ColumnArranger.NameOf(table.Id, "Add"));

        foreach (var key in new[] { "Market", "Outfitting", "Shipyard", "Pads", "Economies", "Services", "DockingAccess", "Body", "Allegiance", "FactionState", "Updated" })
        {
            Named<CheckBox>(root, ColumnArranger.NameOf(table.Id, $"Show.{key}")).IsChecked = true;
        }

        Dispatcher.UIThread.RunJobs();
        Press(root, ColumnArranger.NameOf(table.Id, "Done"));

        var texts = root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();
        Assert.Contains("No", texts);
        Assert.Contains(TableCells.Dash, texts);
        Assert.Contains(texts, text => text is not null && text.StartsWith("+", StringComparison.Ordinal));
        Assert.Contains(texts, text => text is not null && text.StartsWith("S ", StringComparison.Ordinal) && text.Contains(" · M ", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void ASortFromAHeaderFollowsItsColumnWhenItMoves()
    {
        var table = Tables[0];
        var (root, window) = table.Open(new ColumnArrangementMemory(Views()));

        List<string> Rows() =>
            [.. root.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains(StarSystemPage.StationRowClass) && AutomationProperties.GetName(border) is { Length: > 0 })
                .Select(border => AutomationProperties.GetName(border)!)];

        Press(root, "StationHead.Pad");
        var sorted = Rows();
        Assert.Contains(root.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "PAD ▼");

        Press(root, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(root, "StationHead.Choose.Pad");
        Press(root, ColumnArranger.NameOf(table.Id, "MoveLeft"));
        Press(root, ColumnArranger.NameOf(table.Id, "MoveLeft"));
        Press(root, ColumnArranger.NameOf(table.Id, "Done"));

        Assert.Equal(sorted, Rows());
        Assert.Contains(root.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "PAD ▼");
        Assert.True(Array.IndexOf(Keys(root, table.Id), "Pad") < Array.IndexOf(table.Defaults, "Pad"));

        window.Close();
    }
}
