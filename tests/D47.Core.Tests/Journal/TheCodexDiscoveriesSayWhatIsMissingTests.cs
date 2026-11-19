using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
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

    // Two categories; the second's top count is larger than the first's, and two of its entries tie.
    private const string TwoCategories =
        "1\tAlpha Tube\tBiology\tTubers\t18:5\n"
        + "2\tBeta Tube\tBiology\tTubers\t18:50\n"
        + "3\tGamma Tube\tBiology\tTubers\t18:20\n"
        + "4\tDelta Tube\tBiology\tTubers\t18:1\n"
        + "5\tEpsilon Vent\tGeology\tVents\t18:70\n"
        + "6\tZeta Vent\tGeology\tVents\t18:7\n"
        + "7\tEta Vent\tGeology\tVents\t18:7\n";

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

    private static GameStateStore Store(params string[] events) =>
        JournalScript.Store([Commander, Line("2026-07-03T09:01:00Z", "LoadGame", "\"Credits\":1"), .. events]);

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
    public void EntriesAreOrderedByCategoryThenReportedSystemsThenName()
    {
        var discoveries = CodexDiscoveries.For(Ledger(Entry(3, 18), Entry(6, 18, "Vents")), CodexTable.Parse(TwoCategories), 18);

        Assert.Equal([2L, 3L, 1L, 4L, 5L, 7L, 6L], discoveries.Known.Select(item => item.Entry.EntryId));
        Assert.Equal([2L, 1L, 4L, 5L, 7L], discoveries.Missing.Select(item => item.Entry.EntryId));
        Assert.Equal([3L, 6L], discoveries.Logged.Select(item => item.Entry.EntryId));
    }

    [Fact]
    public void TheAnswerNamesTheMissingEntriesOfEachCategoryOnItsOwnLine()
    {
        var discoveries = CodexDiscoveries.For(Ledger(Entry(3, 18)), CodexTable.Parse(TwoCategories), 18);

        Assert.Equal(
            [
                "Most reported missing in biology: Beta Tube, Alpha Tube, Delta Tube.",
                "Most reported missing in geology: Epsilon Vent, Eta Vent, Zeta Vent.",
            ],
            JournalCapability.CodexMissingByCategory(discoveries));
    }

    [Fact]
    public void ACategoryWithNothingMissingHasNoLine()
    {
        var discoveries = CodexDiscoveries.For(
            Ledger(Entry(5, 18, "Vents"), Entry(6, 18, "Vents"), Entry(7, 18, "Vents")), CodexTable.Parse(TwoCategories), 18);

        Assert.Equal(
            ["Most reported missing in biology: Beta Tube, Gamma Tube, Alpha Tube, and 1 more."],
            JournalCapability.CodexMissingByCategory(discoveries));
    }

    [Fact]
    public void AnEntryTheTableDoesNotListIsUncoveredNotMissing()
    {
        var discoveries = CodexDiscoveries.For(Ledger(Entry(900, 18, "Stars")), CodexTable.Parse(Table), 18);

        Assert.Equal(5, discoveries.Missing.Count);
        Assert.Equal([900L], discoveries.Uncovered.Select(entry => entry.EntryId));
    }

    [Fact]
    public async Task TheAnswerNamesTheThreeMostReportedMissingEntriesInEachCategory()
    {
        var shipped = CodexCatalogue.InRegion(18);
        var logged = shipped.First(found => found.Entry.EntryId is not null).Entry.EntryId!.Value;
        var categories = shipped
            .Where(found => found.Entry.EntryId != logged)
            .GroupBy(found => found.Entry.Category)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var said = await Ask(
            Store(Entry(logged, 18), Position(0, 0)), "{\"region\":\"" + GalacticRegions.NameOf(18) + "\"}");
        var lines = said.Split(Environment.NewLine).Where(line => line.StartsWith("Most reported missing in ", StringComparison.Ordinal)).ToList();

        Assert.True(categories.Count > 1);
        Assert.Equal(categories.Count, lines.Count);

        foreach (var (group, line) in categories.Zip(lines))
        {
            var top = group
                .OrderByDescending(found => found.Systems)
                .ThenBy(found => found.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(found => found.Entry.Name);
            var rest = group.Count() - 3;

            Assert.Equal(
                $"Most reported missing in {group.Key.ToLowerInvariant()}: {string.Join(", ", top)}{(rest > 0 ? $", and {rest} more" : string.Empty)}.",
                line);
        }

        Assert.Contains($"{shipped.Count - 1:N0} missing.", said);
    }

    [Fact]
    public async Task TheAnswerEndsWithTheCoverageSentence()
    {
        var said = await Ask(Store(Entry(1, 18)), "{\"region\":\"" + GalacticRegions.NameOf(18) + "\"}");

        Assert.Contains(CodexDiscoveries.Coverage, said);
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
