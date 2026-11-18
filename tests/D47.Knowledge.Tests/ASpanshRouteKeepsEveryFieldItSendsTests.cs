using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class ASpanshRouteKeepsEveryFieldItSendsTests
{
    private static JsonElement Result(JsonDocument document) => document.RootElement.GetProperty("result");

    [Fact]
    public void APlottedRouteKeepsTheTotalTheServiceGives()
    {
        using var document = Fixture.Json("spansh-route-sol-colonia-full.json");

        var route = SpanshResponse.ReadRoute(Result(document));

        Assert.Equal(148, route!.ReportedJumps);
        Assert.NotEqual(route.ReportedJumps, route.TotalJumps);
    }

    [Fact]
    public void ARichesBodyKeepsItsScanValueTypeAndAddress()
    {
        using var document = Fixture.Json("spansh-riches-route-sol-full.json");

        var body = SpanshResponse.ReadRiches(Result(document))!.Stops.SelectMany(stop => stop.Bodies).First();

        Assert.Equal("Planet", body.Type);
        Assert.True(body.BodyAddress > 0);
        Assert.True(body.ScanValue > 0);
        Assert.True(body.MappingValue > 0);
    }

    [Fact]
    public void AnExobiologyBodyKeepsItsScanAndMappingValues()
    {
        using var document = Fixture.Json("spansh-exobiology-route-sol-full.json");

        var body = SpanshResponse.ReadExobiology(Result(document))!.Stops.SelectMany(stop => stop.Bodies).First();

        Assert.Equal("Planet", body.Type);
        Assert.True(body.BodyAddress > 0);
        Assert.True(body.ScanValue > 0);
        Assert.True(body.MappingValue > 0);
        Assert.True(body.LandmarkValue > 0);
    }

    [Fact]
    public void ACarrierRouteKeepsItsMassAndEachSystemsAddress()
    {
        using var document = Fixture.Json("spansh-fleetcarrier-route-sol-colonia-sol.json");

        var route = SpanshResponse.ReadCarrier(Result(document))!;

        Assert.Equal(25000, route.Mass);
        Assert.Equal(10477373803, route.Waypoints[0].SystemAddress);
    }

    [Fact]
    public void AMarketRowKeepsTheStationFactsAndEachCommoditysCategory()
    {
        using var document = Fixture.Json("spansh-markets-gold-sol-full.json");

        var market = SpanshResponse.ReadMarkets(document)[0];

        Assert.NotNull(market.MarketId);
        Assert.NotNull(market.Government);
        Assert.NotEmpty(market.Services);
        Assert.NotEmpty(market.Economies);
        Assert.NotNull(market.StationUpdatedAt);
        Assert.NotNull(market.Quote("Gold")!.Category);
    }
}
