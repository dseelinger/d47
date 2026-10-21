using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheCodexDiscoveriesSayWhatIsMissingTests
{
    private const string Commander =
        """{"timestamp":"2026-07-03T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    // EntryID, name, category, subcategory, regions (id:systems).
    private const string Table =
        "# fixture\n"
        + "1\tAlpha Tube\tBiology\tTubers\t18:5,3:9\n"
        + "2\tBeta Tube\tBiology\tTubers\t18:50\n"
        + "3\tGamma Tube\tBiology\tTubers\t18:20\n"
        + "4\tDelta Tube\tBiology\tTubers\t18:1\n"
        + "5\tEpsilon Vent\tGeology\tVents\t18:7\n";

    private static string Line(string time, string kind, string rest) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"" + kind + "\"," + rest + "}";

    private static string Entry(long id, int region, string subCategory = "Tubers") =>
        Line(
            "2026-10-01T10:00:00Z",
            "CodexEntry",
            "\"EntryID\":" + id + ",\"Name_Localised\":\"Entry " + id + "\",\"SubCategory_Localised\":\"" + subCategory
            + "\",\"Region\":\"$Codex_RegionName_" + region + ";\",\"System\":\"Sol\",\"IsNewEntry\":true");

    private static string Position(int x, int z) =>
        Line("2026-10-02T10:00:00Z", "Location", "\"StarSystem\":\"Fixture\",\"StarPos\":[" + x + ",0," + z + "]");

    private static GameStateStore Store(params string[] events)
    {
        var store = new GameStateStore();

        foreach (var line in new[] { Commander, Line("2026-07-03T09:01:00Z", "LoadGame", "\"Credits\":1") }.Concat(events))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static CodexLedger Ledger(params string[] events) => Store(events).Active!.CodexLedger;

    private static async Task<string> Ask(
        GameStateStore store, string? json = null, HistoryState history = HistoryState.Done)
    {
        var capability = JournalCapability.Create(store, () => history);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_codex_discoveries", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken;
    }

    [Fact]
    public void TheRegionCountsKnownLoggedAndMissing()
    {
        var discoveries = CodexDiscoveries.For(Ledger(Entry(1, 18), Entry(5, 18, "Vents")), CodexTable.Parse(Table), 18);

        Assert.Equal(5, discoveries.Known.Count);
        Assert.Equal([1L, 5L], discoveries.Logged.Select(item => item.Entry.EntryId).Order());
        Assert.Equal(3, discoveries.Missing.Count);
        Assert.All(discoveries.Logged, item => Assert.NotNull(item.Logged));
    }

    [Fact]
    public void AnEntryLoggedInAnotherRegionIsStillMissingHere()
    {
        var discoveries = CodexDiscoveries.For(Ledger(Entry(1, 3)), CodexTable.Parse(Table), 18);

        Assert.Empty(discoveries.Logged);
        Assert.Contains(discoveries.Missing, item => item.Entry.EntryId == 1);
    }

    [Fact]
    public void MissingEntriesAreOrderedByReportedSystems()
    {
        var discoveries = CodexDiscoveries.For(Ledger(Entry(5, 18, "Vents")), CodexTable.Parse(Table), 18);

        Assert.Equal([2L, 3L, 1L, 4L], discoveries.Missing.Select(item => item.Entry.EntryId));
    }

    [Fact]
    public void AnEntryTheTableDoesNotListIsUncoveredNotMissing()
    {
        var discoveries = CodexDiscoveries.For(Ledger(Entry(900, 18, "Stars")), CodexTable.Parse(Table), 18);

        Assert.Equal(5, discoveries.Missing.Count);
        Assert.Equal([900L], discoveries.Uncovered.Select(entry => entry.EntryId));
    }

    [Fact]
    public async Task TheAnswerNamesTheThreeMostReportedMissingEntries()
    {
        var shipped = CodexCatalogue.InRegion(18);
        var logged = shipped[0].Entry.EntryId;
        var expected = string.Join(", ", shipped.Skip(1).Take(3).Select(found => found.Entry.Name));

        var said = await Ask(
            Store(Entry(logged, 18), Position(0, 0)), "{\"region\":\"" + GalacticRegions.NameOf(18) + "\"}");

        Assert.Contains($"Most reported missing: {expected}, and {shipped.Count - 4} more.", said);
        Assert.Contains($"{shipped.Count - 1:N0} missing.", said);
    }

    [Fact]
    public async Task AnUnknownRegionIsRefused()
    {
        Assert.Equal(
            "I do not know a region called Nowhere.", await Ask(Store(Entry(1, 18)), """{"region":"Nowhere"}"""));
    }

    [Fact]
    public async Task WhileTheHistoryWalkRunsTheAnswerSaysSo()
    {
        Assert.Equal(
            "I have not finished reading the journal history yet.",
            await Ask(Store(Entry(1, 18)), history: HistoryState.Running));
    }

    [Theory]
    [InlineData("what codex entries am i missing")]
    [InlineData("missing codex entries")]
    public void QuestionsReachTheDiscoveriesTool(string asked)
    {
        var match = new KeywordRouter(TestSurface.For(new MemoryInstall()).Registry).Match(asked);

        Assert.NotNull(match);
        Assert.Equal("get_codex_discoveries", match!.ToolName);
    }

    [Fact]
    public void TheModelMayCallTheDiscoveriesTool()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var tool = registry.All.SelectMany(capability => capability.Descriptor.Tools).Single(tool => tool.Name == "get_codex_discoveries");

        Assert.False(tool.Protected);
    }

    [Theory]
    [InlineData("codex checklist")]
    [InlineData("my codex checklist")]
    public void TheOldChecklistPhrasesNoLongerReachTheTool(string asked)
    {
        var match = new KeywordRouter(TestSurface.For(new MemoryInstall()).Registry).Match(asked);

        Assert.NotEqual("get_codex_discoveries", match?.ToolName);
    }
}
