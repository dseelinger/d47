using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public sealed class AHomeSystemSearchStaysWithinItsCallBudgetTests
{
    [Fact]
    public async Task AtMostTwelveSystemsHaveTheirBodiesRead()
    {
        var galaxy = new Galaxy(30);

        var result = await new HomeSystemGatherer(galaxy).SuggestAsync(
            new HomeSystemRequest { CurrentSystem = "Sol", Weights = CareerWeights.Even }, CancellationToken.None);

        Assert.True(galaxy.BodyCalls is > 0 and <= 12);
        Assert.Equal(3, result.Count);
        Assert.All(result, suggestion => Assert.Equal(HomeSystemReason.Interests, suggestion.Reason));
    }

    [Fact]
    public async Task AClaimedSystemNeedsNoStation()
    {
        var galaxy = new Galaxy(5);
        var claim = new ColonisationClaim("Mine", DateTimeOffset.UnixEpoch) { SystemAddress = 99, BeaconDeployedAt = DateTimeOffset.UnixEpoch };

        var result = await new HomeSystemGatherer(galaxy).SuggestAsync(
            new HomeSystemRequest { CurrentSystem = "Sol", Weights = CareerWeights.Even, Claims = [claim] }, CancellationToken.None);

        Assert.Equal("Mine", Assert.Single(result).Name);
        Assert.Equal(0, galaxy.StationCalls);
    }

    [Fact]
    public async Task SystemsWithoutAFittingPadAreLeftOut()
    {
        var galaxy = new Galaxy(4) { LargePads = false };

        var result = await new HomeSystemGatherer(galaxy).SuggestAsync(
            new HomeSystemRequest { CurrentSystem = "Sol", Weights = CareerWeights.Even, Pad = PadSize.Large }, CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, galaxy.BodyCalls);
    }

    [Fact]
    public async Task ASystemInBothSetsIsOfferedFirstAmongSingleSetSystems()
    {
        var galaxy = new Galaxy(0)
        {
            Squadron = [new("S1", 1, 1), new("S2", 2, 2), new("S3", 3, 80)],
            Power = [new("S3", 3, 80), new("S4", 4, 4)],
        };

        var result = await new HomeSystemGatherer(galaxy).SuggestAsync(
            new HomeSystemRequest { CurrentSystem = "Sol", Weights = CareerWeights.Even, Power = "Aisling Duval", SquadronFaction = "Faction" },
            CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal("S3", result[0].Name);
        Assert.Equal(HomeSystemReason.SquadronAndPower, result[0].Reason);
    }

    [Fact]
    public async Task APowerWithoutASquadronOffersItsCapitalAndItsStrongholds()
    {
        var galaxy = new Galaxy(0) { Power = [new("S1", 1, 10), new("S2", 2, 20)] };

        var result = await new HomeSystemGatherer(galaxy).SuggestAsync(
            new HomeSystemRequest { CurrentSystem = "Sol", Weights = CareerWeights.Even, Power = "Aisling Duval" },
            CancellationToken.None);

        Assert.Equal(["S1", "S2", "Cubeo"], result.Select(suggestion => suggestion.Name));
        Assert.All(result, suggestion => Assert.Equal(HomeSystemReason.Power, suggestion.Reason));
    }

    private sealed record Place(string Name, long Address, double Distance);

    private sealed class Galaxy(int systems) : IGalaxyService
    {
        public int BodyCalls { get; private set; }

        public int StationCalls { get; private set; }

        public bool LargePads { get; init; } = true;

        public IReadOnlyList<Place> Squadron { get; init; } = [];

        public IReadOnlyList<Place> Power { get; init; } = [];

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            bool Filtered(string name) => query.Criteria.Any(criterion => criterion.Filter.Name == name);

            IEnumerable<Place> places = Filtered("controlling_faction") ? Squadron
                : Filtered("power") ? Power
                : query.ReferenceSystem == "Cubeo" ? [new Place("Cubeo", 500, 0)]
                : Enumerable.Range(1, systems).Select(n => new Place($"S{n}", n, n));

            return Task.FromResult(new GalaxySearchResult(
                query.ReferenceSystem,
                0,
                [.. places.Select(place => new SystemSummary { Name = place.Name, SystemAddress = place.Address, Distance = place.Distance })]));
        }

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            StationCalls++;
            var system = query.ReferenceSystem!;

            return Task.FromResult(new StationSearchResult(
                system,
                1,
                [new StationSummary { Name = system + " Port", SystemName = system, Type = "Orbis", SmallPads = 1, MediumPads = 1, LargePads = LargePads ? 1 : 0, Services = ["Market"] }]));
        }

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken)
        {
            BodyCalls++;
            return Task.FromResult<IReadOnlyList<BodySummary>>([new BodySummary { Name = "A", SystemName = "any" }]);
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(50);

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
