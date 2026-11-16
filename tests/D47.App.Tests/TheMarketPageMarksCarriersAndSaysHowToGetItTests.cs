using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Market page marks fleet carriers, says when only carriers answered, and says how to get the commodity (#2446).</summary>
public sealed class TheMarketPageMarksCarriersAndSaysHowToGetItTests
{
    private static MarketSnapshot Market(string station, string type) => new()
    {
        Station = station,
        System = "Sol",
        Type = type,
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Painite"] = new("Painite") { BuyPrice = 50_000, SellPrice = 50_000, Supply = 10, Demand = 0 },
        },
    };

    private static CommodityPosting Posting(string commodity, bool carriersOnly, params MarketSnapshot[] markets) =>
        new(
            new CommodityQuery(commodity, MaxDistance: 50),
            new CommodityAnswer([.. markets.Select(market => new CommodityOffer(market, 50_000, 1, 5))], markets.Length, 0, true),
            "Sol",
            DateTimeOffset.UtcNow,
            carriersOnly);

    private static (Window Window, RouteMarketPage Page) Show(CommodityPosting posting, BodySearchLink? link)
    {
        var board = new CommodityBoard();
        board.Post(posting);

        var page = new RouteMarketPage(CapabilityRegistry.Build([]), board, bodySearch: () => link);
        var window = new Window { Content = page, Width = 1100, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static T? Named<T>(RouteMarketPage page, string name)
        where T : Control =>
        page.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);

    [AvaloniaFact]
    public void ACarrierRowIsMarkedAndAStationRowIsNot()
    {
        var (_, page) = Show(Posting("Painite", false, Market("Q7X-4KD", "FleetCarrier"), Market("Galileo", "Coriolis Starport")), null);

        var marks = page.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Name == RouteMarketPage.CarrierMarkName).ToList();

        Assert.Single(marks);
        Assert.Equal("FLEET CARRIER", marks[0].Text);
    }

    [AvaloniaFact]
    public void ACarriersOnlyAnswerOpensWithTheNotice()
    {
        var (_, page) = Show(Posting("Painite", true, Market("Q7X-4KD", "FleetCarrier")), null);

        var notice = Named<D47.App.Controls.Notice>(page, RouteMarketPage.CarriersOnlyName);

        Assert.NotNull(notice);
        Assert.Equal(
            "No station within 50 ly sells Painite, so these are fleet carriers. Their owners set the prices, and the listings move.",
            notice.Text);
    }

    [AvaloniaFact]
    public void AnAnswerWithStationsHasNoNotice()
    {
        var (_, page) = Show(Posting("Painite", false, Market("Galileo", "Coriolis Starport")), null);

        Assert.Null(Named<D47.App.Controls.Notice>(page, RouteMarketPage.CarriersOnlyName));
    }

    [AvaloniaFact]
    public void ANothingFoundAnswerShowsTheLineAndTheNoteAndNoTable()
    {
        var (_, page) = Show(Posting("Painite", false), null);

        var line = Named<TextBlock>(page, RouteMarketPage.NothingName);

        Assert.Equal("Nothing within 50 ly sells Painite, at a station or on a fleet carrier.", line?.Text);
        Assert.NotNull(Named<Border>(page, RouteMarketPage.NoteName));
        Assert.Null(Named<Control>(page, RouteMarketPage.HeadName + ".Station"));
    }

    [AvaloniaFact]
    public void AHotspotCommodityNoteLinksToTheBodiesSearchClampedToItsCeiling()
    {
        IReadOnlyDictionary<string, string>? ran = null;
        var posting = Posting("Painite", false, Market("Galileo", "Coriolis Starport"));
        posting = posting with { Query = posting.Query with { MaxDistance = 5_000 } };

        var (_, page) = Show(posting, new BodySearchLink(arguments => ran = arguments));

        var tile = Named<Button>(page, RouteMarketPage.HotspotName);

        Assert.NotNull(tile);
        Assert.Equal(44, tile.Height);
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("Painite", ran!["hotspot"]);
        Assert.Equal("Sol", ran["near"]);
        Assert.Equal("1000", ran["max_distance"]);
    }

    [AvaloniaFact]
    public void ACommodityWithNoHotspotsHasTheNoteAndNoLink()
    {
        var (_, page) = Show(Posting("Gold", false, Market("Galileo", "Coriolis Starport")), new BodySearchLink(_ => { }));

        Assert.NotNull(Named<Border>(page, RouteMarketPage.NoteName));
        Assert.Null(Named<Button>(page, RouteMarketPage.HotspotName));
    }

    [AvaloniaFact]
    public void AHotspotCommodityNamesItsRingTypesAndKeepsOnlyTheHotspotLink()
    {
        var (_, page) = Show(Posting("Tritium", false, Market("Galileo", "Coriolis Starport")), new BodySearchLink(_ => { }));

        Assert.Equal("Mined from Icy rings.", Named<TextBlock>(page, RouteMarketPage.RingTypeLineName)?.Text);
        Assert.NotNull(Named<Button>(page, RouteMarketPage.HotspotName));
        Assert.Null(Named<Button>(page, RouteMarketPage.RingTypeLinkName));
    }

    [AvaloniaFact]
    public void ACommodityWithNoHotspotLinksToTheBodiesSearchByItsFirstRingType()
    {
        IReadOnlyDictionary<string, string>? ran = null;
        var (_, page) = Show(Posting("Gold", false, Market("Galileo", "Coriolis Starport")), new BodySearchLink(arguments => ran = arguments));

        Assert.StartsWith("Mined from Metallic or Metal Rich rings", Named<TextBlock>(page, RouteMarketPage.RingTypeLineName)?.Text);

        var tile = Named<Button>(page, RouteMarketPage.RingTypeLinkName);

        Assert.Equal("Find Metallic rings", tile?.Content);
        tile!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("Metallic", ran!["ring_type"]);
        Assert.Equal("Sol", ran["near"]);
        Assert.Equal("50", ran["max_distance"]);
        Assert.False(ran.ContainsKey("hotspot"));
    }

    [AvaloniaFact]
    public void AMotherlodeCommodityIsCrackedFromCoreAsteroids()
    {
        var (_, page) = Show(Posting("Void Opal", false, Market("Galileo", "Coriolis Starport")), new BodySearchLink(_ => { }));

        Assert.Equal("Cracked from core asteroids in Icy rings.", Named<TextBlock>(page, RouteMarketPage.RingTypeLineName)?.Text);
    }

    [AvaloniaFact]
    public void ACommodityWithNoHotspotAndOneRingTypeLinksToThatType()
    {
        var (_, page) = Show(Posting("Haematite", false, Market("Galileo", "Coriolis Starport")), new BodySearchLink(_ => { }));

        Assert.Equal("Mined from Rocky rings.", Named<TextBlock>(page, RouteMarketPage.RingTypeLineName)?.Text);
        Assert.Equal("Find Rocky rings", Named<Button>(page, RouteMarketPage.RingTypeLinkName)?.Content);
    }

    [AvaloniaFact]
    public void ACommodityBoughtOnlyAtMarketsHasNoNote()
    {
        var (_, page) = Show(Posting("CMM Composite", false), null);

        Assert.Null(Named<Border>(page, RouteMarketPage.NoteName));
    }
}
