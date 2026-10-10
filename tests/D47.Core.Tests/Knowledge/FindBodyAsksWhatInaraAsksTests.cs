using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>find_body's INARA filters, its order by material share, and its board posting (#811).</summary>
public class FindBodyAsksWhatInaraAsksTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public BodyQuery? LastQuery { get; private set; }

        public Exception? Throws { get; set; }

        public BodySearchResult Result { get; set; } = new(
            "Sol",
            3,
            [
                Body("Near 1", 0.4),
                Body("Middle 2", 1.1),
                Body("Far 3", 0.7),
            ]);

        private static BodySummary Body(string name, double polonium) => new()
        {
            Name = name,
            SystemName = "Sol",
            Volcanism = "Water Geysers",
            Gravity = 0.08,
            IsTidallyLocked = true,
            Materials = [("Iron", 20), ("Polonium", polonium)],
        };

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;

            return Throws is not null ? Task.FromException<BodySearchResult>(Throws) : Task.FromResult(Result);
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
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

    private static Task<ToolResult> FindBody(CapabilityRegistry registry, params (string Name, string Value)[] values) =>
        registry.InvokeAsync("find_body", Args(values), TestContext.Current.CancellationToken);

    [Fact]
    public void TheSchemaOffersTheNewFiltersAndTheBodyVocabulary()
    {
        var install = new MemoryInstall();
        var (registry, _, _) = Build(install);

        var names = registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Single(tool => tool.Name == "find_body")
            .Parameters.Select(parameter => parameter.Name)
            .ToList();

        Assert.Subset(
            new HashSet<string>(names),
            new HashSet<string>(
            [
                "volcanism", "atmosphere", "tidally_locked", "gravity", "temperature", "max_arrival_distance",
                "material", "order_by", "power", "power_state",
            ]));
        Assert.DoesNotContain("allegiance", names);
    }

    [Fact]
    public async Task VolcanismIsReadInTheCataloguesSpelling()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);

        var result = await FindBody(registry, ("volcanism", "water geysers"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal("Water Geysers", galaxy.LastQuery?.Volcanism);
        Assert.Contains("water geysers", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownVolcanismIsRefusedWithSuggestions()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);

        var result = await FindBody(registry, ("volcanism", "water geyser"));

        Assert.True(result.IsError);
        Assert.Null(galaxy.LastQuery);
        Assert.Contains("Water Geysers", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderingByMaterialKeepsTheRichestAndSaysAmongHowMany()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);

        var result = await FindBody(registry, ("material", "Polonium"), ("order_by", "material"), ("limit", "2"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(BodyQuery.RichestOf, galaxy.LastQuery?.Size);
        Assert.Contains("richest 2 in Polonium among the nearest 3", result.Content, StringComparison.Ordinal);
        Assert.True(
            result.Content.IndexOf("Middle 2", StringComparison.Ordinal)
            < result.Content.IndexOf("Far 3", StringComparison.Ordinal));
        Assert.DoesNotContain("Near 1", result.Content, StringComparison.Ordinal);
        Assert.Contains("1.1% Polonium", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderingByMaterialWithoutAMaterialIsRefused()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);

        var result = await FindBody(registry, ("landable", "true"), ("order_by", "material"));

        Assert.True(result.IsError);
        Assert.Null(galaxy.LastQuery);
        Assert.Contains("surface material", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void AllegianceIsRefusedWithTheSentenceNamingTheKind()
    {
        Assert.False(BodyQuery.TryParse(
            new BodyRequest
            {
                ReferenceSystem = "Sol",
                Landable = true,
                Filters = new Dictionary<string, string>(StringComparer.Ordinal) { ["allegiance"] = "Federation" },
            },
            out _,
            out var failure));

        Assert.Equal("Bodies can't be filtered by allegiance: Spansh's body index doesn't carry it.", failure);
    }

    [Fact]
    public async Task ANewFilterAloneIsEnoughToSearch()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, _) = Build(install);

        var result = await FindBody(registry, ("gravity", "0.1"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(0.1, galaxy.LastQuery?.GravityMax);
        Assert.Contains("0.08 g", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAnswerIsPostedUnderBodies()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, board) = Build(install);

        var result = await FindBody(registry, ("tidally_locked", "true"));

        var posting = board.Last(GalaxySearchKind.Bodies);

        Assert.NotNull(posting);
        Assert.Equal("true", posting.Arguments["tidally_locked"]);
        Assert.Same(galaxy.Result, posting.Result);
        Assert.Equal(result.Content, posting.Answer);
        Assert.Equal(AskedAt, posting.AskedAt);
        Assert.Contains("tidally locked", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedSearchPostsNothing()
    {
        var install = new MemoryInstall();
        var (registry, galaxy, board) = Build(install);
        galaxy.Throws = new GalaxyUnavailableException("The galaxy search could not be reached.");

        var result = await FindBody(registry, ("landable", "true"));

        Assert.True(result.IsError);
        Assert.Null(board.Last(GalaxySearchKind.Bodies));
    }
}
