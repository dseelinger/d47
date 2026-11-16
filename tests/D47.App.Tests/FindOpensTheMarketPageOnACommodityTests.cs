using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Choosing a commodity in Find opens Navigation › Market with it as the only tile and searches (#2445).</summary>
public sealed class FindOpensTheMarketPageOnACommodityTests
{
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

    private static (PanelView Panel, Trade Trade) Furnished()
    {
        var trade = new Trade();
        var board = new CommodityBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new NoGalaxy(), () => "Sol", TestSurface.Settings(), trade, board: board)]);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(() => new NavRoute(), () => "Sol", registry, Plans: null, Commodities: board)
            {
                SuggestionPause = TimeSpan.Zero,
            },
            plan: false,
            progress: false);

        var window = new Window { Content = panel, Width = 1100, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, trade);
    }

    private static List<string> Tiles(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains(RouteMarketPage.ItemClass))
            .Select(button => (string)button.Tag!)];

    private static void Choose(PanelView panel, string commodity)
    {
        panel.OpenFound(new PanelFindResult(PanelFindKind.Commodity, commodity, "Commodity", commodity, 1));
        UiWait.Until(() => panel.GetVisualDescendants().OfType<RouteMarketPage>().Any(), showing: () => "no Market page");
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ACommodityOpensTheMarketPageWithItAloneAndSearchesOnce()
    {
        var (panel, trade) = Furnished();
        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Choose(panel, "Lithium Hydroxide");
        UiWait.Until(() => trade.Asked.Count > 0, showing: () => "no search");

        Assert.Equal(PanelTab.Astrometrics, panel.Tab);
        Assert.Equal(["Lithium Hydroxide"], Tiles(panel));
        Assert.All(trade.Asked, query => Assert.Equal("Lithium Hydroxide", query.Commodity));
    }

    [AvaloniaFact]
    public void ACommodityReplacesTheTilesThatWereThere()
    {
        var (panel, trade) = Furnished();
        panel.Tab = PanelTab.Astrometrics;
        Dispatcher.UIThread.RunJobs();
        panel.GetVisualDescendants().OfType<RouteMarketPage>().Single().Add("Gold", 20);

        Choose(panel, "Lithium Hydroxide");
        UiWait.Until(() => trade.Asked.Count > 0, showing: () => "no search");

        Assert.Equal(["Lithium Hydroxide"], Tiles(panel));
        Assert.All(trade.Asked, query => Assert.Null(query.Tonnes));
    }
}
