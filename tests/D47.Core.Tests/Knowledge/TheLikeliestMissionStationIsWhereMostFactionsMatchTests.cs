using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>find_mission_stations: the ranking and the answer, with no network (#1064).</summary>
public class TheLikeliestMissionStationIsWhereMostFactionsMatchTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public MissionStationQuery? LastQuery { get; private set; }

        public MissionStationScan Scan { get; set; } = new("Sol", 0, []);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;

            return Task.FromResult(Scan);
        }

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static readonly DateTimeOffset At = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] Board = ["Dock", "Missions", "Missions Generated", "Social Space"];

    private static StationSummary Station(string name, double arrival, string type = "Coriolis Starport", params string[] services) => new()
    {
        Name = name,
        SystemName = string.Empty,
        DistanceToArrival = arrival,
        Type = type,
        Services = services.Length == 0 ? Board : services,
    };

    private static MissionSystem System(string name, double distance, string economy, (string State, string Government)[] factions, params StationSummary[] stations) =>
        new(
            new SystemSummary
            {
                Name = name,
                Distance = distance,
                PrimaryEconomy = economy,
                Factions = [.. factions.Select((faction, i) => new FactionPresence($"{name} faction {i}", 0.1)
                {
                    State = faction.State,
                    Government = faction.Government,
                })],
            },
            stations);

    private static MissionStationQuery Query(string type)
    {
        Assert.True(MissionStationQuery.TryParse("Sol", MissionTypes.Resolve(type), null, out var query, out var failure), failure);

        return query;
    }

    [Fact]
    public void ADisableSearchFiltersOnItsStatesAndScoresTheHighTechEconomy()
    {
        var query = Query("disable");

        Assert.True(query.FiltersOnStates);
        Assert.Equal(["Civil Unrest", "Civil War", "Expansion", "Lockdown", "Retreat", "War"], query.FactionFilter);
        Assert.Equal(["High Tech"], query.Economies);
        Assert.False(query.OnFoot);
    }

    [Fact]
    public void ATypeWithNoStatesFiltersOnItsGovernmentsAndATypeWithNeitherOnNothing()
    {
        var name = new MissionName("mission_x", "Mission_X", "Combat", "x", "", "Prison Colony", "", "", "", "", []);

        Assert.True(MissionStationQuery.TryParse("Sol", [name], null, out var governed, out _));
        Assert.False(governed.FiltersOnStates);
        Assert.Equal(["Prison Colony"], governed.FactionFilter);

        Assert.Empty(Query("on foot heist").FactionFilter);
    }

    [Fact]
    public void StationsAreOrderedByScoreThenByDistanceAndAMatchingEconomyCountsOne()
    {
        var scan = new MissionStationScan("Sol", 3,
        [
            System("Near", 4, "Industrial", [("War", "Democracy"), ("None", "Anarchy")], Station("Near Port", 100)),
            System("Middle", 6, "High Tech", [("War", "Democracy"), ("Civil War", "Corporate")], Station("Middle Port", 900), Station("Middle Dock", 50)),
            System("Far", 9, "Industrial", [("War", "Democracy"), ("Civil War", "Corporate"), ("Lockdown", "Feudal")], Station("Far Port", 10)),
        ]);

        var ranked = MissionStationRanking.Rank(Query("disable"), scan, 5);

        Assert.Equal(["Middle Dock", "Middle Port", "Far Port", "Near Port"], ranked.Select(candidate => candidate.Station.Name));
        Assert.Equal([3, 3, 3, 1], ranked.Select(candidate => candidate.Score));
        Assert.Equal([2, 2, 3, 1], ranked.Select(candidate => candidate.MatchingStates));
        Assert.Equal("Middle", ranked[0].Station.SystemName);
        Assert.Equal(6, ranked[0].Station.Distance);
    }

    [Fact]
    public void AGovernmentMatchScoresOneForEachFactionUnderIt()
    {
        var name = new MissionName("mission_x", "Mission_X", "Combat", "x", "", "Prison Colony", "", "", "", "", []);
        Assert.True(MissionStationQuery.TryParse("Sol", [name], null, out var query, out _));

        var scan = new MissionStationScan("Sol", 1,
        [
            System("Gaol", 5, "Industrial", [("Boom", "Prison Colony"), ("None", "Prison Colony"), ("None", "Democracy")], Station("Gaol Port", 10)),
        ]);

        var candidate = Assert.Single(MissionStationRanking.Rank(query, scan, 5));

        Assert.Equal(2, candidate.MatchingGovernments);
        Assert.Equal(2, candidate.Score);
    }

    [Fact]
    public void AStationWithoutAMissionBoardIsDropped()
    {
        var scan = new MissionStationScan("Sol", 1,
        [
            System("Here", 5, "Industrial", [("War", "Democracy")],
                Station("Board", 10),
                Station("X1X-11X", 5, "Drake-Class Carrier", "Dock", "Social Space"),
                Station("Generated Only", 1, "Outpost", "Dock", "Missions Generated")),
        ]);

        Assert.Equal(["Board"], MissionStationRanking.Rank(Query("disable"), scan, 5).Select(candidate => candidate.Station.Name));
    }

    [Fact]
    public void AnOnFootTypeNeedsASocialSpaceUnlessTheStationIsASettlement()
    {
        var scan = new MissionStationScan("Sol", 1,
        [
            System("Here", 5, "Industrial", [],
                Station("Concourse", 10),
                Station("No Concourse", 20, "Outpost", "Dock", "Missions"),
                Station("Settlement", 30, "Settlement", "Missions"),
                Station("Surface Settlement", 40, "Surface Settlement", "Missions")),
        ]);

        var query = Query("on foot heist");

        Assert.True(query.OnFoot);
        Assert.Equal(
            ["Concourse", "Settlement", "Surface Settlement"],
            MissionStationRanking.Rank(query, scan, 5).Select(candidate => candidate.Station.Name));
        Assert.Equal(4, MissionStationRanking.Rank(Query("disable"), scan, 5).Count);
    }

    private static CapabilityRegistry Build(FakeGalaxy galaxy, CommanderGameState? state = null)
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        return CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "Sol", settings, gameState: () => state)]);
    }

    private static Task<ToolResult> Ask(CapabilityRegistry registry, string type) =>
        registry.InvokeAsync(
            MissionStations.Tool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["mission_type"] = type }),
            TestContext.Current.CancellationToken);

    private static void SaysLikeliest(string said)
    {
        Assert.Contains("likeliest", said, StringComparison.Ordinal);
        Assert.DoesNotContain("offer", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheAnswerNamesEachStationWithItsSystemDistanceAndMatchingFactions()
    {
        var galaxy = new FakeGalaxy
        {
            Scan = new MissionStationScan("Sol", 40,
            [
                System("Barnard's Star", 6, "Refinery", [("War", "Corporate"), ("Civil Unrest", "Confederacy")], Station("Miller Depot", 10)),
            ]),
        };

        var result = await Ask(Build(galaxy), "disable");

        Assert.False(result.IsError, result.Content);
        Assert.Contains("Miller Depot in Barnard's Star, 6 light years, 2 factions in a matching state.", result.Content, StringComparison.Ordinal);
        Assert.Contains("Ranked over the nearest 1 of the 40 systems that matched.", result.Content, StringComparison.Ordinal);
        Assert.Equal("Sol", galaxy.LastQuery!.ReferenceSystem);
        Assert.Equal(MissionStationQuery.DefaultDistance, galaxy.LastQuery.MaxDistance);
        SaysLikeliest(result.Content);
    }

    [Fact]
    public async Task NothingFoundStillSaysLikeliest()
    {
        var result = await Ask(Build(new FakeGalaxy()), "disable");

        Assert.False(result.IsError, result.Content);
        Assert.Equal(
            "No system within 50 light years of Sol has a faction in Civil Unrest, Civil War, Expansion, Lockdown, Retreat or War, "
            + "so I have no likeliest station for disable missions there.",
            result.Content);
        SaysLikeliest(result.Content);
    }

    [Fact]
    public async Task ARankTheCommanderLacksIsSaidBeforeTheSearch()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));
        state.Apply(RankEvent("\"Empire\":0,\"Federation\":3"));

        var galaxy = new FakeGalaxy();
        var result = await Ask(Build(galaxy, state), "Mission_Assassinate_RankEmp");

        Assert.StartsWith("These need an Empire navy rank, and you hold none. ", result.Content, StringComparison.Ordinal);
        Assert.NotNull(galaxy.LastQuery);
        SaysLikeliest(result.Content);

        var some = await Ask(Build(new FakeGalaxy(), state), "assassination");

        Assert.StartsWith("Some of these need an Empire navy rank, and you hold none. ", some.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Federation navy rank", some.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEngineerTypeNamesEngineerBasesAndSearchesNothing()
    {
        var galaxy = new FakeGalaxy();
        var result = await Ask(Build(galaxy), "Mission_Courier_Engineer");

        Assert.False(result.IsError, result.Content);
        Assert.Contains("engineer bases", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
        SaysLikeliest(result.Content);
    }

    [Fact]
    public void TheNearestEngineerBasesAreNamedWithTheirDistance()
    {
        var deciat = EngineerDirectory.All.First(engineer => engineer.System == "Deciat");
        var said = MissionStations.DescribeEngineers("courier", deciat.Position);

        Assert.Contains($"The nearest to you: {deciat.Name} at {deciat.Where}, 0 light years; ", said, StringComparison.Ordinal);
        SaysLikeliest(said);
    }

    [Fact]
    public async Task AnUnknownTypeIsAnsweredWithTheNearestNames()
    {
        var galaxy = new FakeGalaxy();
        var result = await Ask(Build(galaxy), "disabel");

        Assert.StartsWith("I do not know a mission type called disabel.", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
    }

    private static JournalEvent RankEvent(string fields)
    {
        var text = $"{{ \"timestamp\":\"{At.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"Rank\", {fields} }}";

        return new JournalEvent(At, "Rank", JsonDocument.Parse(text).RootElement);
    }
}
