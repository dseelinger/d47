using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Market page on the Routing tab (list.md Phase 49).
/// <para>
/// Driven through the drawn page rather than through the ranker, because the ranker being right
/// and the page showing it are two different claims and only one of them is what a Commander
/// looks at.
/// </para>
/// </summary>
public class TheMarketPageTests
{
    private static MarketSnapshot Market(
        string station,
        string system,
        double x,
        int buy,
        int supply,
        PriceSource source = PriceSource.Reported) => new()
        {
            Station = station,
            System = system,
            X = x,
            Type = "Coriolis Starport",
            HasLargePad = true,
            Source = source,
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-6),
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Tritium"] = new("Tritium") { BuyPrice = buy, Supply = supply },
            },
        };

    private static CommodityBoard Board(params MarketSnapshot[] markets)
    {
        var board = new CommodityBoard();
        var query = new CommodityQuery("Tritium", TradeSide.Buying, Tonnes: 700);
        var origin = Market("Home", "Sol", 0, 0, 0);

        board.Post(new CommodityPosting(
            query,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(query, markets, origin),
                markets.Length,
                DroppedAsStale: 0,
                OriginKnown: true),
            "Sol",
            DateTimeOffset.UtcNow));

        return board;
    }

    private static PanelView Furnished(CommodityBoard board, bool lookups = true)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Sol",
                D47.Core.Capabilities.CapabilityRegistry.Build([]),
                Plans: null,
                LookupsEnabled: () => lookups,
                OpenSettings: null,
                Commodities: board),
            plan: false,
            progress: false,
            course: false);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static string[] TextOf(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    [AvaloniaFact]
    public void TheMarketRootIsTheOneFurnished()
    {
        var panel = Furnished(Board(Market("Cheap", "Sol", 0, 500, 1000)));

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Market", panel.Nav.Root.Word);
    }

    /// <summary>
    /// The table is the reason the page exists: six stations with a price, a stock figure, a
    /// distance and a date is a thing to look at rather than listen to.
    /// </summary>
    [AvaloniaFact]
    public void ItDrawsTheAnswerTheCommanderWasAlreadyGiven()
    {
        var panel = Furnished(Board(
            Market("Near and dear", "Sol", 0, 900, 1000),
            Market("Far and cheap", "Elsewhere", 40, 300, 1000)));

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var drawn = TextOf(panel);

        Assert.Contains(drawn, text => text.Contains("Far and cheap", StringComparison.Ordinal));
        Assert.Contains(drawn, text => text.Contains("Near and dear", StringComparison.Ordinal));

        // The columns, and the load total the tonnage bought.
        Assert.Contains("Price", drawn);
        Assert.Contains("Stock", drawn);
        Assert.Contains("The load", drawn);
        Assert.Contains("210,000", drawn);
    }

    /// <summary>
    /// The date is the part this feature is wrong without. A page of prices with no ages on them
    /// reads as current whatever it is.
    /// </summary>
    [AvaloniaFact]
    public void EveryPriceCarriesItsAge()
    {
        var panel = Furnished(Board(Market("Somewhere", "Sol", 0, 500, 1000)));

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("6 hours ago", StringComparison.Ordinal));
    }

    /// <summary>A market the Commander stood in themselves is named as theirs.</summary>
    [AvaloniaFact]
    public void TheirOwnReadingIsNamedAsTheirs()
    {
        var panel = Furnished(Board(Market("Under their feet", "Sol", 0, 500, 1000, PriceSource.Seen)));

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("you saw it", StringComparison.Ordinal));
    }

    /// <summary>
    /// Switched off is a state rather than an error, and the page says which row turns it on —
    /// the same answer the tool gives, for the reason Phase 3 states.
    /// </summary>
    [AvaloniaFact]
    public void WithLookupsOffThePageSaysSoRatherThanDrawingAnEmptyTable()
    {
        var panel = Furnished(Board(Market("Somewhere", "Sol", 0, 500, 1000)), lookups: false);

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var drawn = TextOf(panel);

        Assert.Contains(drawn, text => text.Contains("switched off", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(drawn, text => text.Contains("Somewhere", StringComparison.Ordinal));
    }

    /// <summary>
    /// Nothing asked yet draws the form and no table. An empty grid with headings reads as "no
    /// stations found", which is a different and wrong answer.
    /// </summary>
    [AvaloniaFact]
    public void WithNothingAskedYetThereIsAFormAndNoTable()
    {
        var panel = Furnished(new CommodityBoard());

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var drawn = TextOf(panel);

        Assert.Contains("Where to buy it", drawn);
        Assert.DoesNotContain("Price", drawn);
    }
}
