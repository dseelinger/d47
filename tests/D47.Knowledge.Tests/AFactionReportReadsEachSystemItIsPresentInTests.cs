using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class AFactionReportReadsEachSystemItIsPresentInTests
{
    private const string Faction = FactionAnswer.Faction;

    private static async Task<(FactionReport Report, FactionAnswer Answer)> Report(string recorded)
    {
        var answer = new FactionAnswer(recorded);
        using var service = new SpanshGalaxyService(NullLogger<SpanshGalaxyService>.Instance, new HttpClient(answer));

        return (await service.FactionAsync(Faction, TestContext.Current.CancellationToken), answer);
    }

    [Fact]
    public void TheRequestFiltersOnTheFactionsExactNameSortedByNameAFullPageAtATime()
    {
        var body = JsonDocument.Parse(SpanshRequest.Faction(Faction, 1)).RootElement;

        var presence = Assert.Single(body.GetProperty("filters").GetProperty("minor_faction_presences").EnumerateArray());
        Assert.Equal([Faction], presence.GetProperty("name").GetProperty("value").EnumerateArray().Select(v => v.GetString()));

        var sort = Assert.Single(body.GetProperty("sort").EnumerateArray());
        Assert.Equal("asc", sort.GetProperty("name").GetProperty("direction").GetString());

        Assert.Equal(500, body.GetProperty("size").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.False(body.TryGetProperty("reference_system", out _));
    }

    [Fact]
    public async Task TheReportNamesTheFactionItsGovernmentAndItsAllegiance()
    {
        var (report, answer) = await Report(FactionAnswer.Recorded());

        Assert.Equal(Faction, report.Name);
        Assert.Equal("Anarchy", report.Government);
        Assert.Equal("Independent", report.Allegiance);
        Assert.Equal(["Eurybia", "Hyades Sector RE-G b11-3", "LPM 173"], report.Systems.Select(system => system.Name));
        Assert.Single(answer.Bodies);
    }

    [Fact]
    public async Task EachSystemCarriesTheFactionsInfluenceStatesAndControl()
    {
        var (report, _) = await Report(FactionAnswer.Recorded());

        var eurybia = report.Systems[0];
        Assert.Equal(1458309141194, eurybia.SystemAddress);
        Assert.Equal(145173, eurybia.Population);
        Assert.Equal(0.256256, eurybia.Influence);
        Assert.Equal(["Boom"], eurybia.ActiveStates);
        Assert.Empty(eurybia.PendingStates);
        Assert.Empty(eurybia.RecoveringStates);
        Assert.True(eurybia.Controls);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 11, 21, 42, TimeSpan.Zero), eurybia.ReportedAt);

        var hyades = report.Systems[1];
        Assert.False(hyades.Controls);
        Assert.Equal(["War"], hyades.ActiveStates);

        Assert.True(report.Systems[2].Controls);
        Assert.Equal(0.736318, report.Systems[2].Influence);
    }

    [Fact]
    public async Task TheOtherFactionsInASystemCarryTheirInfluenceAndStates()
    {
        var (report, _) = await Report(FactionAnswer.Recorded());

        var hyades = report.Systems[1];
        Assert.DoesNotContain(hyades.Others, other => other.Name == Faction);
        Assert.Equal(4, hyades.Others.Count);

        var opponent = Assert.Single(hyades.Others, other => other.ActiveStates.Contains("War"));
        Assert.Equal("Booty Bay Butchers", opponent.Name);
        Assert.Equal(0.160481, opponent.Influence);
        Assert.Equal(["Expansion", "War"], opponent.ActiveStates);

        var consulate = Assert.Single(report.Systems[0].Others, other => other.Name == "Eurybia Empire Consulate");
        Assert.Equal(["Civil Unrest", "Election"], consulate.ActiveStates);
    }

    [Fact]
    public async Task PendingAndRecoveringStatesAreRead()
    {
        var recorded = JsonNode.Parse(FactionAnswer.Recorded())!;
        var lpm = recorded["results"]![2]!;
        var own = lpm["minor_faction_presences"]!.AsArray().Single(presence => (string?)presence!["name"] == Faction)!;
        own["pending_states"] = new JsonArray("Election");
        own["recovering_states"] = new JsonArray("War");
        var neighbour = lpm["minor_faction_presences"]!.AsArray().Single(presence => (string?)presence!["name"] == "Booty Bay Butchers")!;
        neighbour["pending_states"] = new JsonArray("Election");

        var (report, _) = await Report(recorded.ToJsonString());

        var system = report.Systems[2];
        Assert.Equal(["Election"], system.PendingStates);
        Assert.Equal(["War"], system.RecoveringStates);
        Assert.Equal(["Election"], system.Others.Single(other => other.Name == "Booty Bay Butchers").PendingStates);
    }

    [Fact]
    public async Task OnlyTheStationsTheFactionControlsAreListed()
    {
        var (report, _) = await Report(FactionAnswer.Recorded());

        Assert.Equal(22, report.Stations.Count);
        Assert.DoesNotContain(report.Stations, station => station.Type == "Drake-Class Carrier");
        Assert.DoesNotContain(report.Stations, station => station.Name is "Nowak's Folly" or "Prelude to Victory");

        Assert.Contains(new FactionStation("Awyra Flirble", "Coriolis Starport", PadSize.Large, 321.462203, "Eurybia"), report.Stations);
        Assert.Contains(new FactionStation("Heinrich's Expedition", "Settlement", PadSize.Medium, 1676.251161, "Eurybia"), report.Stations);
        Assert.Contains(new FactionStation("Sandoval's Jurisdiction", "Settlement", PadSize.Small, 1674.527113, "Eurybia"), report.Stations);
        Assert.Contains(new FactionStation("Adams Installation", null, null, 315.283078, "Eurybia"), report.Stations);
        Assert.Contains(new FactionStation("Ochoa City", "Dodec Starport", PadSize.Large, 1008.162388, "Hyades Sector RE-G b11-3"), report.Stations);
        Assert.Contains(new FactionStation("Lockwood Legacy", "Coriolis Starport", PadSize.Large, 5.136895, "LPM 173"), report.Stations);
    }

    [Fact]
    public async Task BodiesAreNotRead()
    {
        var recorded = JsonNode.Parse(FactionAnswer.Recorded())!;

        foreach (var result in recorded["results"]!.AsArray())
        {
            result!["bodies"] = "not a list of bodies";
        }

        var (report, _) = await Report(recorded.ToJsonString());
        var (whole, _) = await Report(FactionAnswer.Recorded());

        Assert.Equal(whole.Systems.Count, report.Systems.Count);
        Assert.Equal(whole.Stations, report.Stations);
    }
}
