using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>find_mission_stations' request body and ranking, against shapes probed on 2026-10-10 (#1064).</summary>
public class AMissionStationSearchFiltersEveryFactionPresentTests
{
    private static readonly string[] DisableStates = ["Civil Unrest", "Civil War", "Expansion", "Lockdown", "Retreat", "War"];

    private static MissionName Name(string family, string state = "", string government = "", string economy = "") =>
        new(family.ToLowerInvariant(), family, "Combat", "x", state, government, economy, "", "", "", []);

    private static MissionStationQuery Query(params MissionName[] names)
    {
        Assert.True(MissionStationQuery.TryParse("Sol", names, 30, out var query, out var failure), failure);

        return query;
    }

    private static MissionStationQuery Disable() =>
        Query([.. DisableStates.Select(state => Name("Mission_Disable", state)), Name("Mission_Disable", economy: "High Tech")]);

    private static JsonElement Body(MissionStationQuery query) =>
        JsonDocument.Parse(SpanshRequest.MissionStations(query)).RootElement;

    private static string[] Values(JsonElement filter) =>
        [.. filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()!)];

    [Fact]
    public void StatesAreOnePresenceFilterSoAnyFactionPresentMatches()
    {
        var presences = Body(Disable()).GetProperty("filters").GetProperty("minor_faction_presences");

        Assert.Equal(JsonValueKind.Array, presences.ValueKind);

        var presence = Assert.Single(presences.EnumerateArray());

        Assert.Equal(DisableStates, Values(presence.GetProperty("state")));
        Assert.False(presence.TryGetProperty("government", out _));
    }

    [Fact]
    public void GovernmentsAreTheFilterOnlyWhereThereAreNoStates()
    {
        var presence = Assert.Single(Body(Query(Name("Mission_X", government: "Prison Colony")))
            .GetProperty("filters").GetProperty("minor_faction_presences").EnumerateArray());

        Assert.Equal(["Prison Colony"], Values(presence.GetProperty("government")));
        Assert.False(presence.TryGetProperty("state", out _));
    }

    [Fact]
    public void ATypeWithNeitherSendsNoFactionFilter()
    {
        Assert.False(Body(Query(Name("Mission_OnFoot_Heist"))).GetProperty("filters").TryGetProperty("minor_faction_presences", out _));
    }

    [Fact]
    public void TheDistanceIsARangeAndTheScanIsNearestFirstFromTheReference()
    {
        var body = Body(Disable());
        var distance = body.GetProperty("filters").GetProperty("distance");

        Assert.Equal("0", distance.GetProperty("min").GetString());
        Assert.Equal("30", distance.GetProperty("max").GetString());
        Assert.Equal("asc", body.GetProperty("sort")[0].GetProperty("distance").GetProperty("direction").GetString());
        Assert.Equal(MissionStationQuery.ScanSize, body.GetProperty("size").GetInt32());
        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
    }

    [Fact]
    public void TheResponseYieldsEveryFactionAndEveryStationInEachSystem()
    {
        using var document = Fixture.Json("spansh-search-mission-states-disable-near-sol.json");
        var scan = SpanshResponse.ReadMissionStations(document);

        Assert.Equal("Sol", scan.Reference);
        Assert.Equal(138, scan.Total);
        Assert.Equal(6, scan.Systems.Count);

        var barnard = scan.Systems.Single(system => system.System.Name == "Barnard's Star");

        Assert.Equal(7, barnard.System.Factions.Count);
        Assert.Equal("Refinery", barnard.System.PrimaryEconomy);

        var miller = barnard.Stations.Single(station => station.Name == "Miller Depot");

        Assert.Equal("Orbis Starport", miller.Type);
        Assert.Contains("Missions", miller.Services);
        Assert.Contains("Social Space", miller.Services);
        Assert.Equal("Barnard's Star", miller.SystemName);
        Assert.Equal(barnard.System.Distance, miller.Distance);
        Assert.True(miller.LargePads > 0);
    }

    [Fact]
    public void TheFixtureRanksBarnardsStarFirstAndDropsEveryCarrier()
    {
        using var document = Fixture.Json("spansh-search-mission-states-disable-near-sol.json");
        var scan = SpanshResponse.ReadMissionStations(document);
        var all = MissionStationRanking.Rank(Disable(), scan, int.MaxValue);

        Assert.Contains(scan.Systems.SelectMany(system => system.Stations), station => station.Type == "Drake-Class Carrier");
        Assert.DoesNotContain(all, candidate => candidate.Station.Type is "Drake-Class Carrier" or null);
        Assert.DoesNotContain(all, candidate => candidate.Station.Name == "Bakshi Manufacturing Complex");

        var top = all.Take(MissionStationQuery.Size).ToList();

        Assert.All(top, candidate => Assert.Equal("Barnard's Star", candidate.Station.SystemName));
        Assert.All(top, candidate => Assert.Equal(4, candidate.MatchingStates));

        var systems = all.Select(candidate => candidate.Station.SystemName).Distinct().ToList();

        Assert.Equal(["Barnard's Star", "Wolf 359", "Ross 154", "Alpha Centauri", "Sirius", "Yin Sector CL-Y d127"], systems);
    }

    [Fact]
    public void AnOnFootRankingKeepsSettlementsAndDropsAStationWithNoSocialSpace()
    {
        using var document = Fixture.Json("spansh-search-mission-states-disable-near-sol.json");
        var scan = SpanshResponse.ReadMissionStations(document);
        var all = MissionStationRanking.Rank(Query(Name("Mission_OnFoot_Heist")), scan, int.MaxValue);

        Assert.Contains(all, candidate => candidate.Station.Type == "Settlement");
        Assert.Contains(all, candidate => candidate.Station.Name == "Miller Depot");
        Assert.DoesNotContain(all, candidate => candidate.Station.Name == "Stronghold Carrier");
    }
}
