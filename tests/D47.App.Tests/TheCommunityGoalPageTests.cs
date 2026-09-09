using Microsoft.Extensions.Logging.Abstractions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Community Goal page on the Routing tab, and that it survives its own rebuild.</summary>
public class TheCommunityGoalPageTests
{
    private const string Fid = "F1234";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null ? parsed : throw new InvalidOperationException(json);

 /// <summary>d47's own default boundary, the way <c>MainWindow</c> wires it from settings.</summary>
    private static LedgerWindow Week(DateTimeOffset at) => CommodityLedger.Week(at, DayOfWeek.Thursday, 7);

    private static CommodityLedger Ledger()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1 }"""),
            Event($$"""{ "timestamp":"{{Noon.AddMinutes(5):yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":2, "Type":"palladium", "Count":100, "SellPrice":51000, "TotalSale":5100000, "AvgPricePaid":48200 }"""),
        ]);

        return ledger;
    }

    private static MarketSnapshot Market(string station, double x, int supply, double arrival) => new()
    {
        Station = station,
        System = station + " system",
        X = x,
        Type = "Orbis Starport",
        HasLargePad = true,
        DistanceToArrival = arrival,
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Palladium"] = new("Palladium") { BuyPrice = 51_000, Supply = supply },
        },
    };

    private static CommodityBoard Board(params MarketSnapshot[] markets) =>
        Board(CommunityGoalSearch.Tag, markets);

    private static CommodityBoard Board(string tag, params MarketSnapshot[] markets)
    {
        var board = new CommodityBoard();
        var query = new CommodityQuery(
            "Palladium",
            MaxDistance: 250,
            OrderBy: CommodityOrder.Distance,
            Limit: 10,
            Tag: tag);

        board.Post(new CommodityPosting(
            query,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(query, markets, new MarketSnapshot { Station = "Ega", System = "Ega" }),
                markets.Length,
                DroppedAsStale: 0,
                OriginKnown: true),
            "Ega",
            DateTimeOffset.UtcNow));

        return board;
    }

    private static PanelView Furnished(
        CommodityBoard board,
        CommodityLedger ledger,
        bool lookups = true,
        double width = 1000)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

 // Refresh() is gated on the page actually showing; the real window ties this to nav state, and
        // these tests stand in for that by declaring the page always up.
        var search = new CommunityGoalSearch { Showing = () => true };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Ega",
                D47.Core.Capabilities.CapabilityRegistry.Build([]),
                Plans: null,
                LookupsEnabled: () => lookups,
                OpenSettings: null,
                Commodities: board,
                CommunityGoal: new CommunityGoalSurface(search, ledger, () => Fid, () => Noon.AddMinutes(10), Week)),
            plan: false,
            progress: false,
            course: false,
            market: false);

        var window = new Window { Content = panel, Width = width, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        window.Measure(new Avalonia.Size(width, 700));
        window.Arrange(new Avalonia.Rect(0, 0, width, 700));
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static string[] TextOf(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    [AvaloniaFact]
    public void TheCommunityGoalRootIsFurnishedWithItsHelp()
    {
        var panel = Furnished(new CommodityBoard(), new CommodityLedger());

        Assert.Equal("Community Goal", panel.Nav.Root.Word);
        Assert.Equal(D47.Core.Capabilities.Builtin.CommunityGoalCapability.Id, panel.Nav.Root.Help);
    }

    [AvaloniaFact]
    public void ItDrawsTheAnswerNearestFirstWithTheStationDistance()
    {
        var panel = Furnished(Board(Market("Far", 90, 50_000, 40), Market("Near", 12, 12_000, 210)), new CommodityLedger());

        var drawn = TextOf(panel);

        Assert.Contains("Near", drawn);
        Assert.Contains("210 Ls", drawn);
        Assert.Contains("12,000", drawn);
        Assert.Contains("From star", drawn);
        Assert.True(Array.IndexOf(drawn, "Near") < Array.IndexOf(drawn, "Far"));
    }

    [AvaloniaFact]
    public void ALongProceduralSystemNameIsNotTruncated()
    {
        // The System column was fixed at 130px, which cut a real procedural name down to "Scorpii Sector
 // OD-...". 230px gives an ordinary name room to sit whole.
        const string longName = "Scorpii Sector FG-Y d7-23";

        var market = Market("Outpost", 12, 12_000, 210) with { System = longName };
        var panel = Furnished(Board(market), new CommodityLedger());

        Assert.Contains(longName, TextOf(panel));
    }

    [AvaloniaFact]
    public void EachResultRowCarriesACopyButtonForItsSystem()
    {
        var panel = Furnished(Board(Market("Near", 12, 12_000, 210)), new CommodityLedger());

        var copyButtons = panel.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => Equals(
                Avalonia.Automation.AutomationProperties.GetName(button), "Copy Near system"))
            .ToList();

        Assert.Single(copyButtons);
    }

    [AvaloniaFact]
    public void APostingFromAnywhereElseIsNotDrawnAsIfItWereThisSearch()
    {
        // CommodityBoard is one shared slot, written by the Market page's own Find and by a spoken market
        // question too — neither carries this search's tag, so neither should be drawn here under this page's
 // own heading.
        var board = new CommodityBoard();
        var query = new CommodityQuery("Gold", Side: TradeSide.Selling);

        board.Post(new CommodityPosting(
            query,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(query, [Market("Elsewhere", 12, 12_000, 210)], null),
                1,
                DroppedAsStale: 0,
                OriginKnown: true),
            "Ega",
            DateTimeOffset.UtcNow));

        var panel = Furnished(board, new CommodityLedger());

        Assert.DoesNotContain("Elsewhere", TextOf(panel));
    }

    [AvaloniaFact]
    public void TheHeadingNamesTheOrderTheAnswerWasActuallyRankedBy()
    {
        var board = new CommodityBoard();
        var query = new CommodityQuery(
            "Palladium", OrderBy: CommodityOrder.Price, Tag: CommunityGoalSearch.Tag, Limit: 10);

        board.Post(new CommodityPosting(
            query,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(query, [Market("Cheap", 12, 12_000, 210)], null),
                1,
                DroppedAsStale: 0,
                OriginKnown: true),
            "Ega",
            DateTimeOffset.UtcNow));

        var panel = Furnished(board, new CommodityLedger());

        Assert.Contains("Palladium near Ega, the goal's system, best price first", TextOf(panel));
    }

    /// <summary>The heading has to say whose system it measured from: a system name on its own does not say whether it is the goal's or the ship's, and those differ.</summary>
    [AvaloniaFact]
    public void TheHeadingSaysWhoseSystemItMeasuredFrom()
    {
        var panel = Furnished(Board(Market("Near", 12, 12_000, 210)), new CommodityLedger());

        Assert.Contains("Palladium near Ega, the goal's system, nearest first", TextOf(panel));
    }

    /// <summary>
    /// The same page draws the from-the-ship answer, which is the other tag this search posts under —
    /// the one it falls back to with no live goal, and the one "cg search from here" asks for on
    /// purpose.
    /// </summary>
    [AvaloniaFact]
    public void ItDrawsTheAnswerAskedFromTheShipAndSaysThatIsWhatItIs()
    {
        var panel = Furnished(
            Board(CommunityGoalSearch.ShipTag, Market("Near", 12, 12_000, 210)), new CommodityLedger());

        Assert.Contains("Palladium near Ega, your ship's system, nearest first", TextOf(panel));
    }

    [AvaloniaFact]
    public void ItDrawsTheLedgerNetOfCost()
    {
        var panel = Furnished(new CommodityBoard(), Ledger());

        var drawn = TextOf(panel);

        Assert.Contains("This session", drawn);
        Assert.Contains("Today", drawn);
        Assert.Contains("This week", drawn);

 // Three rows only: no fourth, goal-named row, even though none of the sales in this fixture
        // come from a live goal to begin with.
        Assert.DoesNotContain("This goal", drawn);
        Assert.DoesNotContain("no goal running", drawn);

        Assert.Equal(3, drawn.Count(text => text == "+280,000 cr"));
        Assert.Contains(drawn, text => text.StartsWith("Last sale: 100 tonnes at 51,000 cr, paid 48,200 cr each, +280,000 cr", StringComparison.Ordinal));
    }

    /// <summary>The no-goal fixture proves nothing about a live goal: this one folds a live <c>CommunityGoal</c> sighting before the sale, so the three-row draw is asserted against a state that could grow a fourth.</summary>
    [AvaloniaFact]
    public void WithALiveGoalThePageStillDrawsOnlyThreeRows()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            Event($$"""{ "timestamp":"{{Noon.AddDays(-1):yyyy-MM-ddTHH:mm:ssZ}}", "event":"CommunityGoal", "CurrentGoals":[ { "CGID":901, "Title":"Palladium Drive", "SystemName":"Ega", "MarketName":"Port", "Expiry":"{{Noon.AddDays(6):yyyy-MM-ddTHH:mm:ssZ}}", "IsComplete":false } ] }"""),
            Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1 }"""),
            Event($$"""{ "timestamp":"{{Noon.AddMinutes(5):yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":2, "Type":"palladium", "Count":100, "SellPrice":51000, "TotalSale":5100000, "AvgPricePaid":48200 }"""),
        ]);

        var panel = Furnished(new CommodityBoard(), ledger);

        var drawn = TextOf(panel);

        Assert.Contains("This session", drawn);
        Assert.Contains("Today", drawn);
        Assert.Contains("This week", drawn);
        Assert.DoesNotContain("This goal", drawn);
        Assert.DoesNotContain("Palladium Drive", drawn);
    }

    [AvaloniaFact]
    public void AFailedRunClearsTheStaleTableRatherThanLeavingItUp()
    {
        // #318: a failed run left _board.Last exactly as it was, so the OLD table — with its old heading and
        // its old "searched N minutes ago" age — stayed on screen with no visible sign that this run found
        // nothing.
        var panel = Furnished(Board(Market("Near", 12, 12_000, 210)), new CommodityLedger());

        Assert.Contains("Near", TextOf(panel));

        // The registry behind Furnished() has no galaxy capability, so this run errors.
        panel.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Run"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("Near", TextOf(panel));
    }

    [AvaloniaFact]
    public void RunningTwiceDoesNotCrashThePage()
    {
        var panel = Furnished(new CommodityBoard(), Ledger());

        var run = () => panel.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Run"));

        // The registry has no galaxy capability, so the tool errors; what is under test is the rebuild after
 // the press, which is where the Market page's Find took d47 down.
        run().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        run().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Community Goal search", TextOf(panel));
    }

    [AvaloniaFact]
    public void ASaleLandingElsewhereRedrawsTheLedger()
    {
        var ledger = new CommodityLedger();
        var panel = Furnished(new CommodityBoard(), ledger);

        Assert.DoesNotContain("+280,000 cr", TextOf(panel));

        ledger.Apply(
        [
            Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1 }"""),
            Event($$"""{ "timestamp":"{{Noon.AddMinutes(5):yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":2, "Type":"palladium", "Count":100, "SellPrice":51000, "TotalSale":5100000, "AvgPricePaid":48200 }"""),
        ]);

        Dispatcher.UIThread.RunJobs();

        Assert.Contains("+280,000 cr", TextOf(panel));
    }

    [AvaloniaFact]
    public void ASaleLandingWhileThePageIsNotShowingDoesNotRebuildIt()
    {
        var ledger = new CommodityLedger();
        var search = new CommunityGoalSearch();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Ega",
                D47.Core.Capabilities.CapabilityRegistry.Build([]),
                Plans: null,
                LookupsEnabled: () => true,
                OpenSettings: null,
                Commodities: new CommodityBoard(),
                CommunityGoal: new CommunityGoalSurface(search, ledger, () => Fid, () => Noon.AddMinutes(10), Week)),
            plan: false,
            progress: false,
            course: false,
            market: false);

        var window = new Window { Content = panel, Width = 1000, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

 // search.Showing stays at its default of false: the page was never opened, so Ledger.Changed
        // firing on a sale must not rebuild it.
        Assert.DoesNotContain("+280,000 cr", TextOf(panel));

        ledger.Apply(
        [
            Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1 }"""),
            Event($$"""{ "timestamp":"{{Noon.AddMinutes(5):yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":2, "Type":"palladium", "Count":100, "SellPrice":51000, "TotalSale":5100000, "AvgPricePaid":48200 }"""),
        ]);

        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("+280,000 cr", TextOf(panel));
    }

    [AvaloniaFact]
    public void EditingTheCommodityChangesTheSavedSearch()
    {
        var search = new CommunityGoalSearch { Showing = () => true };
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Ega",
                D47.Core.Capabilities.CapabilityRegistry.Build([]),
                LookupsEnabled: () => true,
                Commodities: new CommodityBoard(),
                CommunityGoal: new CommunityGoalSurface(search, new CommodityLedger(), () => Fid, () => Noon, Week)),
            plan: false,
            progress: false,
            course: false,
            market: false);

        var window = new Window { Content = panel, Width = 1000, Height = 700 };
        window.Show();
        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var box = panel.GetVisualDescendants().OfType<TextBox>().Single(b => b.PlaceholderText == "Palladium");
        box.Text = "Gold";

        panel.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Run"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Gold", search.Commodity);
    }

    [AvaloniaFact]
    public void CommunityGoalWithoutCommoditiesDoesNotCrashOrAddACrumb()
    {
 // RoutingSurface makes the two independent: a caller can supply CommunityGoal without
        // Commodities, and wiring the redraw subscription without checking both threw a
        // NullReferenceException during window construction.
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Ega",
                D47.Core.Capabilities.CapabilityRegistry.Build([]),
                Plans: null,
                LookupsEnabled: () => true,
                OpenSettings: null,
                Commodities: null,
                CommunityGoal: new CommunityGoalSurface(new CommunityGoalSearch(), new CommodityLedger(), () => Fid, () => Noon, Week)),
            plan: false,
            progress: false,
            course: false,
            market: false);

        var window = new Window { Content = panel, Width = 1000, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(panel.Nav.Roots(PanelTab.Routing), root => root.Key == RoutingPages.CommunityGoalRoot);
    }

    [AvaloniaFact]
    public void WithLookupsOffThePageSaysSo()
    {
        var panel = Furnished(new CommodityBoard(), Ledger(), lookups: false);

        var drawn = TextOf(panel);

        Assert.Contains(drawn, text => text.Contains("switched off", StringComparison.OrdinalIgnoreCase));

        // The ledger is the journal's and stays: nothing about it needs the network.
        Assert.Contains("This session", drawn);
    }

    [AvaloniaFact]
    public void TheResultsTableStaysInsideItsCard()
    {
        var panel = Furnished(
            Board(Market("Far", 90, 50_000, 40), Market("Near", 12, 12_000, 210)),
            new CommodityLedger(),
            width: 900);

        Assert.Contains("Distance", TextOf(panel));

        ResultsTableBounds.RowsStayInsideTheCard(panel, "What came back", "Station");
    }
}
