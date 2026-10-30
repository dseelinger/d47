using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class OnFootKillBondsAreSentToAFrontlineDeskTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Ground War", "premise": "A war is owed.", "want": "To finish it.",
         "stake": "Whether it can be paid for.", "turn": "The payer is the target.", "ending": "It is done."}
        """;

    private const string Desk = "Frontline Solutions";

    [Fact]
    public async Task AnOnFootBondAfterADockWithNoFrontlineDeskIsRefused()
    {
        var outcome = await Write(Beats("Dyson's Hollow", "Plain Dock"), new Galaxy());

        Assert.False(outcome.Succeeded);
        Assert.Contains("Objective 2 (The Fight) is a kill bond earned on foot", outcome.Refusal);
        Assert.Contains("Plain Dock has no Frontline Solutions in the galaxy search", outcome.Refusal);
    }

    [Fact]
    public async Task AnOnFootBondAfterADockAtADeskInASystemNotAtWarIsRefused()
    {
        var outcome = await Write(Beats("Ossen's Lantern", "Quiet Port"), new Galaxy());

        Assert.False(outcome.Succeeded);
        Assert.Contains("Objective 2 (The Fight) is a kill bond earned on foot", outcome.Refusal);
        Assert.Contains("Ossen's Lantern is not at war", outcome.Refusal);
    }

    [Fact]
    public async Task AnOnFootBondAfterADockAtADeskInASystemAtWarPasses()
    {
        var outcome = await Write(Beats("Dyson's Hollow", "Maren Anchorage"), new Galaxy());

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task AnOnFootBondThatDoesNotFollowADockIsRefused()
    {
        var outcome = await Write(Beats(null, null), new Galaxy());

        Assert.False(outcome.Succeeded);
        Assert.Contains("The objective before it is not a dock", outcome.Refusal);
    }

    [Fact]
    public async Task TheWriterIsHandedTheDeskStationsAndTheRule()
    {
        var provider = Provider(Beats("Dyson's Hollow", "Maren Anchorage"));

        await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("Stations with Frontline Solutions in a system at war, within reach, from the galaxy search, nearest first:", brief);
        Assert.Contains("- Maren Anchorage in Dyson's Hollow", brief);
        Assert.DoesNotContain("- Quiet Port in", brief);
        Assert.DoesNotContain("- Plain Dock in", brief);
        Assert.Contains("must come directly after a \"dock\" objective at one of these stations", brief);
        Assert.DoesNotContain("reached on foot at a settlement", brief);
    }

    [Fact]
    public async Task WithNoDeskInAWarTheWriterIsToldToWriteNoOnFootBondAndTheKindIsNotOffered()
    {
        var provider = Provider(Beats("Dyson's Hollow", "Maren Anchorage"));

        await Generator(provider, new Galaxy { HollowAtWar = false }).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("write no \"onfootbond\" objective", brief);
        Assert.DoesNotContain("Stations with Frontline Solutions in a system at war", brief);
        Assert.DoesNotContain("- \"onfootbond\":", brief);
    }

    [Fact]
    public async Task WhenTheDeskSearchFailsNoOnFootBondIsOffered()
    {
        var provider = Provider(Beats("Dyson's Hollow", "Maren Anchorage"));

        await Generator(provider, new Galaxy { DeskSearchFails = true }).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("write no \"onfootbond\" objective", brief);
        Assert.DoesNotContain("- \"onfootbond\":", brief);
    }

    [Fact]
    public void TheDrawLeavesOutOnFootBondsWhenNoDeskIsInReach()
    {
        var ranks = new RankState { TakenAt = DateTimeOffset.UnixEpoch, Standings = [new RankStanding("Combat", 0), new RankStanding("Soldier", 0)] };
        var ship = new ShipLoadout { Type = "cobramkiii", CargoCapacity = 64 };

        IReadOnlyList<DrawnActivity> Draw(uint seed, bool desk) =>
            ChapterDraw.Draw(CareerWeights.Even, ranks, ship, odyssey: true, [], null, false, CareerStatistics.Empty, 0, seed, frontlineInReach: desk);

        for (uint seed = 0; seed < 1000; seed++)
        {
            Assert.DoesNotContain(Draw(seed, desk: false), slot => slot.Kind == TriggerKind.OnFootBond);
        }

        Assert.Contains(Enumerable.Range(0, 1000).SelectMany(seed => Draw((uint)seed, desk: true)), slot => slot.Kind == TriggerKind.OnFootBond);
        Assert.Contains(Enumerable.Range(0, 1000).SelectMany(seed => Draw((uint)seed, desk: false)), slot => slot.Kind == TriggerKind.Signal);
    }

    private static async Task<AdventureOutcome> Write(string beats, Galaxy galaxy)
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(beats), Saying(Repairs.Replacing(beats, 2)), Saying(Repairs.Replacing(beats, 2)));

        return await Generator(provider, galaxy).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);
    }

    private static RoundScriptedLlmProvider Provider(string beats) => new(Saying(Spine), Saying(beats));

    /// <summary>A dock at <paramref name="station"/> in <paramref name="system"/> (or none when null), then the on-foot bond, then a rank.</summary>
    private static string Beats(string? system, string? station)
    {
        var dock = system is null
            ? string.Empty
            : $$"""{"title": "The Landing", "function": "setup", "kind": "dock", "reason": "The desk is here.", "system": "{{system}}", "station": "{{station}}", "body": null, "career": null, "rank": null, "line": "Dock."},""";

        return $$"""
            {"opening": "A war.", "reply": "Here it is.", "beats": [
              {{dock}}
              {"title": "The Fight", "function": "turn", "kind": "onfootbond", "system": null, "station": null, "body": null, "career": null, "rank": null, "count": 5, "faction": null, "line": "Go to the desk."},
              {"title": "The Ground War Ends", "function": "resolution", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "It is done."}
            ]}
            """;
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-01T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     """{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[3.68750,-44.62500,131.00000], "Docked":false }""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            store.Apply(parsed!);
        }

        var state = store.Active;

        return new AdventureGenerator(
            () => provider,
            () => null,
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => galaxy,
            () => null,
            null,
            null,
            NullLogger.Instance);
    }

    /// <summary>
    /// Dyson's Hollow is at war and has Maren Anchorage with the desk and Plain Dock without it; Ossen's Lantern is at peace and has Quiet Port with the desk.
    /// </summary>
    private sealed class Galaxy : IGalaxyService
    {
        private readonly List<StationSummary> _stations =
        [
            Station("Maren Anchorage", "Dyson's Hollow", 101, AdventureFixtures.Hollow, Desk),
            Station("Plain Dock", "Dyson's Hollow", 102, AdventureFixtures.Hollow),
            Station("Quiet Port", "Ossen's Lantern", 103, AdventureFixtures.Lantern, Desk),
        ];

        public bool HollowAtWar { get; init; } = true;

        public bool DeskSearchFails { get; init; }

        private static StationSummary Station(string name, string system, long marketId, long address, params string[] services) => new()
        {
            Name = name,
            SystemName = system,
            MarketId = marketId,
            SystemAddress = address,
            Type = "Coriolis Starport",
            Distance = 10,
            Services = services,
        };

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var systems = new[]
            {
                new SystemSummary { Name = "Oppi", SystemAddress = 3382387380970, Government = "Democracy", Distance = 5 },
                new SystemSummary { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 },
                new SystemSummary { Name = "Dyson's Hollow", SystemAddress = AdventureFixtures.Hollow, Government = "Anarchy", Distance = 5, AtWar = HollowAtWar },
            };

            if (query.Criteria.Any(criterion => criterion.Filter.Name == "government"))
            {
                return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, 0, []));
            }

            var named = systems.Where(system => string.Equals(system.Name, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase)).ToList();

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            if (query.Services.Contains(Desk) && DeskSearchFails)
            {
                throw new GalaxyUnavailableException("the service is down");
            }

            var found = _stations
                .Where(station => query.StationName is null
                    ? query.Services.All(service => station.Services.Contains(service))
                    : string.Equals(station.SystemName, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(station.Name, query.StationName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
