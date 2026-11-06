using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class OnFootKillBondsAreSentToAFrontlineDeskTests
{
    private const string Desk = "Frontline Solutions";

    [Fact]
    public async Task WhenTheDeskSearchFailsNoOnFootBondIsDrawn()
    {
        var state = State();

        Assert.False(await DrawGates.FrontlineInReachAsync(new Galaxy { DeskSearchFails = true }, state, NullLogger.Instance, TestContext.Current.CancellationToken));
        Assert.True(await DrawGates.FrontlineInReachAsync(new Galaxy(), state, NullLogger.Instance, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithNoDeskInASystemAtWarNoOnFootBondIsDrawn()
    {
        Assert.False(await DrawGates.FrontlineInReachAsync(new Galaxy { HollowAtWar = false }, State(), NullLogger.Instance, TestContext.Current.CancellationToken));
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

    private static CommanderGameState State()
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

        return store.Active!;
    }

    /// <summary>
    /// Dyson's Hollow is at war and has Maren Anchorage with the desk and Plain Dock without it; Ossen's Lantern is at peace and has Quiet Port with the desk.
    /// </summary>
    private sealed class Galaxy : StubGalaxyService
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

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
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

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(12);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
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

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));
    }
}
