using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public class AStrongholdCarrierIsAStationTypeTests
{
    private static string Body(StationSearch search)
    {
        Assert.True(
            StationQuery.TryParse(search with { ReferenceSystem = "Sol" }, out var query, out var failure),
            failure);

        return SpanshRequest.Stations(query);
    }

    private static JsonElement Filters(StationSearch search) =>
        JsonDocument.Parse(Body(search)).RootElement.GetProperty("filters");

    private static string Failure(StationSearch search)
    {
        Assert.False(StationQuery.TryParse(search with { ReferenceSystem = "Sol" }, out _, out var failure));
        return failure;
    }

    private static StationSearch Carriers(string? power = null, string? powerState = null) => new()
    {
        StationTypes = "Stronghold carrier",
        Filters = new Dictionary<string, string>
        {
            ["power"] = power!,
            ["power_state"] = powerState!,
        }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value),
    };

    [Fact]
    public void ACarrierSearchWithAPowerSendsTheExactNameStrongholdStateAndPowerAndNoType()
    {
        var body = Body(Carriers("Zemina Torval"));
        var filters = Filters(Carriers("Zemina Torval"));

        Assert.Contains("\"name\":{\"value\":[\"Stronghold Carrier\"]}", body, StringComparison.Ordinal);
        Assert.Contains("\"system_power_state\":{\"value\":[\"Stronghold\"]}", body, StringComparison.Ordinal);
        Assert.Contains("\"system_controlling_power\":{\"value\":[\"Zemina Torval\"]}", body, StringComparison.Ordinal);
        Assert.False(filters.TryGetProperty("type", out _));
    }

    [Fact]
    public void ACarrierSearchWithoutAPowerSendsNoPower()
    {
        var body = Body(Carriers());

        Assert.Contains("\"name\":{\"value\":[\"Stronghold Carrier\"]}", body, StringComparison.Ordinal);
        Assert.DoesNotContain("system_controlling_power", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AStrongholdPowerStateIsWrittenOnce()
    {
        var body = Body(Carriers(powerState: "Stronghold"));

        Assert.Equal(1, body.Split("system_power_state").Length - 1);
    }

    [Fact]
    public void ACarrierSearchWithAnotherTypeFailsInOneSentence()
    {
        var failure = Failure(new StationSearch { StationTypes = "Stronghold carrier, Outpost" });

        Assert.Equal("Stronghold carrier is searched on its own; untick the other types.", failure);
    }

    [Fact]
    public void ACarrierSearchWithAnotherPowerStateFails()
    {
        Assert.Contains("Stronghold carrier", Failure(Carriers(powerState: "Exploited")), StringComparison.Ordinal);
    }

    [Fact]
    public void OtherStationNamesStillSendTheLooseNameAndATypeFilter()
    {
        var filters = Filters(new StationSearch { StationTypes = "Outpost" });

        Assert.False(filters.TryGetProperty("name", out _));
        Assert.True(filters.TryGetProperty("type", out _));
    }

    private static StationSummary Carrier(string power) => new()
    {
        Name = "Stronghold Carrier",
        SystemName = "Somewhere",
        Type = "Planetary Outpost",
        ControllingPower = power,
        PowerState = "Stronghold",
    };

    private static readonly StationSummary Outpost = new()
    {
        Name = "Hutton Orbital",
        SystemName = "Alpha Centauri",
        Type = "Planetary Outpost",
    };

    private sealed class Held : IGalaxyService
    {
        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult("Sol", 2, [Carrier("Zemina Torval"), Outpost]));

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(
            MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static async Task<StationSearchResult> Run(StationSearch search)
    {
        Assert.True(StationQuery.TryParse(search with { ReferenceSystem = "Sol" }, out var query, out var failure), failure);

        return await new StationPowerFilter(new Held(), () => new PowerplayPledge("Jerome Archer"))
            .FindStationsAsync(query, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ACarrierSearchKeepsAnotherPowersCarrier()
    {
        var result = await Run(Carriers());

        Assert.Contains(result.Stations, station => station.Name == "Stronghold Carrier");
        Assert.Equal(0, result.LeftOut);
    }

    [Fact]
    public async Task ASearchNamingTheCarriersPowerKeepsItWithoutAType()
    {
        var result = await Run(new StationSearch
        {
            Filters = new Dictionary<string, string> { ["power"] = "Zemina Torval" },
        });

        Assert.Contains(result.Stations, station => station.Name == "Stronghold Carrier");
        Assert.Equal(0, result.LeftOut);
    }

    [Fact]
    public async Task ASearchNamingNeitherDropsItAndCountsIt()
    {
        var result = await Run(new StationSearch { StationTypes = "Outpost" });

        Assert.DoesNotContain(result.Stations, station => station.Name == "Stronghold Carrier");
        Assert.Equal(1, result.LeftOut);
    }
}
