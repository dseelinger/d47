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

/// <summary>The Community Goal page on the Routing tab (#296), and that it survives its own rebuild.</summary>
public class TheCommunityGoalPageTests
{
    private const string Fid = "F1234";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null ? parsed : throw new InvalidOperationException(json);

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

    private static CommodityBoard Board(params MarketSnapshot[] markets)
    {
        var board = new CommodityBoard();
        var query = new CommodityQuery("Palladium", MaxDistance: 250, OrderBy: CommodityOrder.Distance, Limit: 10);

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

    private static PanelView Furnished(CommodityBoard board, CommodityLedger ledger, bool lookups = true)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Ega",
                D47.Core.Capabilities.CapabilityRegistry.Build([]),
                Plans: null,
                LookupsEnabled: () => lookups,
                OpenSettings: null,
                Commodities: board,
                CommunityGoal: new CommunityGoalSurface(new CommunityGoalSearch(), ledger, () => Fid, () => Noon.AddMinutes(10))),
            plan: false,
            progress: false,
            course: false,
            market: false);

        var window = new Window { Content = panel, Width = 1000, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Routing;
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
    public void ItDrawsTheLedgerNetOfCost()
    {
        var panel = Furnished(new CommodityBoard(), Ledger());

        var drawn = TextOf(panel);

        Assert.Contains("This session", drawn);
        Assert.Contains("Today", drawn);
        Assert.Contains("This week", drawn);
        Assert.Equal(3, drawn.Count(text => text == "+280,000 cr"));
        Assert.Contains(drawn, text => text.StartsWith("Last sale: 100 tonnes at 51,000 cr, paid 48,200 cr each, +280,000 cr", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void RunningTwiceDoesNotCrashThePage()
    {
        var panel = Furnished(new CommodityBoard(), Ledger());

        var run = () => panel.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Run"));

        // The registry has no galaxy capability, so the tool errors; what is under test is the
        // rebuild after the press, which is where the Market page's Find took d47 down (#284).
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
    public void EditingTheCommodityChangesTheSavedSearch()
    {
        var search = new CommunityGoalSearch();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Ega",
                D47.Core.Capabilities.CapabilityRegistry.Build([]),
                LookupsEnabled: () => true,
                Commodities: new CommodityBoard(),
                CommunityGoal: new CommunityGoalSurface(search, new CommodityLedger(), () => Fid, () => Noon)),
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
    public void WithLookupsOffThePageSaysSo()
    {
        var panel = Furnished(new CommodityBoard(), Ledger(), lookups: false);

        var drawn = TextOf(panel);

        Assert.Contains(drawn, text => text.Contains("switched off", StringComparison.OrdinalIgnoreCase));

        // The ledger is the journal's and stays: nothing about it needs the network.
        Assert.Contains("This session", drawn);
    }
}
