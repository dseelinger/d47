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
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Best cargo, Market and Construction needs take the COLUMNS control and keep what it stores (#2526).</summary>
[Trait("Category", "Integration")]
public sealed class TheNavigationTablesAreArrangedTests
{
    private const string CommanderLine = """{"timestamp":"2026-10-03T19:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private static readonly DateTimeOffset Now = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

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

    private static void Add(Visual root, string id, string key)
    {
        if (root.GetVisualDescendants().OfType<CheckBox>().All(box => box.Name != ColumnArranger.NameOf(id, $"Show.{key}")))
        {
            Press(root, ColumnArranger.NameOf(id, "Add"));
        }

        Named<CheckBox>(root, ColumnArranger.NameOf(id, $"Show.{key}")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static string Id(string table) => ColumnArranger.NameOf(table, "Tile");

    // Best cargo.

    private static readonly BestCargoAnswer Picks = new(
    [
        new CargoPick("Superconductors", "Ray Gateway", 9_412, 256, CargoLimit.Hold),
        new CargoPick("Power Generators", "Ray Gateway", 4_880, 256, CargoLimit.Hold),
        new CargoPick("Robotics", "Fraser Hub", 3_210, 180, CargoLimit.Supply),
        new CargoPick("Aluminium", "Ray Gateway", 410, 256, CargoLimit.Hold),
    ], 256);

    private static (Window Window, RouteBestCargoPage Page) OpenBestCargo(ViewStateStore views)
    {
        var board = new BestCargoBoard();
        board.Post(new BestCargoPosting(
            new BestCargoSearch { System = "LTT 7786", Station = "Parise Gateway", Destination = "Sol" }, Picks, Now));

        var state = JournalScript.State(
            CommanderLine,
            """{"timestamp":"2026-10-03T19:00:01Z","event":"Loadout","Ship":"type9","CargoCapacity":256}""",
            """{"timestamp":"2026-10-03T19:00:02Z","event":"Docked","StarSystem":"LTT 7786","StationName":"Parise Gateway"}""");

        return PageHost.Open(() => new RouteBestCargoPage(
            CapabilityRegistry.Build([]),
            board,
            () => state,
            () => NavRoute.None,
            columnArrangements: new ColumnArrangementMemory(views)));
    }

    [AvaloniaFact]
    public void BestCargoDrawsItsColumnsAndAMovedColumnIsKeptWithTheSortOnIt()
    {
        var views = Views();
        var (window, page) = OpenBestCargo(views);
        var id = RouteBestCargoPage.TableId;

        Assert.Equal(["Rank", "Commodity", "SellAt", "Profit", "Tonnes", "Total", "Bar"], Keys(page, id));
        Assert.Equal("Superconductors", page.Rows[0].Commodity);

        Press(page, "BestCargoHead.Profit/t");
        Press(page, "BestCargoHead.Profit/t");
        Assert.Equal("Aluminium", page.Rows[0].Commodity);

        Press(page, Id(id));
        Press(page, "BestCargoHead.Choose.Profit");
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(page, id);
        Assert.Equal(["Rank", "Profit", "Commodity", "SellAt", "Tonnes", "Total", "Bar"], arranged);
        Assert.Equal("Aluminium", page.Rows[0].Commodity);
        window.Close();

        var (againWindow, again) = OpenBestCargo(views);
        Assert.Equal(arranged, Keys(again, id));

        Press(again, Id(id));
        Press(again, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(["Rank", "Commodity", "SellAt", "Profit", "Tonnes", "Total", "Bar"], Keys(again, id));
        againWindow.Close();
    }

    // Construction needs.

    private static (Window Window, ConstructionPage Page) OpenConstruction(ViewStateStore views, bool headset = false)
    {
        var state = JournalScript.Timed(
            Now,
            "F735466",
            "John Deparagon",
            [
                (TimeSpan.FromHours(-2), """{"event":"Docked","StationName":"Vogel Relay","StationType":"SpaceConstructionDepot","StarSystem":"HIP 47126","MarketID":3900000001}"""),
                (TimeSpan.FromHours(-2), """{"event":"ColonisationConstructionDepot","MarketID":3900000001,"ConstructionProgress":0.62,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":20000,"ProvidedAmount":5180,"Payment":1000},{"Name":"$titanium_name;","Name_Localised":"Titanium","RequiredAmount":1150,"ProvidedAmount":0,"Payment":1000}]}"""),
            ]);

        return PageHost.Open(() => new ConstructionPage(
            new ConstructionSurface(() => state, () => Now, null, new SourcingBoard()),
            headset: headset,
            columnArrangements: new ColumnArrangementMemory(views, headset)));
    }

    [AvaloniaFact]
    public void ConstructionNeedsAddsRequiredAndDeliveredAndKeepsThem()
    {
        var views = Views();
        var (window, page) = OpenConstruction(views);
        var id = ConstructionPage.NeedsTable;

        Assert.Equal(["Commodity", "Remaining", "InHold", "OnCarrier", "ToBuy", "Order"], Keys(page, id));

        Press(page, Id(id));
        Add(page, id, "Required");
        Add(page, id, "Delivered");
        Press(page, "NeedsHead.Choose.Order");
        Press(page, ColumnArranger.NameOf(id, "Remove"));
        Press(page, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(page, id);
        Assert.Equal(["Commodity", "Remaining", "InHold", "OnCarrier", "ToBuy", "Required", "Delivered"], arranged);

        var cells = page.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("20,000", cells);
        Assert.Contains("5,180", cells);
        window.Close();

        var (againWindow, again) = OpenConstruction(views);
        Assert.Equal(arranged, Keys(again, id));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void AConstructionSortFollowsItsColumnWhenItMoves()
    {
        var (window, page) = OpenConstruction(Views());
        var id = ConstructionPage.NeedsTable;

        bool Sorted() =>
            page.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text is { } said && said.StartsWith("TO BUY", StringComparison.Ordinal) && said.EndsWith('▼'));

        Press(page, "NeedsHead.To" + "buy");
        Assert.True(Sorted());

        Press(page, Id(id));
        Press(page, "NeedsHead.Choose.ToBuy");
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(id, "Done"));

        Assert.Equal("ToBuy", Keys(page, id)[3]);
        Assert.True(Sorted());
        window.Close();
    }

    // Market.

    private static MarketSnapshot Station(string station, string system, double x, int price) => new()
    {
        Station = station,
        System = system,
        X = x,
        Type = "Coriolis Starport",
        HasLargePad = true,
        DistanceToArrival = 1_234,
        ControllingPower = "Aisling Duval",
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gold"] = new("Gold") { BuyPrice = price, SellPrice = price, Supply = 1_200, Demand = 900 },
            ["Silver"] = new("Silver") { BuyPrice = price / 2, SellPrice = price / 2, Supply = 340, Demand = 260 },
        },
    };

    private sealed class Trade : ITradePlanService
    {
        public List<CommodityQuery> Asked { get; } = [];

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) => Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken)
        {
            Asked.Add(search.Query);
            return Task.FromResult(new CommodityAnswer([], 0, 0, true));
        }

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) => Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) => Task.FromResult<BestCargoAnswer?>(null);
    }

    private sealed class NoGalaxy : StubGalaxyService
    {
    }

    private static (PanelView Panel, Window Window, Trade Trade) OpenMarket(ViewStateStore views, CommodityQuery query)
    {
        var settings = TestSurface.Settings();
        var trade = new Trade();
        var board = new CommodityBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new NoGalaxy(), () => "Sol", settings, trade, board: board)]);

        board.Post(new CommodityPosting(
            query,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(query, [Station("Ohm Port", "Ohm", 12, 9_412), Station("Galileo", "Sol", 0, 9_480)], Station("Home", "Sol", 0, 0)),
                2,
                DroppedAsStale: 0,
                OriginKnown: true),
            "Sol",
            DateTimeOffset.UtcNow));

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableColumnArrangements(new ColumnArrangementMemory(views));

        panel.EnableRouting(
            new RoutingSurface(() => new NavRoute(), () => "Sol", registry, Plans: null, Commodities: board, Clipboard: new RecordingClipboard()),
            plan: false,
            progress: false);

        var window = new Window { Content = panel, Width = 1100, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Astrometrics;
        Dispatcher.UIThread.RunJobs();

        return (panel, window, trade);
    }

    private static CommodityQuery One => new("Gold", MaxDistance: 100);

    private static CommodityQuery Two => new("Gold", MaxDistance: 100, More: [new("Silver", null)]);

    [AvaloniaFact]
    public void MarketOffersFiveMoreColumnsAndKeepsTheOnesAdded()
    {
        var views = Views();
        var (panel, window, _) = OpenMarket(views, One);
        var id = RouteMarketPage.TableId;

        Assert.Equal(["Station", "Distance", "Price", "Stock", "Priced"], Keys(panel, id));

        Press(panel, Id(id));
        Add(panel, id, "FromStar");
        Add(panel, id, "LargePad");
        Add(panel, id, "Surface");
        Add(panel, id, "Power");
        Add(panel, id, "PowerState");
        Press(panel, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(panel, id);
        Assert.Equal(["Station", "Distance", "Price", "Stock", "Priced", "FromStar", "LargePad", "Surface", "Power", "PowerState"], arranged);

        var cells = panel.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("1,234 LS", cells);
        Assert.Contains("Aisling Duval", cells);
        window.Close();

        var (againPanel, againWindow, _) = OpenMarket(views, One);
        Assert.Equal(arranged, Keys(againPanel, id));

        Press(againPanel, Id(id));
        Press(againPanel, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(["Station", "Distance", "Price", "Stock", "Priced"], Keys(againPanel, id));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void AMovedDistanceColumnStillAsksForTheNearest()
    {
        var (panel, window, trade) = OpenMarket(Views(), One);
        var id = RouteMarketPage.TableId;

        Press(panel, Id(id));
        Press(panel, "MarketHead.Choose.Distance");
        Press(panel, ColumnArranger.NameOf(id, "MoveRight"));
        Press(panel, ColumnArranger.NameOf(id, "MoveRight"));
        Press(panel, ColumnArranger.NameOf(id, "Done"));

        Assert.Equal(["Station", "Price", "Stock", "Distance", "Priced"], Keys(panel, id));

        Press(panel, "MarketHead.Distance");

        Assert.Equal(CommodityOrder.Distance, trade.Asked[^1].OrderBy);
        Assert.False(trade.Asked[^1].Descending);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCommodityColumnsMoveAsOneAndCannotBeRemoved()
    {
        var views = Views();
        var (panel, window, _) = OpenMarket(views, Two);
        var id = RouteMarketPage.TableId;

        Assert.Equal(["Station", "Distance", "Gold", "Silver", "Total", "Priced"], Keys(panel, id));

        Press(panel, Id(id));
        Press(panel, "MarketHead.Choose.Commodities");

        Assert.False(Named<Button>(panel, ColumnArranger.NameOf(id, "Remove")).IsEffectivelyEnabled);

        Press(panel, ColumnArranger.NameOf(id, "MoveRight"));
        Press(panel, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(panel, id);
        Assert.Equal(["Station", "Distance", "Total", "Gold", "Silver", "Priced"], arranged);
        window.Close();

        var (againPanel, againWindow, _) = OpenMarket(views, Two);
        Assert.Equal(arranged, Keys(againPanel, id));
        againWindow.Close();
    }
}
