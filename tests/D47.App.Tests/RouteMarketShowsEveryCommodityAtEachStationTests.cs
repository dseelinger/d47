using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>A search for several commodities at one station gives each commodity a column of its own, its price over its stock.</summary>
public sealed class RouteMarketShowsEveryCommodityAtEachStationTests
{
    private static MarketSnapshot Market(string station, string system, double x, int gold, int painite, int platinum) => new()
    {
        Station = station,
        System = system,
        X = x,
        Type = "Coriolis Starport",
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gold"] = new("Gold") { BuyPrice = gold, SellPrice = gold, Supply = 1_200, Demand = 900 },
            ["Painite"] = new("Painite") { BuyPrice = painite, SellPrice = painite, Supply = 340, Demand = 260 },
            ["Platinum"] = new("Platinum") { BuyPrice = platinum, SellPrice = platinum, Supply = 75, Demand = 40 },
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

    private static (PanelView Panel, Window Window, Trade Trade) Furnished(CommodityQuery? asked = null)
    {
        asked ??= Asked;

        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var trade = new Trade();
        var board = new CommodityBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new NoGalaxy(), () => "Sol", settings, trade, board: board)]);

        board.Post(new CommodityPosting(
            asked,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(
                    asked,
                    [
                        Market("Ohm Port", "Ohm", 12, 9_412, 51_870, 37_205),
                        Market("Galileo", "Sol", 0, 9_480, 52_010, 37_300),
                        Market("Vela Hub", "Vela", 31, 9_105, 50_990, 38_010),
                    ],
                    Market("Home", "Sol", 0, 0, 0, 0)),
                3,
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
                Clipboard: new RecordingClipboard()),
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

    private static List<string> Heads(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<Control>()
            .Select(control => control.Name ?? string.Empty)
            .Where(name => name.StartsWith(RouteMarketPage.HeadName + ".", StringComparison.Ordinal))];

    private static int Count(List<string> texts, string text) =>
        texts.Count(shown => string.Equals(shown, text, StringComparison.OrdinalIgnoreCase));

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.FindResource(key)!).Color;

    private static Color InkOf(List<Run> runs, string text) =>
        ((ISolidColorBrush)runs.Single(run => run.Text == text).Foreground!).Color;

    private static void Press(PanelView panel, string name)
    {
        panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void EachCommodityHasAColumnWithItsPriceOverItsStock()
    {
        var (panel, window, _) = Furnished();

        var texts = Texts(panel);

        Assert.Contains("Buying Painite, Platinum and Gold near Sol", texts);
        Assert.Equal(
            ["MarketHead.Station", "MarketHead.Distance", "MarketHead.Painite", "MarketHead.Platinum", "MarketHead.Gold", "MarketHead.Total", "MarketHead.Priced"],
            Heads(panel));
        Assert.Equal(3, Count(texts, "Price / stock"));
        Assert.Contains($"{9_412:N0}", texts);
        Assert.Contains($"{37_205:N0}", texts);
        Assert.Contains($"{75:N0}", texts);
        Assert.Contains($"{(40 * 51_870) + (12 * 37_205) + 9_412:N0}", texts);

        window.Close();
    }

    [AvaloniaFact]
    public void TotalSaysAnItemWithoutTonnesCountsAsOneTonne()
    {
        var (panel, window, _) = Furnished();

        Assert.Equal(1, Count(Texts(panel), "1 T where blank"));

        window.Close();
    }

    [AvaloniaFact]
    public void TotalSaysNothingMoreWhenEveryItemHasItsTonnes()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Furnished(new CommodityQuery("Gold", Tonnes: 20, MaxDistance: 100, More: [new("Platinum", 12)]));

        var texts = Texts(panel);

        Assert.Equal(0, Count(texts, "1 T where blank"));
        Assert.Contains("Buying Gold and Platinum near Sol", texts);
        Assert.Contains($"{(20 * 9_480) + (12 * 37_300):N0}", texts);

        var page = panel.GetVisualDescendants().OfType<RouteMarketPage>().Single();
        page.Add("Gold", 20);
        page.Add("Platinum", 12);
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-market-two-commodities.png");

        window.Close();
    }

    [AvaloniaFact]
    public void ASellerSeesWhatEachPaysOverItsDemand()
    {
        var (panel, window, _) = Furnished(new CommodityQuery("Gold", TradeSide.Selling, MaxDistance: 100, More: [new("Platinum", null)]));

        var texts = Texts(panel);

        Assert.Contains("Selling Gold and Platinum near Sol", texts);
        Assert.Equal(2, Count(texts, "Pays / demand"));
        Assert.Contains($"{900:N0}", texts);
        Assert.Contains($"{40:N0}", texts);

        window.Close();
    }

    [AvaloniaFact]
    public void TheCurrentSystemIsCyanAndAnyOtherIsA()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Furnished();

        List<Run> runs =
        [
            .. panel.GetVisualDescendants().OfType<TextBlock>().SelectMany(block => block.Inlines?.OfType<Run>() ?? []),
        ];

        Assert.Equal(Ink(ThemeManager.CyanKey), InkOf(runs, " (Sol)"));
        Assert.Equal(Ink(ThemeManager.AKey), InkOf(runs, " (Ohm)"));
        Assert.Equal(Ink(ThemeManager.WhiteKey), InkOf(runs, "Galileo"));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAHeaderAsksAgainForEveryCommodityWithItsOwnTonnage()
    {
        var (panel, window, trade) = Furnished();

        Press(panel, "MarketHead.Distance");

        Assert.Equal(Asked.Wants, trade.Asked[0].Wants);
        Assert.Equal(CommodityOrder.Distance, trade.Asked[0].OrderBy);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTotalAsksAgainRankedByPrice()
    {
        var (panel, window, trade) = Furnished();

        Press(panel, "MarketHead.Total");

        Assert.Equal(Asked.Wants, trade.Asked[0].Wants);
        Assert.Equal(CommodityOrder.Price, trade.Asked[0].OrderBy);
        Assert.True(trade.Asked[0].Descending);

        window.Close();
    }

    [AvaloniaFact]
    public void TheCommodityColumnsAreNotButtons()
    {
        var (panel, window, _) = Furnished();

        List<string?> buttons = [.. panel.GetVisualDescendants().OfType<Button>().Select(button => button.Name)];

        Assert.Contains("MarketHead.Total", buttons);
        Assert.DoesNotContain("MarketHead.Painite", buttons);
        Assert.DoesNotContain("MarketHead.Gold", buttons);

        window.Close();
    }
}
