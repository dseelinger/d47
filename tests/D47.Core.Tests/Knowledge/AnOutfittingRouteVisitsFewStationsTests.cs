using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AnOutfittingRouteVisitsFewStationsTests
{
    private static OutfittingItem Item(string text)
    {
        Assert.True(OutfittingItem.TryParse(text, out var item, out var failure), failure);

        return item;
    }

    private static OutfitterStation Station(string name, double distance, StarPosition? at, params string[] sells) =>
        new(
            new StationSummary { Name = name, SystemName = $"{name} system", MarketId = name.GetHashCode(), Distance = distance, Position = at },
            [.. sells.Select(Item)]);

    private static readonly string[] Wanted = ["6A Fuel Scoop", "5A Frame Shift Drive", "Python MkII"];

    [Fact]
    public void OneStationThatSellsEverythingIsTheWholeRoute()
    {
        var route = OutfittingRoutePlanner.Plan(
            [.. Wanted.Select(Item)],
            [
                Station("Near", 5, null, "6A Fuel Scoop"),
                Station("All", 20, null, "6A Fuel Scoop", "5A Frame Shift Drive", "Python MkII"),
            ],
            "Sol");

        var stop = Assert.Single(route.Stops);
        Assert.Equal("All", stop.Station);
        Assert.Equal(3, stop.Items.Count);
        Assert.Empty(route.NotFound);
    }

    [Fact]
    public void ACoverThatNeedsThreeStationsOrdersThemNearestNext()
    {
        var route = OutfittingRoutePlanner.Plan(
            [.. Wanted.Select(Item)],
            [
                Station("Far", 40, new StarPosition(0, 0, 40), "Python MkII"),
                Station("Middle", 20, new StarPosition(0, 0, 20), "5A Frame Shift Drive"),
                Station("Close", 10, new StarPosition(0, 0, 10), "6A Fuel Scoop"),
            ],
            "Sol",
            StarPosition.Origin);

        Assert.Equal(["Close", "Middle", "Far"], route.Stops.Select(stop => stop.Station));
        Assert.Equal(10, route.Stops[0].Distance);
        Assert.Equal(10, route.Stops[1].Distance);
        Assert.Equal(20, route.Stops[2].Distance);
    }

    [Fact]
    public void TheNearerStationWinsATieOnItemsCovered()
    {
        var route = OutfittingRoutePlanner.Plan(
            [Item("6A Fuel Scoop")],
            [Station("Far", 30, null, "6A Fuel Scoop"), Station("Near", 3, null, "6A Fuel Scoop")],
            "Sol");

        Assert.Equal("Near", Assert.Single(route.Stops).Station);
    }

    [Fact]
    public void AnItemNobodySellsIsListedAsNotFound()
    {
        var route = OutfittingRoutePlanner.Plan(
            [.. Wanted.Select(Item)],
            [Station("Some", 5, null, "6A Fuel Scoop", "Python MkII")],
            "Sol");

        Assert.Equal(["Some"], route.Stops.Select(stop => stop.Station));
        Assert.Equal(["5A Frame Shift Drive"], route.NotFound);
    }

    [Fact]
    public void ARouteStopsAtSixStationsAndNamesWhatItLeftOut()
    {
        var items = Enumerable.Range(1, 7).Select(size => Item($"{size}A Fuel Scoop")).ToArray();
        var stations = items.Select((item, index) => Station($"S{index}", index + 1, null, item.Display)).ToArray();

        var route = OutfittingRoutePlanner.Plan(items, stations, "Sol");

        Assert.Equal(OutfittingRoutePlanner.MostStops, route.Stops.Count);
        Assert.Single(route.NotFound);
    }

    private sealed class Outfitters(params OutfitterStation[] stations) : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
            IReadOnlyList<OutfittingItem> items,
            string referenceSystem,
            double maxDistance,
            PadSize? minPad,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutfitterStation>>(stations);
    }

    [Fact]
    public async Task PlottingItSavesThePlanAndPointsAtItsPage()
    {
        var install = new MemoryInstall();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var plans = new RoutePlanBook(
            Path.Combine(install.Root, "route-plans.json"), new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            RouteCapability.Create(
                null,
                null,
                () => null,
                settings,
                plans,
                () => new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
                galaxy: new Outfitters(Station("Some", 5, null, "6A Fuel Scoop"))),
        ]);

        var result = await registry.InvokeAsync(
            "plot_outfitting_route",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["items"] = "6A Fuel Scoop, 5A Frame Shift Drive",
                ["near"] = "Sol",
            }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);
        Assert.Contains("1 stop", result.Content, StringComparison.Ordinal);
        Assert.Contains("Some", result.Content, StringComparison.Ordinal);
        Assert.Contains("Not found: 5A Frame Shift Drive", result.Content, StringComparison.Ordinal);
        Assert.Equal(PageKind.Plan, result.Page?.Kind);
        Assert.Equal((long)RoutePlanKind.Outfitting, result.Page?.Id);
        Assert.Equal("Some system", plans.Last(RoutePlanKind.Outfitting)?.Outfitting?.Stops[0].System);
    }

    [Fact]
    public void ABookSavedBeforeOutfittingStillReads()
    {
        var files = new MemoryFileSystem();
        const string path = "plans.json";

        files.WriteText(path, """[{"kind":"trade","plottedAt":"2026-10-01T00:00:00+00:00","headline":"old"}]""");

        var book = new RoutePlanBook(path, files, NullLogger<RoutePlanBook>.Instance);
        book.Load();

        Assert.Equal("old", book.Last(RoutePlanKind.Trade)?.Headline);
        Assert.Null(book.Last(RoutePlanKind.Outfitting));
    }
}
