using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AStationsTraderTypeIsCorrectedTests
{
    private const long BernoulliCity = 3226424832;

    private sealed class Spansh : StubGalaxyService, IGalaxyService
    {
        public List<StationQuery> Asked { get; } = [];

        public int Running { get; private set; }

        public int MostRunning { get; private set; }

        public Func<StationQuery, StationSearchResult> Answer { get; set; } = _ => new(null, 0, []);

        public bool FailTheSecond { get; set; }

        public IReadOnlyList<OutfitterStation> Outfitters { get; set; } = [];

        public override async Task<StationSearchResult> FindStationsAsync(
            StationQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            Running++;
            MostRunning = Math.Max(MostRunning, Running);

            try
            {
                await Task.Yield();

                if (FailTheSecond && Asked.Count > 1)
                {
                    throw new HttpRequestException("refused");
                }

                return Answer(query);
            }
            finally
            {
                Running--;
            }
        }

        public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
            IReadOnlyList<OutfittingItem> items,
            string referenceSystem,
            double maxDistance,
            PadSize? minPad,
            int? maxReportAgeHours,
            CancellationToken cancellationToken) =>
            Task.FromResult(Outfitters);
    }

    private static StationSummary Station(string name, long? marketId, double distance, string? type) =>
        new() { Name = name, SystemName = "Machadan", MarketId = marketId, Distance = distance, TraderType = type };

    private static string? Known(long id) => id == BernoulliCity ? "Raw" : null;

    private static TraderTypeCorrection Over(Spansh inner) => new(inner, Known);

    /// <summary>Spansh files Bernoulli City as Manufactured; the typed answers hold what Spansh filters on.</summary>
    private static Spansh MachadanAsSpanshHasIt(bool more = false, bool rawNearby = false)
    {
        var bernoulli = Station("Bernoulli City", BernoulliCity, 0, "Manufactured");
        var far = Station("Far Raw", 1, 23.9, "Raw");
        var near = Station("Near Raw", 2, 3, "Raw");

        return new Spansh
        {
            Answer = query =>
                query.TraderType is null
                    ? new(null, 3, rawNearby ? [bernoulli, near, far] : [bernoulli, far])
                    : query.TraderType == "Manufactured"
                        ? new(null, 1, [bernoulli]) { More = more }
                        : new(null, rawNearby ? 2 : 1, rawNearby ? [near, far] : [far]) { More = more },
        };
    }

    [Fact]
    public async Task ARawSearchReturnsTheStationTheTableCallsRawFirst()
    {
        var service = Over(MachadanAsSpanshHasIt());

        var result = await service.FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Raw", null, 5), TestContext.Current.CancellationToken);

        var first = result.Stations[0];
        Assert.Equal("Bernoulli City", first.Name);
        Assert.Equal("Raw", first.TraderType);
        Assert.Equal(0, first.Distance);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task AManufacturedSearchDropsIt()
    {
        var service = Over(MachadanAsSpanshHasIt());

        var result = await service.FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Manufactured", null, 5), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(result.Stations, station => station.MarketId == BernoulliCity);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task ARawSearchWithNothingTypedRawNearbyStillFindsIt()
    {
        var inner = new Spansh
        {
            Answer = query => query.TraderType is null
                ? new(null, 1, [Station("Bernoulli City", BernoulliCity, 0, "Manufactured")])
                : new(null, 0, []),
        };

        var result = await Over(inner).FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Raw", null, 5), TestContext.Current.CancellationToken);

        Assert.Equal("Bernoulli City", Assert.Single(result.Stations).Name);
    }

    [Fact]
    public async Task AStationBeyondTheFirstAnswersReachIsNotAddedWhileThereIsMore()
    {
        var inner = MachadanAsSpanshHasIt(more: true, rawNearby: false);
        inner.Answer = query => query.TraderType is null
            ? new(null, 2, [Station("Bernoulli City", BernoulliCity, 30, "Manufactured")])
            : new(null, 5, [Station("Far Raw", 1, 23.9, "Raw")]) { More = true };

        var result = await Over(inner).FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Raw", null, 5), TestContext.Current.CancellationToken);

        Assert.Equal(["Far Raw"], result.Stations.Select(station => station.Name));
        Assert.Single(inner.Asked, query => query.TraderType is null && query.Size == GalaxyQuery.MaxSize && query.IsAboutTraders);
    }

    [Fact]
    public async Task ThePageIsSortedByDistance()
    {
        var result = await Over(MachadanAsSpanshHasIt(rawNearby: true)).FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Raw", null, 5), TestContext.Current.CancellationToken);

        Assert.Equal(["Bernoulli City", "Near Raw", "Far Raw"], result.Stations.Select(station => station.Name));
    }

    [Fact]
    public async Task ALaterPageIsCorrectedAndFilteredWithNothingAdded()
    {
        var inner = MachadanAsSpanshHasIt();

        var result = await Over(inner).FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Raw", null, 5) with { Page = 2 }, TestContext.Current.CancellationToken);

        Assert.Single(inner.Asked);
        Assert.Equal(["Far Raw"], result.Stations.Select(station => station.Name));
    }

    [Fact]
    public async Task ASortedSearchIsCorrectedAndFilteredWithNothingAdded()
    {
        var inner = MachadanAsSpanshHasIt();
        var sorted = StationQuery.ForTrader("Machadan", "Raw", null, 5) with { Sort = new SearchSort(new SearchSortField("updated", "updated_at", true, "Oldest", "Newest"), false) };

        var result = await Over(inner).FindStationsAsync(sorted, TestContext.Current.CancellationToken);

        Assert.Single(inner.Asked);
        Assert.Equal(["Far Raw"], result.Stations.Select(station => station.Name));
    }

    [Fact]
    public async Task AStationWithNoKnownTypeKeepsTheServicesType()
    {
        var inner = new Spansh
        {
            Answer = _ => new(null, 2, [Station("Unlisted", 99, 1, "Encoded"), Station("No id", null, 2, "Raw")]),
        };

        var result = await Over(inner).FindStationsAsync(
            StationQuery.ForTrader("Machadan", null, null, 5), TestContext.Current.CancellationToken);

        Assert.Equal(["Encoded", "Raw"], result.Stations.Select(station => station.TraderType));
    }

    [Fact]
    public async Task AFailedSecondRequestLeavesTheFirstAnswerCorrectedAndFiltered()
    {
        var inner = MachadanAsSpanshHasIt();
        inner.FailTheSecond = true;

        var result = await Over(inner).FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Raw", null, 5), TestContext.Current.CancellationToken);

        Assert.Equal(["Far Raw"], result.Stations.Select(station => station.Name));
    }

    [Fact]
    public async Task OutfittersCarryTheCorrectedType()
    {
        var inner = new Spansh
        {
            Outfitters = [new OutfitterStation(Station("Bernoulli City", BernoulliCity, 0, "Manufactured"), [])],
        };

        var found = await Over(inner).FindOutfittersAsync([], "Machadan", 30, null, null, TestContext.Current.CancellationToken);

        Assert.Equal("Raw", Assert.Single(found).Station.TraderType);
    }

    [Fact]
    public async Task TheTwoRequestsRunOneAfterTheOther()
    {
        var inner = MachadanAsSpanshHasIt();

        await Over(inner).FindStationsAsync(
            StationQuery.ForTrader("Machadan", "Raw", null, 5), TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.Asked.Count);
        Assert.Equal(1, inner.MostRunning);
    }
}
