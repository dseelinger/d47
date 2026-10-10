using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>One filter vocabulary, keyed per search kind, and the system search built from it (#809).</summary>
public class EachSearchKindOffersOnlyTheFiltersItHonoursTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public GalaxyQuery? LastQuery { get; private set; }

        public Exception? Throws { get; set; }

        public GalaxySearchResult Result { get; set; } = new(
            "Sol",
            1,
            [new SystemSummary
            {
                Name = "Alpha Centauri",
                Distance = 4.38,
                ControllingPower = "Jerome Archer",
                PowerState = "Fortified",
            }]);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;

            return Throws is not null ? Task.FromException<GalaxySearchResult>(Throws) : Task.FromResult(Result);
        }

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
            [GalaxyCapability.Create(galaxy, () => "Sol", settings, now: () => AskedAt, searches: board)]);

        return (registry, galaxy, board);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    private static IReadOnlyList<GalaxyCriterion> Parse(params (string Name, string Value)[] values)
    {
        Assert.True(
            GalaxyCriteria.TryParse(
                GalaxySearchKind.Systems,
                values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal),
                out var criteria,
                out var failure),
            failure);

        return criteria;
    }

    [Fact]
    public void EachKindHoldsExactlyTheFiltersItHonours()
    {
        Assert.Equal(
            [
                "distance", "allegiance", "government", "primary_economy", "security", "state", "faction",
                "faction_state", "faction_government", "controlling_faction", "power", "power_state", "population",
                "colonised",
            ],
            GalaxyFilters.For(GalaxySearchKind.Systems).Select(filter => filter.Name));

        Assert.Equal(
            [
                "allegiance", "government", "primary_economy", "station_economy", "state", "controlling_faction",
                "power", "power_state", "population", "colonised",
            ],
            GalaxyFilters.For(GalaxySearchKind.Stations).Select(filter => filter.Name));
        Assert.Equal(["power", "power_state"], GalaxyFilters.For(GalaxySearchKind.Bodies).Select(filter => filter.Name));
    }

    [Fact]
    public void AFilterAKindDoesNotHonourIsRefusedByName()
    {
        Assert.False(GalaxyCriteria.TryParse(
            GalaxySearchKind.Stations,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["security"] = "High" },
            out _,
            out var failure));

        Assert.Equal("Stations can't be filtered by security: Spansh's station index doesn't carry it.", failure);
    }

    [Theory]
    [InlineData(GalaxySearchKind.Stations, "faction_state", "Civil Unrest", "Stations", "station")]
    [InlineData(GalaxySearchKind.Stations, "faction_government", "Corporate", "Stations", "station")]
    [InlineData(GalaxySearchKind.Systems, "station_economy", "Refinery", "Systems", "system")]
    public void AFactionPresentIsRefusedToStationsAndAStationEconomyToSystems(
        GalaxySearchKind kind,
        string filter,
        string value,
        string plural,
        string singular)
    {
        Assert.False(GalaxyCriteria.TryParse(
            kind,
            new Dictionary<string, string>(StringComparer.Ordinal) { [filter] = value },
            out _,
            out var failure));

        Assert.Equal($"{plural} can't be filtered by {filter}: Spansh's {singular} index doesn't carry it.", failure);
    }

    [Theory]
    [InlineData("power", "Zachary Hudson")]
    [InlineData("power_state", "Contested")]
    [InlineData("faction_state", "Civil Unrst")]
    [InlineData("faction_government", "Corprate")]
    public void AnUnknownChoiceIsRefusedWithTheValidOnes(string filter, string value)
    {
        Assert.False(GalaxyCriteria.TryParse(
            GalaxySearchKind.Systems,
            new Dictionary<string, string>(StringComparer.Ordinal) { [filter] = value },
            out _,
            out var failure));

        Assert.Contains(value, failure, StringComparison.Ordinal);

        foreach (var valid in GalaxyFilters.Find(filter)!.Choices)
        {
            Assert.Contains(valid, failure, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ColonisedFalseIsNoFilterAtAll()
    {
        Assert.Empty(Parse(("colonised", "false")));
        Assert.Single(Parse(("colonised", "true")));
    }

    [Fact]
    public void PopulationZeroMeansUnpopulated()
    {
        var population = Assert.Single(Parse(("population", "0")));

        Assert.Equal(GalaxyFilterKind.Comparison, population.Filter.Kind);
        Assert.Null(population.Min);
        Assert.Equal(0, population.Max);
    }

    [Fact]
    public void TheSchemaOffersExactlyTheSystemFilters()
    {
        var install = new MemoryInstall();
        var (registry, _, _) = Build(install);

        var tool = registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Single(tool => tool.Name == "search_systems");

        Assert.Equal(
            ["near", .. GalaxyFilters.For(GalaxySearchKind.Systems).Select(filter => filter.Name), "limit"],
            tool.Parameters.Select(parameter => parameter.Name));

        Assert.Equal(
            ToolParameterType.Boolean,
            tool.Parameters.Single(parameter => parameter.Name == "colonised").Type);
        Assert.Contains(
            "Jerome Archer",
            tool.Parameters.Single(parameter => parameter.Name == "power").AllowedValues);
    }

    [Fact]
    public async Task AnAnswerIsPostedWithTheArgumentsAsGiven()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, board) = Build(install);
        GalaxySearchKind? raised = null;
        board.Posted += kind => raised = kind;

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("power", "jerome archer"), ("distance", "20")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(GalaxySearchKind.Systems, raised);

        var posting = board.Last(GalaxySearchKind.Systems);

        Assert.NotNull(posting);
        Assert.Equal("jerome archer", posting.Arguments["power"]);
        Assert.False(posting.Arguments.ContainsKey("near"));
        Assert.Equal("Sol", posting.Reference);
        Assert.Same(galaxy.Result, posting.Result);
        Assert.Equal(result.Content, posting.Answer);
        Assert.Equal(AskedAt, posting.AskedAt);
    }

    [Fact]
    public async Task APowerSearchNamesThePowerAndItsState()
    {
        var install = new MemoryInstall();
        var (registry, _, _) = Build(install);

        var withPower = await registry.InvokeAsync(
            "search_systems",
            Args(("power_state", "Fortified")),
            TestContext.Current.CancellationToken);
        var without = await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20")),
            TestContext.Current.CancellationToken);

        Assert.Contains("Jerome Archer, Fortified", withPower.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Jerome Archer", without.Content, StringComparison.Ordinal);
    }

    private static readonly GalaxySearchResult Barnards = new(
        "Sol",
        1,
        [new SystemSummary
        {
            Name = "Barnard's Star",
            Distance = 5.95,
            ControllingFaction = "Barnard's Star Labour",
            Factions =
            [
                new FactionPresence("Barnard's Star Labour", 0.61) { State = "Boom", Government = "Democracy" },
                new FactionPresence("Barnard's Star Alliance", 0.12) { State = "Civil Unrest", Government = "Corporate" },
                new FactionPresence("Barnard's Star Crimson Gang", 0.27) { State = "Expansion", Government = "Anarchy" },
            ],
        }]);

    [Fact]
    public async Task AFactionStateSearchNamesTheFactionInThatState()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);
        galaxy.Result = Barnards;

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("faction_state", "civil unrest")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains(
            "controlled by Barnard's Star Labour; Barnard's Star Alliance in Civil Unrest",
            result.Content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Crimson Gang", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFactionStateAndGovernmentSearchNamesTheFactionThatIsBoth()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);
        galaxy.Result = Barnards;

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("faction_state", "Civil Unrest, Expansion"), ("faction_government", "Anarchy")),
            TestContext.Current.CancellationToken);

        Assert.Contains("; Barnard's Star Crimson Gang, Anarchy, in Expansion", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Barnard's Star Alliance", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableServicePostsNothing()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, board) = Build(install);
        galaxy.Throws = new GalaxyUnavailableException("The galaxy search could not be reached.");

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Null(board.Last(GalaxySearchKind.Systems));
    }
}
