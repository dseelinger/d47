using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>A search for several commodities at one station lists every one of them on each station's row.</summary>
public sealed class RouteMarketShowsEveryCommodityAtEachStationTests
{
    private static MarketSnapshot Market(string station, double x, int gold, int painite, int platinum) => new()
    {
        Station = station,
        System = station + " system",
        X = x,
        Type = "Coriolis Starport",
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gold"] = new("Gold") { BuyPrice = gold, Supply = 1_200 },
            ["Painite"] = new("Painite") { BuyPrice = painite, Supply = 340 },
            ["Platinum"] = new("Platinum") { BuyPrice = platinum, Supply = 75 },
        },
    };

    private static readonly CommodityQuery Asked = new(
        "Painite",
        Tonnes: 40,
        MaxDistance: 100,
        More: [new("Platinum", 12), new("Gold", null)]);

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

    private sealed class NoGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window, Trade Trade) Furnished()
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var trade = new Trade();
        var board = new CommodityBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new NoGalaxy(), () => "Sol", settings, trade, board: board)]);

        board.Post(new CommodityPosting(
            Asked,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(
                    Asked,
                    [Market("Ohm Port", 12, 9_412, 51_870, 37_205), Market("Vela Hub", 31, 9_105, 50_990, 38_010)],
                    Market("Home", 0, 0, 0, 0)),
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

    private static List<string> Texts(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void EachStationListsEveryCommoditysPriceAndStock()
    {
        var (panel, window, _) = Furnished();

        var texts = Texts(panel);

        Assert.Contains("Buying Painite, Platinum and Gold near Sol", texts);
        Assert.Contains("Commodity", texts);
        Assert.Equal(2, texts.Count(text => text == "Gold"));
        Assert.Equal(2, texts.Count(text => text == "Platinum"));
        Assert.Contains($"{9_412:N0}", texts);
        Assert.Contains($"{37_205:N0}", texts);
        Assert.Contains($"{75:N0}", texts);
        Assert.Contains($"{(40 * 51_870) + (12 * 37_205) + 9_412:N0}", texts);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-market-several-commodities.png");

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAHeaderAsksAgainForEveryCommodityWithItsOwnTonnage()
    {
        var (panel, window, trade) = Furnished();

        panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "MarketHead.Distance")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Asked.Wants, trade.Asked[0].Wants);
        Assert.Equal(CommodityOrder.Distance, trade.Asked[0].OrderBy);

        window.Close();
    }
}
