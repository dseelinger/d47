using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A live codex entry is a possible first only when it is new to a whole ledger and the table rates it one (#2598).</summary>
public class ALiveCodexEntryIsJudgedAFirstTests
{
    private const long Listed = 2100101;
    private const long Unlisted = 2100102;
    private const long Unknown = 2100103;

    private static readonly CodexTable Table = CodexTable.Parse(
        "2100101\tFonticulua Fluctus - Yellow\tBiology\tOrganic\t18:5\n"
        + "2100102\tFonticulua Fluctus - Lime\tBiology\tOrganic\t3:2\n");

    private static string Entry(long id, string stamp = "2026-08-16T10:00:00Z", bool isNew = true) => $$"""
        {"timestamp":"{{stamp}}","event":"CodexEntry","EntryID":{{id}},"Name":"$Codex_Ent_{{id}};",
         "Name_Localised":"Fonticulua Fluctus - Lime","SubCategory":"$Codex_SubCategory_Organic_Structures;",
         "Region":"$Codex_RegionName_18;","Region_Localised":"Inner Orion Spur","System":"Wregoe AA-A d1",
         "SystemAddress":77,"BodyID":3{{(isNew ? ",\"IsNewEntry\":true" : "")}}}
        """;

    private static CodexLedger Ledger(bool whole, params string[] events)
    {
        var store = JournalScript.Store(
        [
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            .. events,
        ]);

        return store.Active!.CodexLedger with { IsWhole = whole };
    }

    [Fact]
    public void AnEntryTheTableDoesNotListForTheRegionIsARegionFirst()
    {
        var line = Entry(Unlisted);
        var seen = CodexFirstSeen.Of(JournalScript.Parse(line), Ledger(true, line), Table);

        Assert.NotNull(seen);
        Assert.Equal(CodexFirst.Region, seen.First);
        Assert.Equal((Unlisted, 18), (seen.EntryId, seen.RegionId));
        Assert.Equal("Fonticulua Fluctus - Lime", seen.Name);
        Assert.Equal("Inner Orion Spur", seen.Region);
        Assert.Equal(("Wregoe AA-A d1", (long?)77, (int?)3), (seen.System, seen.SystemAddress, seen.BodyId));
    }

    [Fact]
    public void AnEntryTheTableLacksIsAGalaxyFirst()
    {
        var line = Entry(Unknown);

        Assert.Equal(CodexFirst.Galaxy, CodexFirstSeen.Of(JournalScript.Parse(line), Ledger(true, line), Table)?.First);
    }

    [Fact]
    public void AnEntryTheTableListsForTheRegionIsNone()
    {
        var line = Entry(Listed);

        Assert.Null(CodexFirstSeen.Of(JournalScript.Parse(line), Ledger(true, line), Table));
    }

    [Fact]
    public void AnEntryNotNewIsNone()
    {
        var line = Entry(Unlisted, isNew: false);

        Assert.Null(CodexFirstSeen.Of(JournalScript.Parse(line), Ledger(true, line), Table));
    }

    [Fact]
    public void NothingIsJudgedBeforeTheLedgerIsWhole()
    {
        var line = Entry(Unlisted);

        Assert.Null(CodexFirstSeen.Of(JournalScript.Parse(line), Ledger(false, line), Table));
        Assert.Null(CodexFirstSeen.Of(JournalScript.Parse(line), null, Table));
    }

    [Fact]
    public void AnEntryLoggedEarlierInTheRegionIsNone()
    {
        var earlier = Entry(Unlisted, "2026-08-15T10:00:00Z");
        var line = Entry(Unlisted);

        Assert.Null(CodexFirstSeen.Of(JournalScript.Parse(line), Ledger(true, earlier, line), Table));
    }

    [Fact]
    public void TheNewestIsTheLastFirstAmongThePlainEntries()
    {
        var first = Entry(Unlisted);
        var galaxy = Entry(Unknown, "2026-08-16T10:00:01Z");
        var plain = Entry(Listed, "2026-08-16T10:00:02Z");
        var ledger = Ledger(true, first, galaxy, plain);

        var newest = CodexFirstSeen.Newest([.. new[] { first, galaxy, plain }.Select(JournalScript.Parse)], ledger, Table);

        Assert.Equal(Unknown, newest?.EntryId);
        Assert.Null(CodexFirstSeen.Newest([JournalScript.Parse(plain)], ledger, Table));
    }
}
