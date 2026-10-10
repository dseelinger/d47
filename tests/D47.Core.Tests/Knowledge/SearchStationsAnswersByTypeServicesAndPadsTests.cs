using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>search_stations: refusals, faction correction, the spoken answer and the board (#810).</summary>
public class SearchStationsAnswersByTypeServicesAndPadsTests
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

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy, GalaxySearchBoard Board) Build(MemoryInstall install)
    {
        var galaxy = new FakeGalaxy();
        var board = new GalaxySearchBoard();
        var settings = TestSurface.For(install).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                galaxy,
                () => "Sol",
                settings,
                now: () => AskedAt,
                searches: board,
                factions: () => ["Mother Gaia", "Future of EZ Aquarii"]),
        ]);

        return (registry, galaxy, board);
    }

    private static Task<ToolResult> Ask(CapabilityRegistry registry, params (string Name, string Value)[] values) =>
        registry.InvokeAsync(
            "search_stations",
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("security", "High")]
    [InlineData("faction", "Mother Gaia")]
    public void AFilterTheStationIndexCannotCarryIsRefusedByName(string name, string value)
    {
        Assert.False(StationQuery.TryParse(
            new StationSearch
            {
                ReferenceSystem = "Sol",
                Services = "Refuel",
                Filters = new Dictionary<string, string>(StringComparer.Ordinal) { [name] = value },
            },
            out _,
            out var failure));

        Assert.Equal($"Stations can't be filtered by {name}: Spansh's station index doesn't carry it.", failure);
    }

    [Theory]
    [InlineData("security")]
    [InlineData("faction")]
    public void TheToolOffersNeitherSecurityNorFaction(string name)
    {
        var install = new MemoryInstall();
        var (registry, _, _) = Build(install);

        var tool = registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Single(tool => tool.Name == "search_stations");

        Assert.DoesNotContain(tool.Parameters, parameter => parameter.Name == name);
    }

    [Fact]
    public async Task ASearchWithNothingButDistanceIsRefused()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);

        var result = await Ask(registry, ("max_distance", "20"));

        Assert.True(result.IsError);
        Assert.StartsWith("That search has nothing but a distance", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
    }

    [Fact]
    public async Task AMisspelledControllingFactionIsCorrectedAgainstTheJournals()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);

        var result = await Ask(registry, ("controlling_faction", "mother gaia"));

        Assert.False(result.IsError);
        Assert.StartsWith("Read as Mother Gaia.", result.Content, StringComparison.Ordinal);
        Assert.Equal(["Mother Gaia"], Assert.Single(galaxy.LastQuery!.Criteria).Choices);
    }

    [Fact]
    public async Task AnUnmatchedFactionWithNoResultsMayBeMisspelled()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);
        galaxy.Result = new StationSearchResult("Sol", 0, []);

        var result = await Ask(registry, ("controlling_faction", "Zorgon Peterson Collective"));

        Assert.False(result.IsError);
        Assert.StartsWith(
            "'Zorgon Peterson Collective' may be misspelled",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAnswerNamesTypePadArrivalAndTheAskedServices()
    {
        var install = new MemoryInstall();
        var (registry, _, _) = Build(install);

        var result = await Ask(registry, ("services", "Material Trader, Rearm"));

        Assert.False(result.IsError);
        Assert.Contains(
            "Magnus Gateway in EZ Aquarii — 11.10 ly; Coriolis Starport; large pad; 438 ls from arrival; "
            + "has Material Trader, Rearm",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAnswerIsPostedUnderStations()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, board) = Build(install);

        var result = await Ask(registry, ("min_pad", "Large"), ("station_type", "Outpost"));

        var posting = board.Last(GalaxySearchKind.Stations);

        Assert.NotNull(posting);
        Assert.Equal("Outpost", posting.Arguments["station_type"]);
        Assert.Same(galaxy.Result, posting.Result);
        Assert.Equal(result.Content, posting.Answer);
        Assert.Equal(AskedAt, posting.AskedAt);
        Assert.Null(board.Last(GalaxySearchKind.Systems));
    }

    [Fact]
    public async Task AnUnreachableServicePostsNothing()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, board) = Build(install);
        galaxy.Throws = new GalaxyUnavailableException("The galaxy search could not be reached.");

        var result = await Ask(registry, ("services", "Refuel"));

        Assert.True(result.IsError);
        Assert.Null(board.Last(GalaxySearchKind.Stations));
    }
}
