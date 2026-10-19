using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>search_outfitting: several items at one station, the board, the history and a saved search.</summary>
public class SearchOutfittingTakesSeveralItemsTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public StationQuery? LastQuery { get; private set; }

        public Exception? Throws { get; set; }

        public StationSearchResult Result { get; set; } = new(
            "Sol",
            1,
            [new StationSummary
            {
                Name = "Magnus Gateway",
                SystemName = "EZ Aquarii",
                Distance = 11.1,
                DistanceToArrival = 438,
                Type = "Coriolis Starport",
                HasLargePad = true,
                SmallPads = 9,
                MediumPads = 11,
                LargePads = 5,
                Services = ["Market", "Material Trader", "Restock"],
                ControllingFaction = "Future of EZ Aquarii",
            }]);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;

            return Throws is not null ? Task.FromException<StationSearchResult>(Throws) : Task.FromResult(Result);
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy, GalaxySearchBoard Board, SearchHistory History)
        Build(MemoryInstall install)
    {
        var galaxy = new FakeGalaxy();
        var board = new GalaxySearchBoard();
        var history = new SearchHistory(
            Path.Combine(@"C:\d47-test", "search-history.json"),
            new MemoryFileSystem(),
            NullLogger<SearchHistory>.Instance);
        var settings = TestSurface.For(install).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                galaxy, () => "Sol", settings, now: () => AskedAt, searches: board, history: history),
        ]);

        return (registry, galaxy, board, history);
    }

    private static Task<ToolResult> Ask(CapabilityRegistry registry, params (string Name, string Value)[] values) =>
        registry.InvokeAsync(
            "search_outfitting",
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task EveryModuleIsRequiredAtOneStation()
    {
        var (registry, galaxy, _, _) = Build(new MemoryInstall());

        var result = await Ask(registry, ("items", "5A Frame Shift Drive (SCO), fuel scoop, Python MkII"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(
            [new ModuleRequirement("Frame Shift Drive (SCO)", "5", "A"), new ModuleRequirement("Fuel Scoop")],
            galaxy.LastQuery!.Modules);
        Assert.Equal("Python MkII", galaxy.LastQuery.Ship);
        Assert.Equal("Sol", galaxy.LastQuery.ReferenceSystem);
        Assert.Contains("5A Frame Shift Drive", result.Content, StringComparison.Ordinal);
        Assert.Contains("Python MkII", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoShipsAreRefusedAndNothingIsSearched()
    {
        var (registry, galaxy, _, _) = Build(new MemoryInstall());

        var result = await Ask(registry, ("items", "Python, Anaconda"));

        Assert.True(result.IsError);
        Assert.Contains("One ship per search.", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
    }

    [Fact]
    public async Task AnItemNobodyHasHeardOfIsRefusedAndNeverSent()
    {
        var (registry, galaxy, _, _) = Build(new MemoryInstall());

        var result = await Ask(registry, ("items", "Fuel Scoop, 5A Frame Shift Drve"));

        Assert.True(result.IsError);
        Assert.Contains("Frame Shift Drive", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
    }

    [Fact]
    public async Task SurfaceStationsAndCarriersFollowTheirSwitches()
    {
        var (registry, galaxy, _, _) = Build(new MemoryInstall());

        await Ask(registry, ("items", "Fuel Scoop"), ("surface_stations", "false"));

        Assert.False(galaxy.LastQuery!.SurfaceStations);
        Assert.DoesNotContain(StationKind.SurfacePort, galaxy.LastQuery.Kinds!);
        Assert.DoesNotContain(StationKind.Settlement, galaxy.LastQuery.Kinds!);
        Assert.DoesNotContain(StationKind.FleetCarrier, galaxy.LastQuery.Kinds!);

        await Ask(registry, ("items", "Fuel Scoop"), ("include_carriers", "true"));

        Assert.True(galaxy.LastQuery.SurfaceStations);
        Assert.Contains(StationKind.FleetCarrier, galaxy.LastQuery.Kinds!);
    }

    [Fact]
    public async Task AnAnswerPostsUnderOutfittingAndIsKeptInTheHistory()
    {
        var (registry, _, board, history) = Build(new MemoryInstall());

        var result = await Ask(registry, ("items", "Fuel Scoop, Shield Booster"));

        var posting = board.Last(GalaxySearchKind.Outfitting);

        Assert.NotNull(posting);
        Assert.IsType<StationSearchResult>(posting.Result);
        Assert.Null(board.Last(GalaxySearchKind.Stations));
        Assert.Equal(PageRef.Search(GalaxySearchKind.Outfitting), result.Page);

        var entry = Assert.Single(history.All());
        Assert.Equal("search_outfitting", entry.Tool);
        Assert.IsType<StationSearchResult>(entry.Result);
        Assert.StartsWith("Outfitting", SearchWords.Describe(entry.Tool, entry.Arguments, entry.Reference), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOutfittingSearchCanBeSavedByName()
    {
        var store = new SavedSearchStore(
            Path.Combine(@"C:\d47-test", "saved-searches.json"),
            new MemoryFileSystem(),
            NullLogger<SavedSearchStore>.Instance);

        var outcome = store.Add(
            "exploration kit",
            "search_outfitting",
            new Dictionary<string, string> { ["items"] = "5A Frame Shift Drive (SCO), Fuel Scoop" },
            AskedAt,
            []);

        Assert.True(outcome.Saved, outcome.Message);
        Assert.Equal("search_outfitting", Assert.Single(store.All()).Tool);
    }

    [Fact]
    public void TheEgressTextNamesModulesAndShips()
    {
        var settings = new D47Settings { Knowledge = new KnowledgeSettings { GalaxySearch = true } };

        var entry = EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, settings, llmKeyPresent: false);

        Assert.Contains("the modules or ship you want to buy", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSystemFilterIsOfferedToAnOutfittingSearch() =>
        Assert.Empty(GalaxyFilters.For(GalaxySearchKind.Outfitting));
}
