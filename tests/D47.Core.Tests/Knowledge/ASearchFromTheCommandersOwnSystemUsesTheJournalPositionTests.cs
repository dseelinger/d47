using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A search from the Commander's own system is sent by journal position, so a system Spansh lacks still works (#2758).</summary>
public class ASearchFromTheCommandersOwnSystemUsesTheJournalPositionTests
{
    private static readonly StarPosition Here = new(10, -20, 30);

    private sealed class FakeGalaxy : StubGalaxyService
    {
        public string? ReferenceSystem { get; private set; }

        public StarPosition? ReferencePosition { get; private set; }

        public int Calls { get; private set; }

        private void Take(string? system, StarPosition? position)
        {
            Calls++;
            ReferenceSystem = system;
            ReferencePosition = position;
        }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Take(query.ReferenceSystem, query.ReferencePosition);

            return Task.FromResult(new GalaxySearchResult(null, 0, []));
        }

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Take(query.ReferenceSystem, query.ReferencePosition);

            return Task.FromResult(new StationSearchResult(null, 0, []));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Take(query.ReferenceSystem, query.ReferencePosition);

            return Task.FromResult(new BodySearchResult(null, 0, []));
        }
    }

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy) Build(StarPosition? position)
    {
        var galaxy = new FakeGalaxy();
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        return (
            CapabilityRegistry.Build(
                [GalaxyCapability.Create(galaxy, () => "Unlisted Sector AB-C d1", settings, currentPosition: () => position)]),
            galaxy);
    }

    private static Task<ToolResult> Ask(CapabilityRegistry registry, string tool, params (string Name, string Value)[] values) =>
        registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    public static TheoryData<string, string, string> Searches => new()
    {
        { "search_systems", "power_state", "Unoccupied" },
        { "search_stations", "station_type", "Starport" },
        { "search_outfitting", "items", "Fuel Scoop" },
        { "find_nearest_station", "module", "Frame Shift Drive" },
        { "find_body", "landable", "true" },
    };

    [Theory]
    [MemberData(nameof(Searches))]
    public async Task NoNearSendsThePosition(string tool, string key, string value)
    {
        var (registry, galaxy) = Build(Here);

        var result = await Ask(registry, tool, (key, value));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(Here, galaxy.ReferencePosition);
        Assert.Null(galaxy.ReferenceSystem);
    }

    [Theory]
    [MemberData(nameof(Searches))]
    public async Task TheCurrentSystemNamedInAnyCaseSendsThePosition(string tool, string key, string value)
    {
        var (registry, galaxy) = Build(Here);

        var result = await Ask(registry, tool, (key, value), ("near", "unlisted sector ab-c D1"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(Here, galaxy.ReferencePosition);
        Assert.Null(galaxy.ReferenceSystem);
    }

    [Theory]
    [MemberData(nameof(Searches))]
    public async Task AnotherSystemIsSentByName(string tool, string key, string value)
    {
        var (registry, galaxy) = Build(Here);

        var result = await Ask(registry, tool, (key, value), ("near", "Sol"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal("Sol", galaxy.ReferenceSystem);
        Assert.Null(galaxy.ReferencePosition);
    }

    [Theory]
    [MemberData(nameof(Searches))]
    public async Task WithoutAPositionTheNameIsSent(string tool, string key, string value)
    {
        var (registry, galaxy) = Build(null);

        var result = await Ask(registry, tool, (key, value));

        Assert.False(result.IsError, result.Content);
        Assert.Equal("Unlisted Sector AB-C d1", galaxy.ReferenceSystem);
        Assert.Null(galaxy.ReferencePosition);
    }

    [Fact]
    public async Task ASearchByPositionStillNamesTheSystemItIsMeasuredFrom()
    {
        var board = new GalaxySearchBoard();
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                new FakeGalaxy(), () => "Unlisted Sector AB-C d1", settings, searches: board, currentPosition: () => Here),
        ]);

        await Ask(registry, "search_systems", ("power_state", "Unoccupied"));

        Assert.Equal("Unlisted Sector AB-C d1", board.Last(GalaxySearchKind.Systems)!.Reference);
    }
}
