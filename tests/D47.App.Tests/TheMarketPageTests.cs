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

/// <summary>The Market page on the Routing tab.</summary>
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
    /// The table is the reason the page exists: six stations with a price, a stock figure, a distance
    /// and a date is a thing to look at rather than listen to.
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

    /// <summary>The same fixed-width row block as the Community Goal table, and the same thing asked of it: the
    /// columns are drawn inside the card or not at all.</summary>
    [AvaloniaFact]
    public void TheResultsTableStaysInsideItsCard()
    {
        var panel = Furnished(Board(
            Market("A station with a long enough name to fill its column", "Sol", 0, 900, 1000),
            Market("Far and cheap", "Elsewhere", 40, 300, 1000)));

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var window = panel.GetVisualAncestors().OfType<Window>().Single();
        window.Measure(new Avalonia.Size(900, 700));
        window.Arrange(new Avalonia.Rect(0, 0, 900, 700));
        Dispatcher.UIThread.RunJobs();

        ResultsTableBounds.RowsStayInsideTheCard(panel, "What came back", "Station");
    }

    /// <summary>The date is the part this feature is wrong without.</summary>
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
    /// Switched off is a state rather than an error, and the page says which row turns it on — the same
    /// answer the tool gives, for the reason Phase 3 states.
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

    /// <summary><c>Build()</c> clears and redraws the page after every Find, but the commodity and tonnes boxes are <c>readonly</c> fields: a second wrap finds them still parented to the first container and Avalonia refuses.</summary>
    [AvaloniaFact]
    public void FindingASecondTimeDoesNotCrashThePage()
    {
        var panel = Furnished(new CommodityBoard());

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var commodity = panel.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(box => box.PlaceholderText == "which one");

        // Find() is rebuilt fresh by every Build(), so it is re-queried each time — the readonly fields
        // (_commodity among them) are the ones the fix has to keep working across rebuilds.
        commodity.Text = "Tritium";
        panel.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Find it"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Reaching here at all is the assertion: before the fix, the second Build() throws
        // InvalidOperationException from inside Labelled() and takes the process down with it.
        commodity.Text = "Tritium";
        panel.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Find it"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary><c>surface_stations</c> defaults to false for every search, so this page needs a way to turn it back on: a commodity sold only at a Planetary Port would otherwise be unreachable from here.</summary>
    [AvaloniaFact]
    public void TheSurfaceStationsCheckboxReachesTheSearch()
    {
        D47.Core.Capabilities.ToolArguments? sent = null;

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Sol",
                D47.Core.Capabilities.CapabilityRegistry.Build(
                [
                    new D47.Core.Capabilities.CapabilityDescriptor
                    {
                        Id = "probe",
                        Group = "Foundation",
                        Name = "Probe",
                        Summary = "Captures what the Market page asks for.",
                        Tools =
                        [
                            new D47.Core.Capabilities.ToolDefinition
                            {
                                Name = "find_nearest_station",
                                Description = "Captures the arguments it was called with.",
                                Parameters =
                                [
                                    new D47.Core.Capabilities.ToolParameter
                                    {
                                        Name = "commodity",
                                        Type = D47.Core.Capabilities.ToolParameterType.String,
                                        Description = "Which one.",
                                    },
                                    new D47.Core.Capabilities.ToolParameter
                                    {
                                        Name = "surface_stations",
                                        Type = D47.Core.Capabilities.ToolParameterType.Boolean,
                                        Description = "Also planetary ports, outposts and settlements.",
                                    },
                                ],
                                Handler = (arguments, _) =>
                                {
                                    sent = arguments;
                                    return Task.FromResult(D47.Core.Capabilities.ToolResult.Ok("done"));
                                },
                            },
                        ],
                    },
                ]),
                Plans: null,
                LookupsEnabled: () => true,
                OpenSettings: null,
                Commodities: new CommodityBoard()),
            plan: false,
            progress: false,
            course: false);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();
        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        panel.GetVisualDescendants().OfType<TextBox>().Single(box => box.PlaceholderText == "which one").Text
            = "Tritium";
        panel.GetVisualDescendants().OfType<CheckBox>().Single(box => Equals(box.Content, "Include surface stations"))
            .IsChecked = true;

        panel.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Find it"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(sent);
        Assert.True(sent!.TryGetString("surface_stations", out var value));
        Assert.Equal("true", value);
    }
}
