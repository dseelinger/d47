using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Each answered search is shared by every Commander, 25 deep, and survives a restart (#1085).</summary>
public class TheLastTwentyFiveSearchesAreKeptTests
{
    private const long Sol = 10477373803;

    private static readonly DateTimeOffset At = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private static string StorePath => Path.Combine(@"C:\d47-test", "search-history.json");

    private SearchHistory Store() => new(StorePath, _files, NullLogger<SearchHistory>.Instance);

    private static Dictionary<string, string> Args(string services = "Interstellar Factors") =>
        new(StringComparer.Ordinal) { ["services"] = services };

    private static StationSearchResult Stations() =>
        new("Sol", 1, [new StationSummary { Name = "Fixture Port", SystemName = "Eurybia", Distance = 12.5 }]);

    private static SearchHistoryEntry StationEntry(string services, int minutes = 0) =>
        SearchHistoryEntry.Of("search_stations", Args(services), "Sol", null, Stations(), At.AddMinutes(minutes));

    [Fact]
    public void AnEntrySurvivesAReloadWithItsAnswer()
    {
        Store().Add(StationEntry("Repair"));

        var reloaded = Store();
        reloaded.Load();

        var entry = Assert.Single(reloaded.All());
        Assert.Equal("search_stations", entry.Tool);
        Assert.Equal("Repair", entry.Arguments["services"]);
        Assert.Equal("Sol", entry.Reference);
        Assert.Equal(At, entry.AskedAt);
        var result = Assert.IsType<StationSearchResult>(entry.Result);
        Assert.Equal("Fixture Port", Assert.Single(result.Stations).Name);
    }

    [Fact]
    public void ADescribedSystemSurvivesAReloadWithItsAddress()
    {
        Store().Add(SearchHistoryEntry.Of(
            "describe_system", new Dictionary<string, string> { ["system"] = "Sol" }, "Sol", Sol, null, At));

        var reloaded = Store();
        reloaded.Load();

        var entry = Assert.Single(reloaded.All());
        Assert.Equal(Sol, entry.SystemAddress);
        Assert.Null(entry.Result);
    }

    [Fact]
    public void OnlyTheLastTwentyFiveAreKeptNewestFirst()
    {
        var store = Store();

        for (var i = 0; i < SearchHistory.Capacity + 5; i++)
        {
            store.Add(StationEntry($"Service {i}", i));
        }

        var reloaded = Store();
        reloaded.Load();

        var kept = reloaded.All();
        Assert.Equal(SearchHistory.Capacity, kept.Count);
        Assert.Equal("Service 29", kept[0].Arguments["services"]);
        Assert.Equal("Service 5", kept[^1].Arguments["services"]);
    }

    [Fact]
    public void TheSameSearchTwiceLeavesTheNewer()
    {
        var store = Store();
        store.Add(StationEntry("Repair", 0));
        store.Add(StationEntry("Refuel", 1));
        store.Add(StationEntry("Repair", 2));

        var kept = store.All();
        Assert.Equal(2, kept.Count);
        Assert.Equal("Repair", kept[0].Arguments["services"]);
        Assert.Equal(At.AddMinutes(2), kept[0].AskedAt);
    }

    [Fact]
    public void AFileOfPerCommanderRecordsLoadsMergedNewestFirstAndCapped()
    {
        string EntriesOf(string prefix, int from)
        {
            var files = new MemoryFileSystem();
            var history = new SearchHistory(StorePath, files, NullLogger<SearchHistory>.Instance);

            for (var i = 0; i < 20; i++)
            {
                history.Add(StationEntry($"{prefix} {i}", from + i));
            }

            using var document = System.Text.Json.JsonDocument.Parse(files.ReadText(StorePath)!);

            return string.Join(",", document.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.GetRawText()));
        }

        _files.WriteText(
            StorePath,
            $$"""{"commanders":[{"frontierId":"F1","entries":[{{EntriesOf("A", 0)}}]},{"frontierId":"F2","entries":[{{EntriesOf("B", 100)}}]}]}""");

        var store = Store();
        store.Load();

        var kept = store.All();
        Assert.Equal(SearchHistory.Capacity, kept.Count);
        Assert.Equal(At.AddMinutes(119), kept[0].AskedAt);
        Assert.Equal(At.AddMinutes(100), kept[19].AskedAt);
        Assert.Equal(At.AddMinutes(19), kept[20].AskedAt);
        Assert.Equal(At.AddMinutes(15), kept[24].AskedAt);

        var rewritten = _files.ReadText(StorePath)!;
        Assert.DoesNotContain("commanders", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("frontierId", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntryTheFileCannotReadBackIsDroppedAndTheRestLoad()
    {
        var store = Store();
        store.Add(StationEntry("Repair"));
        store.Add(StationEntry("Refuel"));
        var text = _files.ReadText(StorePath)!;
        var first = text.IndexOf("search_stations", StringComparison.Ordinal);
        _files.WriteText(StorePath, text.Remove(first, "search_stations".Length).Insert(first, "no_such_tool"));

        var reloaded = Store();
        reloaded.Load();

        Assert.Single(reloaded.All());
    }

    [Fact]
    public void ASearchIsDescribedInOneLine()
    {
        Assert.Equal(
            "Stations · Interstellar Factors · near Sol",
            SearchWords.Describe("search_stations", Args(), "Sol"));
        Assert.Equal(
            "Systems · Anarchy · Outbreak · near Sol",
            SearchWords.Describe(
                "search_systems",
                new Dictionary<string, string> { ["government"] = "Anarchy", ["state"] = "Outbreak" },
                "Sol"));
        Assert.Equal(
            "System · Shinrarta Dezhra",
            SearchWords.Describe(
                "describe_system",
                new Dictionary<string, string> { ["system"] = "Shinrarta Dezhra" },
                "Shinrarta Dezhra"));
    }

    private sealed class Galaxy : IGalaxyService
    {
        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Stations());

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(
            MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = "Sol", SystemAddress = Sol });

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([new SystemNameMatch("Sol", Sol, new StarPosition(0, 0, 0))]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(
            string system, double lightYears, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private (SearchHistory History, ToolDefinition Tool) Tool(string name, string? frontierId = "F1")
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        var history = Store();
        var state = frontierId is null ? null : new CommanderGameState(new CommanderIdentity(frontierId, "Tester"));

        var tool = GalaxyCapability.Create(
                new Galaxy(),
                () => "Sol",
                settings,
                gameState: () => state,
                systems: new Systems(),
                currentAddress: () => 99,
                history: history)
            .Tools.Single(t => t.Name == name);

        return (history, tool);
    }

    private static Task<ToolResult> Run(ToolDefinition tool, Dictionary<string, string> arguments) =>
        tool.Handler(new ToolArguments(arguments), TestContext.Current.CancellationToken);

    [Fact]
    public async Task AStationSearchIsKeptWithItsAnswer()
    {
        var (history, tool) = Tool("search_stations");

        Assert.False((await Run(tool, Args())).IsError);

        var entry = Assert.Single(history.All());
        Assert.Equal("search_stations", entry.Tool);
        Assert.Equal("Interstellar Factors", entry.Arguments["services"]);
        Assert.IsType<StationSearchResult>(entry.Result);
    }

    [Fact]
    public async Task ARefusedFilterIsNotKept()
    {
        var (history, tool) = Tool("search_stations");

        Assert.True((await Run(tool, Args("No Such Service"))).IsError);

        Assert.Empty(history.All());
    }

    [Fact]
    public async Task ASearchIsKeptWhileNoCommanderIsKnown()
    {
        var (history, tool) = Tool("search_stations", frontierId: null);

        Assert.False((await Run(tool, Args())).IsError);

        Assert.Single(history.All());
    }

    [Fact]
    public async Task ANamedSystemIsKeptWithItsAddress()
    {
        var (history, tool) = Tool("describe_system");

        await Run(tool, new Dictionary<string, string> { ["system"] = "Sol" });

        var entry = Assert.Single(history.All());
        Assert.Equal("describe_system", entry.Tool);
        Assert.Equal(Sol, entry.SystemAddress);
    }

    [Fact]
    public async Task WhereTheCommanderIsIsNotKept()
    {
        var (history, tool) = Tool("describe_system");

        await Run(tool, []);

        Assert.Empty(history.All());
    }
}
