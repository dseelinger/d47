using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>On Route market the Distance and Price headers ask find_nearest_station again, ranked by that column (#1113).</summary>
public sealed class AColumnHeaderOnRouteMarketRerunsTheSearchRankedTests
{
    private static MarketSnapshot Market(string station, double x, int buy) => new()
    {
        Station = station,
        System = station + " system",
        X = x,
        Type = "Coriolis Starport",
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-6),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tritium"] = new("Tritium") { BuyPrice = buy, SellPrice = buy, Supply = 1000, Demand = 1000 },
        },
    };

    private sealed class Trade : ITradePlanService
    {
        public List<CommodityQuery> Asked { get; } = [];

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) => Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken)
        {
            Asked.Add(search.Query);
            return Task.FromResult(new CommodityAnswer([new CommodityOffer(Market("Near", 10, 900), 900, 1, 10)], 1, 0, true));
        }

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) => Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) => Task.FromResult<BestCargoAnswer?>(null);
    }

    private sealed class NoGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window, Trade Trade) Furnished(TradeSide side = TradeSide.Buying)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var trade = new Trade();
        var board = new CommodityBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new NoGalaxy(), () => "Sol", settings, trade, board: board)]);

        var query = new CommodityQuery("Tritium", side, MaxDistance: 100);
        var origin = Market("Home", 0, 0);
        board.Post(new CommodityPosting(
            query,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(query, [Market("Near", 10, 900), Market("Far", 40, 300)], origin),
                2,
                DroppedAsStale: 0,
                OriginKnown: true),
            "Sol",
            DateTimeOffset.UtcNow));

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Sol",
                registry,
                Plans: null,
                LookupsEnabled: () => true,
                OpenSettings: null,
                Commodities: board,
                Clipboard: null),
            plan: false,
            progress: false);

        var window = new Window { Content = panel, Width = 1100, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Navigation;
        Dispatcher.UIThread.RunJobs();

        return (panel, window, trade);
    }

    private static void Press(PanelView panel, string name)
    {
        panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void PressingDistanceAsksForTheNearestAndThenTheFarthestFirst()
    {
        var (panel, window, trade) = Furnished();

        Press(panel, "MarketHead.Distance");
        Assert.Equal(CommodityOrder.Distance, trade.Asked[^1].OrderBy);
        Assert.False(trade.Asked[^1].Descending);

        Press(panel, "MarketHead.Distance");
        Assert.Equal(CommodityOrder.Distance, trade.Asked[^1].OrderBy);
        Assert.True(trade.Asked[^1].Descending);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingPriceReversesTheBestFirstDefaultForABuyer()
    {
        var (panel, window, trade) = Furnished();

        Press(panel, "MarketHead.Price");

        Assert.Equal(CommodityOrder.Price, trade.Asked[^1].OrderBy);
        Assert.True(trade.Asked[^1].Descending);
        Assert.True(trade.Asked[^1].Reversed);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingPaysReversesTheBestFirstDefaultForASeller()
    {
        var (panel, window, trade) = Furnished(TradeSide.Selling);

        Press(panel, "MarketHead.Pays");

        Assert.False(trade.Asked[^1].Descending);
        Assert.True(trade.Asked[^1].Reversed);
        Assert.True(trade.Asked[^1].Side == TradeSide.Selling);

        window.Close();
    }

    [AvaloniaFact]
    public void AColumnThatIsNotRankedIsNotAButton()
    {
        var (panel, window, _) = Furnished();

        var buttons = panel.GetVisualDescendants().OfType<Button>().Select(button => button.Name).ToList();

        Assert.Contains("MarketHead.Distance", buttons);
        Assert.DoesNotContain("MarketHead.Station", buttons);
        Assert.DoesNotContain("MarketHead.Stock", buttons);
        Assert.DoesNotContain("MarketHead.Priced", buttons);

        window.Close();
    }
}
