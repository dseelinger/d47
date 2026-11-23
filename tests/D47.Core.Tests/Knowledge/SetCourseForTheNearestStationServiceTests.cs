using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>"Closest" is "nearest", and a quick search can be followed by a course to its first result.</summary>
public class SetCourseForTheNearestStationServiceTests
{
    private sealed class FakeGalaxy(params StationSummary[] stations) : StubGalaxyService
    {
        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(
                "Sol", stations.Length, [.. stations.Select(station => new SystemSummary { Name = station.SystemName })]));

        public List<StationQuery> Asked { get; } = [];

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);

            return Task.FromResult(new StationSearchResult("Sol", stations.Length, stations));
        }
    }

    private sealed class RecordingClipboard : IClipboard
    {
        public List<string> Texts { get; } = [];

        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Texts.Add(text);

            return Task.FromResult(true);
        }
    }

    private static StationSummary Station(string name, string system) => new() { Name = name, SystemName = system };

    private static (CapabilityRegistry Registry, RecordingClipboard Clipboard, LastFoundSystem LastFound) Build(
        params StationSummary[] stations) => Build(new FakeGalaxy(stations));

    private static (CapabilityRegistry Registry, RecordingClipboard Clipboard, LastFoundSystem LastFound) Build(
        FakeGalaxy galaxy)
    {
        var install = new MemoryInstall();
        var clipboard = new RecordingClipboard();
        var lastFound = new LastFoundSystem();
        var navigation = NavigationSurface.Inert with { Clipboard = clipboard };

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                galaxy,
                () => "Sol",
                TestSurface.For(install).Settings,
                clipboard: null,
                lastFound: lastFound,
                navigation: navigation),
            NavigationCapability.Create(navigation),
        ]);

        return (registry, clipboard, lastFound);
    }

    [Fact]
    public void FindTheClosestVistaGenomicsRunsTheQuickSearch()
    {
        var router = new KeywordRouter(TestSurface.For(new MemoryInstall()).Registry, QuickSearches.Phrases);

        var match = router.MatchToolCommand("Find the closest Vista Genomics.");

        Assert.NotNull(match);
        Assert.Equal("search_stations", match.ToolName);
        Assert.Equal("Vista Genomics", match.Arguments.Values["services"]);
    }

    [Fact]
    public void SetCourseForTheClosestVistaGenomicsIsTheCourseCommand()
    {
        var router = new KeywordRouter(TestSurface.For(new MemoryInstall()).Registry, QuickSearches.CoursePhrases);

        var match = router.MatchToolCommand("Set course for the closest Vista Genomics.");

        Assert.NotNull(match);
        Assert.Equal(GalaxyCapability.Id, match.CapabilityId);
        Assert.Equal(GalaxyCapability.CourseToQuickSearch, match.ToolName);
        Assert.Equal("vista genomics", match.Arguments.Values["search"]);
    }

    [Fact]
    public void EveryCourseSpellingMatchesForEveryQuickSearch()
    {
        var router = new KeywordRouter(TestSurface.For(new MemoryInstall()).Registry, QuickSearches.CoursePhrases);

        foreach (var search in QuickSearches.All)
        {
            foreach (var verb in new[] { "set course for", "set a course for", "plot a course to", "plot a route to" })
            {
                foreach (var word in new[] { "nearest", "closest" })
                {
                    var match = router.MatchToolCommand($"{verb} the {word} {search.Name}");

                    Assert.NotNull(match);
                    Assert.Equal(GalaxyCapability.CourseToQuickSearch, match.ToolName);
                    Assert.Equal(search.Name, match.Arguments.Values["search"]);
                }
            }
        }
    }

    [Fact]
    public async Task TheCourseCommandPlotsToTheFirstStationsSystem()
    {
        var (registry, clipboard, _) = Build(Station("Hume Vista", "Lave"), Station("Other", "Sol"));

        var result = await registry.InvokeAsync(
            GalaxyCapability.CourseToQuickSearch,
            new ToolArguments(new Dictionary<string, string> { ["search"] = "vista genomics" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);
        Assert.Equal(["Lave"], clipboard.Texts);
    }

    [Fact]
    public async Task WithNothingFoundTheCourseCommandPlotsNothingAndSaysSo()
    {
        var (registry, clipboard, lastFound) = Build();
        lastFound.Remember("Older System");

        var result = await registry.InvokeAsync(
            GalaxyCapability.CourseToQuickSearch,
            new ToolArguments(new Dictionary<string, string> { ["search"] = "vista genomics" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);
        Assert.Contains("Nothing was found", result.Content, StringComparison.Ordinal);
        Assert.Empty(clipboard.Texts);
    }

    [Fact]
    public async Task TheCourseToTheNearestEncodedMaterialTraderSearches500LightYears()
    {
        var galaxy = new FakeGalaxy(Station("Trader", "Lave"));
        var (registry, _, _) = Build(galaxy);

        var result = await registry.InvokeAsync(
            GalaxyCapability.CourseToQuickSearch,
            new ToolArguments(new Dictionary<string, string> { ["search"] = "encoded material trader" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);
        Assert.Equal(500, Assert.Single(galaxy.Asked).MaxDistance);
    }

    [Fact]
    public async Task ANearestFirstStationSearchLeavesItsFirstSystemForSetACourse()
    {
        var (registry, _, lastFound) = Build(Station("Hume Vista", "Lave"));

        await registry.InvokeAsync(
            "search_stations",
            new ToolArguments(new Dictionary<string, string> { ["services"] = "Vista Genomics" }),
            TestContext.Current.CancellationToken);

        Assert.Equal("Lave", lastFound.System);
    }

    [Fact]
    public async Task ANearestFirstSystemSearchLeavesItsFirstSystemForSetACourse()
    {
        var (registry, _, lastFound) = Build(Station("Hume Vista", "Lave"));

        await registry.InvokeAsync(
            "search_systems",
            new ToolArguments(new Dictionary<string, string> { ["government"] = "Anarchy" }),
            TestContext.Current.CancellationToken);

        Assert.Equal("Lave", lastFound.System);
    }

    [Fact]
    public async Task ASearchOrderedOtherwiseDoesNotChangeWhatWasFound()
    {
        var (registry, _, lastFound) = Build(Station("Hume Vista", "Lave"));
        lastFound.Remember("Older System");

        await registry.InvokeAsync(
            "search_stations",
            new ToolArguments(new Dictionary<string, string>
            {
                ["services"] = "Vista Genomics",
                ["order_by"] = "distance",
                ["order"] = "desc",
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal("Older System", lastFound.System);
    }
}
