using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// Against an <c>api/dump</c> response for Sol captured on 2026-10-31, cut to four stations and two bodies, plus two
/// bodies spliced in from the LTT 7786 dump of the same day for materials, orbit and biology genera.
/// </summary>
public class ASystemsStationsAndBodiesCarryTheirFullDetailTests
{
    private sealed class Answer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private static async Task<StarSystemProfile> Profile(string json)
    {
        using var service = new SpanshStarSystemService(
            NullLogger<SpanshStarSystemService>.Instance,
            new HttpClient(new Answer(json)));

        return (await service.ProfileAsync(10477373803, TestContext.Current.CancellationToken))!;
    }

    private static Task<StarSystemProfile> Sol() => Profile(Fixture.Text("spansh-dump-sol-trimmed.json"));

    private static StationProfile Station(StarSystemProfile profile, string name) =>
        profile.Stations.Single(station => station.Name == name);

    private static BodyProfile Body(StarSystemProfile profile, string name) =>
        profile.Bodies.Single(body => body.Name == name);

    [Fact]
    public async Task AStationCarriesItsIdentityPoliticsEconomiesPadsAndAccess()
    {
        var profile = await Sol();
        var galileo = Station(profile, "Galileo");

        Assert.Equal(128016640, galileo.MarketId);
        Assert.Equal("Federation", galileo.Allegiance);
        Assert.Equal("Expansion", galileo.ControllingFactionState);
        Assert.Equal([new EconomyShare("Refinery", 100)], galileo.Economies);
        Assert.Equal(new PadCounts(Small: 9, Medium: 13, Large: 6), galileo.Pads);
        Assert.Null(galileo.CarrierDockingAccess);
        Assert.Equal("Squadron Friends", Station(profile, "X2L-2KN").CarrierDockingAccess);
    }

    [Fact]
    public async Task AStationsMarketListsEachCommodityWithItsPricesAndStock()
    {
        var market = Station(await Sol(), "Galileo").Market!;

        Assert.Equal(4, market.Commodities.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 16, 59, 58, TimeSpan.Zero), market.ReportedAt);

        var alexandrite = market.Commodities.Single(row => row.Name == "Alexandrite");

