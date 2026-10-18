using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheCodexLedgerAnswerSaysWhatWasLoggedTests
{
    private const string Commander =
        """{"timestamp":"2026-07-03T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private static string Line(string time, string kind, string rest) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"" + kind + "\"," + rest + "}";

    private static string Entry(string time, long id, int region, string category, bool isNew = true) =>
        Line(
            time,
            "CodexEntry",
            "\"EntryID\":" + id + ",\"Name_Localised\":\"Entry " + id + "\",\"Category_Localised\":\"" + category
            + "\",\"Region\":\"$Codex_RegionName_" + region + ";\",\"System\":\"Sol\""
            + (isNew ? ",\"IsNewEntry\":true" : string.Empty));

    private static string Sale(string time) => Line(time, "MultiSellExplorationData", "\"TotalEarnings\":1000");

    private static string Position(string time, string x, string z) =>
        Line(time, "Location", "\"StarSystem\":\"Fixture\",\"StarPos\":[" + x + ",0," + z + "]");

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

    private static async Task<string> Ask(
        GameStateStore store, string? json = null, HistoryState history = HistoryState.Done)
    {
        var capability = JournalCapability.Create(store, () => history);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_codex_ledger", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken;
    }

    [Fact]
    public async Task TripLeavesOutAnEntryBeforeTheLastSaleAndAllKeepsIt()
    {
        var store = Store(
            Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological"),
            Sale("2026-10-12T10:00:00Z"),
            Entry("2026-10-13T10:00:00Z", 2, 18, "Biological and Geological"));

        Assert.StartsWith(
            "Since you sold exploration data on 12 October: 1 new codex entry, 1 logged in all.", await Ask(store));
        Assert.StartsWith(
            "On record since 3 July 2026: 2 new codex entries, 2 logged in all.", await Ask(store, """{"period":"all"}"""));
    }

    [Fact]
    public async Task WithNoSaleOnRecordTripAnswersFromEverything()
    {
        var said = await Ask(Store(Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological")));

        Assert.StartsWith("On record since 3 July 2026: 1 new codex entry", said);
    }

    [Fact]
    public async Task SessionCountsFromTheLastLogin()
    {
        var store = Store(
            Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological"),
            Line("2026-10-05T10:00:00Z", "LoadGame", "\"Credits\":1"),
            Entry("2026-10-05T11:00:00Z", 2, 18, "Biological and Geological", isNew: false));

        Assert.StartsWith(
            "This session: 0 new codex entries, 1 logged in all.", await Ask(store, """{"period":"session"}"""));
    }

    [Fact]
    public async Task FiveCategoriesNameThreeAndCountTwoMoreAndFiveRegionsDoTheSame()
    {
        var categories = new[] { "Astronomical Bodies", "Biological and Geological", "Anomalies", "Guardian", "Thargoid" };
        var lines = new List<string>();
        var id = 1;

        for (var i = 0; i < categories.Length; i++)
        {
            for (var n = 0; n < categories.Length - i; n++)
            {
                lines.Add(Entry("2026-10-01T10:00:00Z", id++, 18 + i, categories[i]));
            }
        }

        var said = await Ask(Store([.. lines]), """{"period":"all"}""");

        Assert.Contains(
            "Most new: 5 astronomical bodies, 4 biological and geological, 3 anomalies, and 2 more.", said);
        Assert.Contains(
            "In 5 regions: 5 in Inner Orion Spur, 4 in Hawking's Gap, 3 in Dryman's Point, and 2 more.", said);
    }

    [Fact]
    public async Task ARegionCountsOnlyItsOwnEntries()
    {
        var store = Store(
            Entry("2026-10-01T10:00:00Z", 1, 5, "Biological and Geological"),
            Entry("2026-10-01T10:01:00Z", 2, 18, "Biological and Geological"),
            Entry("2026-10-01T10:02:00Z", 3, 18, "Biological and Geological"));

        var said = await Ask(store, """{"period":"all","region":"norma arm"}""");

        Assert.StartsWith("In Norma Arm, on record since 3 July 2026: 1 new codex entry, 1 logged in all.", said);
        Assert.DoesNotContain("regions", said);
    }

    [Fact]
    public async Task HereUsesTheCurrentPositionAndSaysSoWhenThereIsNone()
    {
        var entry = Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological");

        Assert.Equal(
            "I do not know which region you are in.", await Ask(Store(entry), """{"region":"here"}"""));

        var (x, z) = FindIn("Norma Arm");
        var other = Entry("2026-10-01T10:01:00Z", 2, 5, "Biological and Geological");
        var said = await Ask(
            Store(entry, other, Position("2026-10-02T10:00:00Z", x.ToString(), z.ToString())),
            """{"period":"all","region":"here"}""");

        Assert.StartsWith("In Norma Arm, on record since 3 July 2026: 1 new codex entry, 1 logged in all.", said);
    }

    private static (int X, int Z) FindIn(string region)
    {
        for (var x = -40000; x <= 40000; x += 500)
        {
            for (var z = -20000; z <= 60000; z += 500)
            {
                if (GalacticRegions.Find(new StarPosition(x, 0, z)) == region)
                {
                    return (x, z);
                }
            }
        }

        throw new InvalidOperationException(region);
    }

    [Fact]
    public async Task AnUnknownRegionIsNamedBack()
    {
        var store = Store(Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological"));

        Assert.Equal(
            "I do not know a region called Narnia.", await Ask(store, """{"region":"Narnia"}"""));
    }

    [Fact]
    public async Task NothingInThePeriodAndNothingAtAllAreSaidApart()
    {
        var store = Store(Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological"), Sale("2026-10-12T10:00:00Z"));

        Assert.Equal(
            "No codex entries since you sold exploration data on 12 October.", await Ask(store));
        Assert.Equal("No codex entries in the journals on this PC.", await Ask(Store()));
    }

    [Fact]
    public async Task TheAnswerSaysNothingAboutVouchers()
    {
        var said = await Ask(Store(Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological")));

        Assert.DoesNotContain("voucher", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhileTheHistoryIsRunningTheAnswerGivesNoFigures()
    {
        var store = Store(Entry("2026-10-01T10:00:00Z", 1, 18, "Biological and Geological"));

        Assert.Equal(
            "I have not finished reading the journal history yet.", await Ask(store, history: HistoryState.Running));
        Assert.EndsWith(
            "did not finish, so this is only what the live journal shows.", await Ask(store, history: HistoryState.Failed));
    }

    [Theory]
    [InlineData("codex entries", "get_codex_ledger")]
    [InlineData("my codex ledger", "get_codex_ledger")]
    [InlineData("exploration data am i carrying", "get_unsold_exploration")]
    [InlineData("this session", "get_session_summary")]
    public void QuestionsReachTheirOwnTool(string asked, string tool)
    {
        var match = new KeywordRouter(TestSurface.For(new MemoryInstall()).Registry).Match(asked);

        Assert.NotNull(match);
        Assert.Equal(tool, match!.ToolName);
    }

    [Fact]
    public void TheModelMayCallTheCodexLedgerTool()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var tool = registry.All.SelectMany(capability => capability.Descriptor.Tools).Single(tool => tool.Name == "get_codex_ledger");

        Assert.False(tool.Protected);
    }
}
