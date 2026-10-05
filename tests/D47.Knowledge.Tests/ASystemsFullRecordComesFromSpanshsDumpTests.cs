using System.Net;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// Against an <c>api/dump</c> response for LTT 7786 captured from the live service on 2026-10-05, with
/// the stations' <c>market</c>, <c>outfitting</c> and <c>shipyard</c> removed.
/// </summary>
public class ASystemsFullRecordComesFromSpanshsDumpTests
{
    private const long Ltt7786 = 633608311522;

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public List<string> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            UserAgents.Add(request.Headers.UserAgent.ToString());

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>What <see cref="HttpClient"/> throws when its own timeout passes.</summary>
    private sealed class TimesOut : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException();
    }

    private static SpanshStarSystemService Service(HttpMessageHandler handler) =>
        new(NullLogger<SpanshStarSystemService>.Instance, new HttpClient(handler));

    private static string Captured() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spansh-dump-ltt-7786.json"));

    private static async Task<StarSystemProfile> Profile()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, Captured()));

        return (await service.ProfileAsync(Ltt7786, TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task TheSystemGivesItsNamePositionPoliticsAndPopulation()
    {
        var profile = await Profile();

        Assert.Equal("LTT 7786", profile.Name);
        Assert.Equal(Ltt7786, profile.SystemAddress);
        Assert.Equal(new StarPosition(7.96875, -41.90625, 77.875), profile.Position);
        Assert.Equal("Independent", profile.Allegiance);
        Assert.Equal("Anarchy", profile.Government);
        Assert.Equal("Industrial", profile.PrimaryEconomy);
        Assert.Equal("Refinery", profile.SecondaryEconomy);
        Assert.Equal("Anarchy", profile.Security);
        Assert.Equal(35_261_804, profile.Population);
        Assert.False(profile.NeedsPermit);
        Assert.Equal("Diamond Frogs", profile.ControllingFaction);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 9, 15, 58, TimeSpan.Zero), profile.ReportedAt);
    }

    [Fact]
    public async Task EveryFactionComesMostInfluentialFirstWithItsStates()
    {
        var profile = await Profile();

        Assert.Equal(
            [
                "Diamond Frogs", "Posse of Niflheimri", "LTT 7786 Major Group", "Nationals of Yu Tiku",
                "Dominion of LTT 7786", "LTT 7786 Labour", "LTT 7786 Silver Partnership",
            ],
            profile.Factions.Select(f => f.Name));

        var first = profile.Factions[0];
        Assert.Equal("Anarchy", first.Government);
        Assert.Equal("Independent", first.Allegiance);
        Assert.Equal(0.543, first.Influence);
        Assert.Equal(["Boom"], first.ActiveStates);
        Assert.Empty(first.PendingStates);

        Assert.Equal(["War"], profile.Factions[2].ActiveStates);
        Assert.Equal("Federation", profile.Factions[2].Allegiance);
    }

    [Fact]
    public async Task PendingStatesAreReadBesideActiveOnes()
    {
        const string Body =
            """
            {"system":{"name":"Somewhere","id64":1,"factions":[
              {"name":"Small","influence":0.1},
              {"name":"Big","influence":0.6,"activeStates":[{"state":"Boom"}],
               "pendingStates":[{"state":"Election"},{"state":"Expansion"}]}
            ]}}
            """;

        using var service = Service(new Answer(HttpStatusCode.OK, Body));

        var profile = (await service.ProfileAsync(1, TestContext.Current.CancellationToken))!;

        Assert.Equal(["Big", "Small"], profile.Factions.Select(f => f.Name));
        Assert.Equal(["Boom"], profile.Factions[0].ActiveStates);
        Assert.Equal(["Election", "Expansion"], profile.Factions[0].PendingStates);
    }

    [Fact]
    public async Task ThePowerplayFiguresAreRead()
    {
        var powerplay = (await Profile()).Powerplay!;

        Assert.Equal("Jerome Archer", powerplay.ControllingPower);
        Assert.Equal("Fortified", powerplay.State);
        Assert.Equal(0.673658, powerplay.ControlProgress);
        Assert.Equal(187_320, powerplay.Reinforcement);
        Assert.Equal(83_042, powerplay.Undermining);
        Assert.Equal(
            ["Aisling Duval", "Denton Patreus", "Yuri Grom", "Zemina Torval", "Jerome Archer"],
            powerplay.Powers);
    }

    [Fact]
    public async Task ASystemWithNoPowerplayHasNone()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, """{"system":{"name":"Quiet","id64":2}}"""));

        var profile = (await service.ProfileAsync(2, TestContext.Current.CancellationToken))!;

        Assert.Null(profile.Powerplay);
    }

    [Fact]
    public async Task EveryStationIsReadTheOnesOnBodiesIncluded()
    {
        var profile = await Profile();

        Assert.Equal(102, profile.Stations.Count);
        Assert.Equal(
            new Dictionary<StationKind, int>
            {
                [StationKind.Starport] = 1,
                [StationKind.Outpost] = 2,
                [StationKind.SurfacePort] = 4,
                [StationKind.Settlement] = 84,
                [StationKind.FleetCarrier] = 6,
                [StationKind.Other] = 5,
            },
            profile.Stations.GroupBy(s => s.Kind).ToDictionary(g => g.Key, g => g.Count()));

        var gateway = profile.Stations.Single(s => s.Name == "Parise Gateway");
        Assert.Equal(StationKind.Starport, gateway.Kind);
        Assert.Equal("Ocellus Starport", gateway.Type);
        Assert.Null(gateway.Body);
        Assert.Equal(213.410763, gateway.DistanceToArrival);
        Assert.Equal("Diamond Frogs", gateway.ControllingFaction);
        Assert.Equal("Anarchy", gateway.Government);
        Assert.Equal("Industrial", gateway.PrimaryEconomy);
        Assert.Equal(PadSize.Large, gateway.LargestPad);
        Assert.Contains("Vista Genomics", gateway.Services);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 10, 3, 28, TimeSpan.Zero), gateway.UpdatedAt);

        Assert.Equal(PadSize.Medium, profile.Stations.Single(s => s.Name == "Foreman Dock").LargestPad);

        var depot = profile.Stations.Single(s => s.Name == "Galton Depot");
        Assert.Equal(StationKind.SurfacePort, depot.Kind);
        Assert.Equal("LTT 7786 10 i", depot.Body);

        var untyped = profile.Stations.Single(s => s.Name == "Moore Installation");
        Assert.Equal(StationKind.Other, untyped.Kind);
        Assert.Null(untyped.Type);
        Assert.Null(untyped.LargestPad);
    }

    [Theory]
    [InlineData("Coriolis Starport", StationKind.Starport)]
    [InlineData("Orbis Starport", StationKind.Starport)]
    [InlineData("Ocellus Starport", StationKind.Starport)]
    [InlineData("Dodec Starport", StationKind.Starport)]
    [InlineData("Asteroid base", StationKind.Starport)]
    [InlineData("Outpost", StationKind.Outpost)]
    [InlineData("Planetary Port", StationKind.SurfacePort)]
    [InlineData("Planetary Outpost", StationKind.SurfacePort)]
    [InlineData("Settlement", StationKind.Settlement)]
    [InlineData("Mega ship", StationKind.Megaship)]
    [InlineData("Drake-Class Carrier", StationKind.FleetCarrier)]
    [InlineData("Space Construction Depot", StationKind.Other)]
    [InlineData(null, StationKind.Other)]
    public void EachStationTypeHasItsKind(string? type, StationKind kind) =>
        Assert.Equal(kind, SpanshStarSystemService.KindOf(type));

    [Fact]
    public async Task EveryStarAndPlanetIsReadWithItsParentAndBarycentresAreSkipped()
    {
        var profile = await Profile();

        Assert.Equal(57, profile.Bodies.Count);
        Assert.Equal(3, profile.Bodies.Count(b => b.Type == "Star"));
        Assert.DoesNotContain(profile.Bodies, b => b.Name.Contains("barycentre", StringComparison.Ordinal));

        var primary = profile.Bodies.Single(b => b.Name == "LTT 7786");
        Assert.Equal(0, primary.BodyId);
        Assert.Null(primary.ParentId);
        Assert.Equal("K7", primary.SpectralClass);
        Assert.Equal("K (Yellow-Orange) Star", primary.SubType);
        Assert.Equal(0.78125, primary.SolarMasses);
        Assert.True(primary.Scoopable);

        // Its first parent is barycentre 14, which is passed over for the star beyond it.
        Assert.Equal(0, profile.Bodies.Single(b => b.Name == "LTT 7786 4").ParentId);
        Assert.Equal(15, profile.Bodies.Single(b => b.Name == "LTT 7786 4 a").ParentId);

        var moon = profile.Bodies.Single(b => b.Name == "LTT 7786 4 a");
        Assert.Equal(17, moon.BodyId);
        Assert.Equal("Planet", moon.Type);
        Assert.Equal("Rocky body", moon.SubType);
        Assert.Equal(0.0907912715407362, moon.Gravity);
        Assert.Equal(158.834106, moon.SurfaceTemperature);
        Assert.Equal(0.0, moon.SurfacePressure);
        Assert.Equal(776.628875, moon.Radius);
        Assert.Equal("Major Silicate Vapour Geysers", moon.Volcanism);
        Assert.True(moon.IsLandable);
        Assert.Null(moon.Scoopable);

        var second = profile.Bodies.Single(b => b.Name == "LTT 7786 2");
        Assert.Equal("Nitrogen", second.Atmosphere);
        Assert.Equal(1.35139747119405, second.Gravity);
        Assert.Equal(new Dictionary<string, int> { ["$SAA_SignalType_Human;"] = 2 }, second.Signals);

        var ringed = profile.Bodies.Single(b => b.Name == "LTT 7786 3");
        var ring = Assert.Single(ringed.Rings);
        Assert.Equal("LTT 7786 3 A Ring", ring.Name);
        Assert.Equal("Rocky", ring.Type);
        Assert.Equal(new Dictionary<string, int> { ["Musgravite"] = 1 }, ring.Signals);

        Assert.False(profile.Bodies.Single(b => b.Name == "LTT 7786 10").Scoopable);
    }

    [Fact]
    public async Task TheDumpIsAskedForFromSpanshWithD47sUserAgent()
    {
        var answer = new Answer(HttpStatusCode.OK, Captured());
        using var service = Service(answer);

        await service.ProfileAsync(Ltt7786, TestContext.Current.CancellationToken);

        var request = Assert.Single(answer.Requests);
        Assert.Equal("spansh.co.uk", request.Host);
        Assert.Equal("/api/dump/633608311522", request.AbsolutePath);
        Assert.StartsWith("d47/", Assert.Single(answer.UserAgents), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASystemSpanshDoesNotKnowGivesNull()
    {
        using var service = Service(new Answer(
            HttpStatusCode.NotFound, """{"error":"Could not find record with id64 1"}"""));

        Assert.Null(await service.ProfileAsync(1, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ARefusalIsTheServiceBeingUnavailable(HttpStatusCode status)
    {
        using var service = Service(new Answer(status, "{}"));

        await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.ProfileAsync(Ltt7786, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ATimeoutIsTheServiceBeingUnavailable()
    {
        using var service = Service(new TimesOut());

        var thrown = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.ProfileAsync(Ltt7786, TestContext.Current.CancellationToken));

        Assert.Equal("The system lookup took too long to answer.", thrown.Message);
    }
}