        Assert.Equal("Alexandrite", alexandrite.Symbol);
        Assert.Equal("Minerals", alexandrite.Category);
        Assert.Equal(0, alexandrite.BuyPrice);
        Assert.Equal(218309, alexandrite.SellPrice);
        Assert.Equal(0, alexandrite.Supply);
        Assert.Equal(20909, alexandrite.Demand);
    }

    [Fact]
    public async Task AStationsOutfittingListsEachModuleWithItsClassRatingAndShip()
    {
        var outfitting = Station(await Sol(), "Galileo").Outfitting!;

        Assert.Equal(new DateTimeOffset(2026, 10, 31, 16, 59, 58, TimeSpan.Zero), outfitting.ReportedAt);
        Assert.Equal(
            new StationModule("Lightweight Alloy", "Adder_Armour_Grade1", "standard", 1, "I", "Adder"),
            outfitting.Modules[0]);
    }

    [Fact]
    public async Task AStationsShipyardListsEachShip()
    {
        var shipyard = Station(await Sol(), "Galileo").Shipyard!;

        Assert.Equal(new DateTimeOffset(2026, 10, 31, 16, 59, 58, TimeSpan.Zero), shipyard.ReportedAt);
        Assert.Equal(new StationShip("Beluga Liner", "BelugaLiner"), shipyard.Ships[1]);
    }

    [Fact]
    public async Task AStationWithoutAShipyardLeavesItNull()
    {
        var burnell = Station(await Sol(), "Burnell Station");

        Assert.NotNull(burnell.Market);
        Assert.NotNull(burnell.Outfitting);
        Assert.Null(burnell.Shipyard);
    }

    [Fact]
    public async Task ABodyCarriesItsMaterialsCompositionOrbitRotationAndReportTime()
    {
        var body = Body(await Sol(), "LTT 7786 4 a");

        Assert.Equal(15.630939, body.Materials["Carbon"], 6);
        Assert.Equal(11, body.Materials.Count);
        Assert.Equal(91.0291, body.SolidComposition["Rock"], 4);
        Assert.Empty(body.AtmosphereComposition);
        Assert.Equal(0.798199456875, body.OrbitalPeriod!.Value, 9);
        Assert.Equal(0.00245683045276425, body.SemiMajorAxis!.Value, 12);
        Assert.Equal(0.000738, body.OrbitalEccentricity!.Value, 6);
        Assert.Equal(0.159468, body.OrbitalInclination!.Value, 6);
        Assert.Equal(171.332966, body.ArgOfPeriapsis!.Value, 6);
        Assert.Equal(0.798204212395833, body.RotationalPeriod!.Value, 9);
        Assert.True(body.TidallyLocked);
        Assert.Equal(5.2029, body.AxialTilt!.Value, 4);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 10, 20, 32, TimeSpan.Zero), body.ReportedAt);
    }

    private static Task<StarSystemProfile> Ltt7786() => Profile(Fixture.Text("spansh-dump-ltt-7786.json"));

    [Fact]
    public async Task BarycentresAreListedApartFromTheBodiesWithTheirOrbitsAndParents()
    {
        var profile = await Ltt7786();

        Assert.Equal(7, profile.Barycentres.Count);
        Assert.DoesNotContain(profile.Bodies, body => body.Type == "Barycentre");

        var first = profile.Barycentres.Single(barycentre => barycentre.BodyId == 14);

        Assert.Equal([("Star", 0)], first.Parents);
        Assert.Equal(32.687657, first.MeanAnomaly!.Value, 6);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 18, 49, 43, TimeSpan.Zero), first.MeanAnomalyAt);
        Assert.Equal(26.783067, first.AscendingNode!.Value, 6);
        Assert.Equal(1.23361759860611, first.SemiMajorAxis!.Value, 12);

        Assert.Equal([("Planet", 36), ("Star", 0)], profile.Barycentres.Single(barycentre => barycentre.BodyId == 40).Parents);
    }

    [Fact]
    public async Task ABodyKeepsItsWholeParentChainAndItsFullOrbitAndTiltInDegrees()
    {
        var body = (await Ltt7786()).Bodies.Single(candidate => candidate.BodyId == 15);

        Assert.Equal([("Null", 14), ("Star", 0)], body.Parents);
        Assert.Equal(0, body.ParentId);
        Assert.Equal(153.87406, body.AscendingNode!.Value, 5);
        Assert.Equal(14.713517, body.MeanAnomaly!.Value, 6);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 21, 43, 25, TimeSpan.Zero), body.MeanAnomalyAt);
        Assert.Equal(109.1416, body.AxialTilt!.Value, 4);
    }

    [Fact]
    public async Task ABodyWithAnAtmosphereGivesItsGases()
    {
        var body = Body(await Sol(), "LTT 7786 9 h");

        Assert.Equal(100, body.AtmosphereComposition["Argon"]);
        Assert.False(body.TidallyLocked);
    }

    [Fact]
    public async Task ABodyWithBiologyNamesItsGenera()
    {
        var profile = await Sol();

        Assert.Equal(["Bacterium", "Fonticulua", "Fumerola"], Body(profile, "LTT 7786 9 h").Genera);
        Assert.Empty(Body(profile, "Mercury").Genera);
    }

    [Fact]
    public async Task AStarCarriesItsAgeAndAbsoluteMagnitude()
    {
        var sol = Body(await Sol(), "Sol");

        Assert.Equal(4792, sol.Age);
        Assert.Equal(4.829987, sol.AbsoluteMagnitude!.Value, 6);
    }

    [Fact]
    public async Task ABodyAndAStationMissingEveryNewFieldStillRead()
    {
        var profile = await Profile("""
            {"system":{"name":"Bare","id64":1,"bodies":[
              {"bodyId":3,"name":"Bare 1","type":"Planet","stations":[{"name":"Plain"}]}
            ]}}
            """);

        var body = Body(profile, "Bare 1");

        Assert.Empty(body.Materials);
        Assert.Empty(body.AtmosphereComposition);
        Assert.Empty(body.SolidComposition);
        Assert.Empty(body.Genera);
        Assert.Null(body.OrbitalPeriod);
        Assert.Null(body.SemiMajorAxis);
        Assert.Null(body.OrbitalEccentricity);
        Assert.Null(body.OrbitalInclination);
        Assert.Null(body.ArgOfPeriapsis);
        Assert.Null(body.RotationalPeriod);
        Assert.Null(body.TidallyLocked);
        Assert.Null(body.AxialTilt);
        Assert.Null(body.Age);
        Assert.Null(body.AbsoluteMagnitude);
        Assert.Null(body.ReportedAt);

        var station = Station(profile, "Plain");

        Assert.Null(station.MarketId);
        Assert.Null(station.Allegiance);
        Assert.Null(station.ControllingFactionState);
        Assert.Empty(station.Economies);
        Assert.Null(station.Pads);
        Assert.Null(station.CarrierDockingAccess);
        Assert.Null(station.Market);
        Assert.Null(station.Outfitting);
        Assert.Null(station.Shipyard);
    }

    [Fact]
    public async Task TheMarketIdEqualsTheOneAStationSearchCarriesForTheSameStation()
    {
        var galileo = Station(await Sol(), "Galileo");

        using var search = JsonDocument.Parse("""{"count":1,"results":[{"name":"Galileo","market_id":128016640,"system_name":"Sol"}]}""");
        var found = SpanshResponse.ReadStations(search).Stations.Single();

        Assert.Equal(found.MarketId, galileo.MarketId);
    }

    [Theory]
    [InlineData("$Codex_Ent_Bacterial_Genus_Name;", "Bacterium")]
    [InlineData("$Codex_Ent_Shrubs_Genus_Name;", "Frutexa")]
    [InlineData("$Codex_Ent_Unheard_Of_Genus_Name;", "Unheard Of")]
    [InlineData("Tussock", "Tussock")]
    public void AGenusKeyBecomesTheGenusName(string key, string expected) =>
        Assert.Equal(expected, CodexGenus.Name(key));
}
