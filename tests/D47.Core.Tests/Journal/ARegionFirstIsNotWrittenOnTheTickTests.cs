using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The journal tick records a possible first and hands the file write off the tick.</summary>
public sealed class ARegionFirstIsNotWrittenOnTheTickTests
{
    private const long Unlisted = 2399999;

    [Fact]
    public void AFindFoldedOnTheTickIsRecordedAndWrittenOnlyByTheHandedOffWork()
    {
        Assert.Null(CodexCatalogue.Table.Entry(Unlisted));

        var fixture = new JournalTickFixture(new GameStateStore { RestoreCodexLedger = _ => CodexLedger.None });
        var file = Path.Combine(JournalTickFixture.Data, RegionFirstBook.FileName);

        fixture.Write(
            "{\"timestamp\":\"2026-10-31T12:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Fixture\",\"Credits\":1}");
        fixture.Tick.Fold(fixture.Next());

        fixture.Write(
            "{\"timestamp\":\"2026-10-31T12:05:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + Unlisted
            + ",\"Name\":\"$Codex_Ent_Unlisted;\",\"Name_Localised\":\"Unlisted\",\"Category\":\"$Codex_Category_Biology;\""
            + ",\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\",\"Region\":\"$Codex_RegionName_18;\""
            + ",\"System\":\"Sol\",\"SystemAddress\":10477373803,\"IsNewEntry\":true}");
        fixture.Tick.Fold(fixture.Next());

        Assert.Equal(CodexFirst.Galaxy, fixture.RegionFirsts.Find("F1", Unlisted, 18)?.First);
        Assert.Null(fixture.Files.ReadText(file));

        foreach (var work in fixture.OffTick)
        {
            work();
        }

        Assert.Contains("\"entryId\": " + Unlisted, fixture.Files.ReadText(file), StringComparison.Ordinal);
    }
}
