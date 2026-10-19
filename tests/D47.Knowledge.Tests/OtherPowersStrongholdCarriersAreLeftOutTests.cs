using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public class OtherPowersStrongholdCarriersAreLeftOutTests
{
    private static string Row(string name, string system, string? power, string? state) =>
        $$"""
        {"name":"{{name}}","system_name":"{{system}}","type":"Planetary Outpost","distance":4.4,
         "system_controlling_power":{{(power is null ? "null" : $"\"{power}\"")}},
         "system_power_state":{{(state is null ? "null" : $"\"{state}\"")}}}
        """;

    private static string Response(params string[] rows) =>
        $$"""{"count":{{rows.Length}},"results":[{{string.Join(",", rows)}}]}""";

    private static readonly string AislingCarrierFirst = Response(
        Row("Stronghold Carrier", "Alpha Centauri", "Aisling Duval", "Stronghold"),
        Row("Hutton Orbital", "Alpha Centauri", null, null));

    /// <summary>Answers one held Spansh response and records the query it was given.</summary>
    private sealed class Held(string json) : IGalaxyService
    {
        public StationQuery? Last { get; private set; }

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Last = query;
            using var document = JsonDocument.Parse(json);
            return Task.FromResult(SpanshResponse.ReadStations(document));
        }

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

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static async Task<StationSearchResult> SearchAsync(string json, PowerplayPledge? pledge)
    {
        Assert.True(StationQuery.TryParse("Sol", "Frame Shift Drive", null, null, null, false, null, 5, out var query, out _));

        return await new StationPowerFilter(new Held(json), () => pledge)
            .FindStationsAsync(query, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnUnpledgedCommanderGetsTheOutpostOnly()
    {
        var result = await SearchAsync(AislingCarrierFirst, PowerplayPledge.Unpledged);

        Assert.Equal(["Hutton Orbital"], result.Stations.Select(station => station.Name));
        Assert.Equal(1, result.LeftOut);
    }

    [Fact]
    public async Task ACommanderPledgedToTheCarriersPowerGetsBoth()
    {
        var result = await SearchAsync(AislingCarrierFirst, new PowerplayPledge("Aisling Duval"));

        Assert.Equal(["Stronghold Carrier", "Hutton Orbital"], result.Stations.Select(station => station.Name));
        Assert.Equal(0, result.LeftOut);
    }

    [Fact]
    public async Task ACommanderPledgedToAnotherPowerGetsTheOutpostOnly()
    {
        var result = await SearchAsync(AislingCarrierFirst, new PowerplayPledge("Li Yong-Rui"));

        Assert.Equal(["Hutton Orbital"], result.Stations.Select(station => station.Name));
        Assert.Equal(1, result.LeftOut);
    }

    [Fact]
    public async Task AStrongholdCarrierOutsideAStrongholdSystemIsKept()
    {
        var json = Response(Row("Stronghold Carrier", "Alpha Centauri", "Aisling Duval", "Exploited"));

        var result = await SearchAsync(json, PowerplayPledge.Unpledged);

        Assert.Single(result.Stations);
        Assert.Equal(0, result.LeftOut);
    }

    [Fact]
    public async Task TheAnswerIsFilledFromRowsBeyondTheLimit()
    {
        var rows = Enumerable.Range(0, 3)
            .Select(_ => Row("Stronghold Carrier", "Alpha Centauri", "Aisling Duval", "Stronghold"))
            .Append(Row("Hutton Orbital", "Alpha Centauri", null, null))
            .ToArray();

        var held = new Held(Response(rows));
        Assert.True(StationQuery.TryParse("Sol", "Frame Shift Drive", null, null, null, false, null, 1, out var query, out _));

        var result = await new StationPowerFilter(held, () => PowerplayPledge.Unpledged)
            .FindStationsAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(["Hutton Orbital"], result.Stations.Select(station => station.Name));
        Assert.Equal(3, result.LeftOut);
        Assert.True(held.Last!.Size > 1);
    }
}
