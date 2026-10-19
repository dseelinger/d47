using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A search answer posted to the board names its kind's search page; a refusal names none (#1083).</summary>
public class ASpokenSearchNamesItsKindsPageTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public GalaxySearchResult Systems { get; set; } = new(
            "Sol", 1, [new SystemSummary { Name = "Alpha Centauri", Distance = 4.38 }]);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Systems);

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult("Sol", 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Sol", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static readonly DateTimeOffset At = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static CapabilityRegistry Build(GalaxySearchBoard board, FakeGalaxy? galaxy = null)
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        return CapabilityRegistry.Build(
            [GalaxyCapability.Create(galaxy ?? new FakeGalaxy(), () => "Sol", settings, now: () => At, searches: board)]);
    }

    private static Task<ToolResult> CallAsync(CapabilityRegistry registry, string tool, params (string Name, string Value)[] values) =>
        registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    private static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(
            input, InputSource.Spoken, TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    private static TurnLoop Loop(CapabilityRegistry registry, KeywordRouter router, ILlmProvider? model = null) =>
        new(
            registry,
            router,
            new LlmAvailabilityState(model is not null),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            model,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };

    [Theory]
    [InlineData("search_systems", GalaxySearchKind.Systems, "government", "Anarchy")]
    [InlineData("search_stations", GalaxySearchKind.Stations, "services", "Black Market")]
    [InlineData("find_body", GalaxySearchKind.Bodies, "landable", "true")]
    public async Task EachSearchNamesItsOwnPage(string tool, GalaxySearchKind kind, string filter, string value)
    {
        var board = new GalaxySearchBoard();

        var result = await CallAsync(Build(board), tool, (filter, value));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(PageRef.Search(kind), result.Page);
        Assert.NotNull(board.Last(kind));
    }

    [Fact]
    public async Task AnAnswerWithNoResultsStillNamesThePage()
    {
        var galaxy = new FakeGalaxy { Systems = new GalaxySearchResult("Sol", 0, []) };

        var result = await CallAsync(Build(new GalaxySearchBoard(), galaxy), "search_systems", ("government", "Anarchy"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(PageRef.Search(GalaxySearchKind.Systems), result.Page);
    }

    [Theory]
    [InlineData("search_systems", "government", "Pirate Kingdom")]
    [InlineData("search_stations", "min_pad", "Enormous")]
    [InlineData("find_body", "body_type", "Cheese moon")]
    public async Task ARefusedFilterNamesNoPage(string tool, string filter, string value)
    {
        var board = new GalaxySearchBoard();

        var result = await CallAsync(Build(board), tool, (filter, value));

        Assert.True(result.IsError);
        Assert.Null(result.Page);
    }

    [Fact]
    public async Task ASearchAskedOfTheModelEndsOnTheSystemsPage()
    {
        var registry = Build(new GalaxySearchBoard());
        var model = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling(
                "call_1", "search_systems", """{"government":"Anarchy","state":"Boom"}"""),
            RoundScriptedLlmProvider.Saying("Alpha Centauri."));

        var result = await RunAsync(Loop(registry, new KeywordRouter(registry), model), "find anarchy systems in boom");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(PageRef.Search(GalaxySearchKind.Systems), result.Page);
    }

    [Fact]
    public async Task AQuickSearchByItsPhraseEndsOnItsKindsPage()
    {
        var registry = Build(new GalaxySearchBoard());

        var result = await RunAsync(
            Loop(registry, new KeywordRouter(registry, QuickSearches.Phrases)), "run the anarchy outbreak search");

        Assert.Equal(TurnRoute.ActionCommand, result.Route);
        Assert.Equal(PageRef.Search(GalaxySearchKind.Systems), result.Page);
    }

    [Fact]
    public async Task ASavedSearchByItsPhraseEndsOnItsKindsPage()
    {
        var store = new SavedSearchStore(
            Path.Combine(@"C:\d47-test", "saved-searches.json"),
            new MemoryFileSystem(),
            NullLogger<SavedSearchStore>.Instance);
        store.Add("black markets", "search_stations", new Dictionary<string, string> { ["services"] = "Black Market" }, At, []);
        var registry = Build(new GalaxySearchBoard());

        var result = await RunAsync(
            Loop(registry, new KeywordRouter(registry, () => SavedSearchPhrases.Commands(store))),
            "run the black markets search");

        Assert.Equal(TurnRoute.ActionCommand, result.Route);
        Assert.Equal(PageRef.Search(GalaxySearchKind.Stations), result.Page);
    }
}
