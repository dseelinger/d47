using D47.Core.Adventures;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class ADockBeatIsOnlyWhereTheShipFitsTests
{
    private const long Hollow = 3382387380971;

    [Fact]
    public async Task AMediumShipIsRefusedAStationWithOnlySmallPads()
    {
        var resolution = await ResolveAsync(new StationSummary { Name = "Pebble Dock", SystemName = "Dyson's Hollow", MarketId = 1, Type = "Outpost", SmallPads = 4, MediumPads = 0, LargePads = 0 }, PadSize.Medium);

        Assert.False(resolution.Succeeded);
        Assert.Contains("Pebble Dock", resolution.Refusal);
        Assert.Contains("medium pad", resolution.Refusal);
    }

    [Fact]
    public async Task AMediumShipDocksWhereThereIsALargePad()
    {
        var resolution = await ResolveAsync(new StationSummary { Name = "Maren Anchorage", SystemName = "Dyson's Hollow", MarketId = 2, Type = "Coriolis Starport", SmallPads = 0, MediumPads = 0, LargePads = 2 }, PadSize.Medium);

        Assert.True(resolution.Succeeded);
    }

    [Fact]
    public async Task AStationWithUnknownPadCountsIsNotRefused()
    {
        var resolution = await ResolveAsync(new StationSummary { Name = "Mystery Port", SystemName = "Dyson's Hollow", MarketId = 3, Type = "Coriolis Starport" }, PadSize.Large);

        Assert.True(resolution.Succeeded);
    }

    [Fact]
    public void AStationListsTheSizesItHas()
    {
        var station = new StationSummary { Name = "Pebble Dock", SystemName = "Dyson's Hollow", SmallPads = 2, MediumPads = 0, LargePads = 1 };

        Assert.Equal([PadSize.Small, PadSize.Large], station.KnownPads);
        Assert.True(station.Admits(PadSize.Medium));
    }

    [Fact]
    public async Task AStationPastTheServicesPageIsFoundByName()
    {
        var stations = Enumerable.Range(1, 25)
            .Select(i => new StationSummary { Name = $"Dock {i}", SystemName = "Dyson's Hollow", MarketId = i, Type = "Coriolis Starport" })
            .ToArray();

        var resolution = await new AdventureResolver(new Galaxy(stations))
            .ResolveAsync(TriggerKind.Dock, "Dyson's Hollow", "Dock 25", null, "Beat 1", null, CancellationToken.None);

        Assert.True(resolution.Succeeded);
    }

    private static Task<Resolution> ResolveAsync(StationSummary station, PadSize needs) =>
        new AdventureResolver(new Galaxy([station])).ResolveAsync(TriggerKind.Dock, "Dyson's Hollow", station.Name, null, "Beat 1", needs, CancellationToken.None);

    private sealed class Galaxy(StationSummary[] all) : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(
                query.ReferenceSystem, 1, [new SystemSummary { Name = "Dyson's Hollow", SystemAddress = Hollow, Distance = 0 }]));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(1);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(
                query.ReferenceSystem,
                1,
                [.. all.Where(station => query.StationName is null || station.Name.Contains(query.StationName, StringComparison.OrdinalIgnoreCase)).Take(query.Size)]));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
