using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class CodexEntriesAreFoldedFromJournalsTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string At(string time, string body) => "{\"timestamp\":\"" + time + "\"," + body + "}";

    private static string Line(string time, string kind, string rest = "") =>
        At(time, "\"event\":\"" + kind + "\"" + (rest.Length > 0 ? "," + rest : string.Empty));

    private static string Codex(string time, long id, int region, bool isNew = false, string extra = "") =>
        Line(
            time,
            "CodexEntry",
            "\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id + ";\",\"Name_Localised\":\"Entry " + id
            + "\",\"Category\":\"$Codex_Category_Biology;\",\"Category_Localised\":\"Biological and Geological\""
            + ",\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\",\"Region\":\"$Codex_RegionName_" + region
            + ";\",\"System\":\"Sol\",\"SystemAddress\":10477373803,\"BodyID\":5"
            + (isNew ? ",\"IsNewEntry\":true" : string.Empty)
            + extra);

    private static CodexLedger Fold(params string[] lines) =>
        lines.Aggregate(CodexLedger.None, (ledger, line) => ledger.Apply(Event(line)));

    private static string Login(string time, string fid = "F1") =>
        At(time, "\"event\":\"LoadGame\",\"FID\":\"" + fid + "\",\"Credits\":1");

    private static IReadOnlyDictionary<string, CodexLedger> Walk(params string[][] files)
    {
        var system = new MemoryFileSystem();
        var names = new List<string>();

        for (var i = 0; i < files.Length; i++)
        {
            names.Add("j/Journal." + i + ".log");
            system.WriteText(names[i], string.Join("\n", files[i]));
        }

        return CodexLedgerBackfill.FromHistory(system, names, NullLogger.Instance, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AnEntryKeepsItsIdRegionNewnessAndSystem()
    {
        var entry = Assert.Single(Fold(Codex("2026-10-10T10:00:00Z", 1201, 18, isNew: true)).Entries);

        Assert.Equal((1201L, 18, true, "Sol"), (entry.EntryId, entry.RegionId, entry.IsNew, entry.System));
        Assert.Equal(("Entry 1201", "Biological and Geological"), (entry.Name, entry.Category));
        Assert.Equal((10477373803L, 5), (entry.SystemAddress, entry.BodyId));
    }

    [Fact]
    public void RegionEighteenIsInnerOrionSpur()
    {
        Assert.Equal("Inner Orion Spur", GalacticRegions.NameOf(18));
        Assert.Null(GalacticRegions.NameOf(9999));
    }

    [Theory]
    [InlineData("$Codex_RegionName_18;", 18)]
    [InlineData("$Codex_RegionName_x;", null)]
    [InlineData("Inner Orion Spur", null)]
    [InlineData(null, null)]
    public void ARegionValueYieldsItsNumberOrNull(string? region, int? expected) =>
        Assert.Equal(expected, CodexLedger.RegionIdOf(region));

    [Fact]
    public void AnEntryWithoutAnIsNewEntryFlagIsNotNew()
    {
        Assert.False(Assert.Single(Fold(Codex("2026-10-10T10:00:00Z", 1, 18)).Entries).IsNew);
    }

    [Fact]
    public void ASurfaceEntryKeepsItsPositionAndOneWithoutHasNone()
    {
        var ledger = Fold(
            Codex("2026-10-10T10:00:00Z", 1, 18, extra: ",\"Latitude\":-12.5,\"Longitude\":33.25"),
            Codex("2026-10-10T10:01:00Z", 2, 18));

        Assert.Equal((-12.5, 33.25), (ledger.Entries[0].Latitude, ledger.Entries[0].Longitude));
        Assert.Equal((null, null), (ledger.Entries[1].Latitude, ledger.Entries[1].Longitude));
    }

    [Fact]
    public void TwoEntriesOfOneIdAndRegionAreTwoEntriesAndOneLoggedPair()
    {
        var ledger = Fold(Codex("2026-10-10T10:00:00Z", 7, 18, isNew: true), Codex("2026-10-10T11:00:00Z", 7, 18));

        Assert.Equal(2, ledger.Entries.Count);
        Assert.Single(ledger.Logged);
        Assert.Equal(1, ledger.Since(null, null).Logged);
    }

    [Fact]
    public void ANewCommanderLeavesOnlyTheLaterEntry()
    {
        var ledger = Fold(
            Codex("2026-10-10T10:00:00Z", 1, 18),
            Line("2026-10-10T11:00:00Z", "MultiSellExplorationData"),
            Line("2026-10-10T12:00:00Z", "NewCommander"),
            Codex("2026-10-10T13:00:00Z", 2, 18));

        Assert.Equal([2L], ledger.Entries.Select(entry => entry.EntryId));
        Assert.Null(ledger.TripStart);
        Assert.Equal([(2L, (int?)18)], ledger.Logged);
    }

    [Fact]
    public void TripStartIsTheNewestExplorationSaleAndNullBeforeOne()
    {
        Assert.Null(Fold(Codex("2026-10-10T10:00:00Z", 1, 18)).TripStart);

        var ledger = Fold(
            Line("2026-10-10T10:00:00Z", "SellExplorationData"),
            Line("2026-10-10T12:00:00Z", "MultiSellExplorationData"));

        Assert.Equal(DateTimeOffset.Parse("2026-10-10T12:00:00Z"), ledger.TripStart);
    }

    [Fact]
    public void SessionStartIsTheNewestLogin()
    {
        var ledger = Fold(Login("2026-10-10T09:00:00Z"), Login("2026-10-11T09:00:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2026-10-11T09:00:00Z"), ledger.SessionStart);
    }

    [Fact]
    public void SinceReturnsOnlyEntriesFromThatTimeAndRegion()
    {
        var ledger = Fold(
            Codex("2026-10-10T10:00:00Z", 1, 18, isNew: true),
            Codex("2026-10-10T12:00:00Z", 2, 5, isNew: true),
            Codex("2026-10-10T13:00:00Z", 3, 18));

        var later = ledger.Since(DateTimeOffset.Parse("2026-10-10T11:00:00Z"), null);
        var inRegion = ledger.Since(null, 18);

        Assert.Equal((2, 1, 2), (later.Entries, later.New, later.Logged));
        Assert.Equal([(int?)5, 18], later.ByRegion.Select(total => total.RegionId));
        Assert.Equal((2, 1), (inRegion.Entries, inRegion.New));
        Assert.Equal([("Biological and Geological", 2)], inRegion.ByCategory.Select(total => (total.Category, total.Count)));
    }

    [Fact]
    public void ALegacyFileAddsNothing()
    {
        var ledger = Fold(
            Line("2026-10-10T09:00:00Z", "Fileheader", "\"gameversion\":\"3.8.0.404\""),
            Codex("2026-10-10T10:00:00Z", 1, 18));

        Assert.Empty(ledger.Entries);
        Assert.Null(ledger.OnRecordSince);
    }

    [Fact]
    public void ARecordIsOnRecordSinceItsFirstEvent()
    {
        var ledger = Fold(Login("2026-10-10T09:00:00Z"), Codex("2026-10-10T10:00:00Z", 1, 18));

        Assert.Equal(DateTimeOffset.Parse("2026-10-10T09:00:00Z"), ledger.OnRecordSince);
    }

    [Fact]
    public void AMergeCountsAnEntryInTheWalkAndTheLiveJournalOnce()
    {
        var walk = Fold(Codex("2026-10-10T10:00:00Z", 1, 18), Codex("2026-10-10T10:00:30Z", 2, 18), Codex("2026-10-10T10:00:30Z", 3, 18));
        var live = Fold(
            Codex("2026-10-10T10:00:00Z", 1, 18),
            Codex("2026-10-10T10:00:30Z", 2, 18),
            Codex("2026-10-10T10:00:30Z", 3, 18),
            Codex("2026-10-10T10:00:30Z", 4, 18),
            Codex("2026-10-10T10:05:00Z", 5, 18));

        var merged = walk.With(live);

        Assert.Equal([1L, 2, 3, 4, 5], merged.Entries.Select(entry => entry.EntryId));
        Assert.Equal(5, walk.With(merged).Entries.Count);
    }

    [Fact]
    public void AMergeKeepsTheNewestSaleAndLogin()
    {
        var walk = Fold(Login("2026-10-10T09:00:00Z"), Line("2026-10-10T10:00:00Z", "SellExplorationData"));
        var live = Fold(Login("2026-10-10T12:00:00Z"));

        var merged = walk.With(live);

        Assert.Equal(DateTimeOffset.Parse("2026-10-10T10:00:00Z"), merged.TripStart);
        Assert.Equal(DateTimeOffset.Parse("2026-10-10T12:00:00Z"), merged.SessionStart);
        Assert.Equal(DateTimeOffset.Parse("2026-10-10T09:00:00Z"), merged.OnRecordSince);
    }

    [Fact]
    public void ARecordIsWholeOnlyAfterAMergeEvenWithAnEmptyWalk()
    {
        var live = Fold(Codex("2026-10-10T10:00:00Z", 1, 18));

        Assert.False(live.IsWhole);
        Assert.True(CodexLedger.None.With(live).IsWhole);
        Assert.True(live.With(CodexLedger.None).IsWhole);
    }

    [Fact]
    public void TwoCommandersEntriesInOneWalkLandInTwoRecords()
    {
        var found = Walk([
            Line("2026-10-10T08:59:00Z", "Fileheader", "\"gameversion\":\"4.1.0.0\""),
            Login("2026-10-10T09:00:00Z"),
            Codex("2026-10-10T10:00:00Z", 1, 18),
            At("2026-10-10T10:30:00Z", "\"event\":\"Commander\",\"FID\":\"F2\",\"Name\":\"Two\""),
            Login("2026-10-10T11:00:00Z", "F2"),
            Codex("2026-10-10T12:00:00Z", 2, 5),
            Line("2026-10-10T12:30:00Z", "Music"),
        ]);

        Assert.Equal([1L], found["F1"].Entries.Select(entry => entry.EntryId));
        Assert.Equal([2L], found["F2"].Entries.Select(entry => entry.EntryId));
    }

    [Fact]
    public void AWalkSkipsALegacyFile()
    {
        var found = Walk([
            Line("2026-10-10T08:59:00Z", "Fileheader", "\"gameversion\":\"3.8.0.404\""),
            Login("2026-10-10T09:00:00Z"),
            Codex("2026-10-10T10:00:00Z", 1, 18),
        ]);

        Assert.Empty(found);
    }

    [Fact]
    public void ANewCommanderInAWalkClearsTheCommanderItNames()
    {
        var found = Walk([
            Login("2026-10-10T09:00:00Z"),
            Codex("2026-10-10T10:00:00Z", 1, 18),
            Login("2026-10-10T11:00:00Z", "F2"),
            Codex("2026-10-10T12:00:00Z", 2, 18),
            At("2026-10-10T13:00:00Z", "\"event\":\"NewCommander\",\"FID\":\"F1\",\"Name\":\"Again\""),
        ]);

        Assert.False(found.ContainsKey("F1"));
        Assert.Single(found["F2"].Entries);
    }

    [Fact]
    public void AWalkReadsEntriesOldestFirstAcrossFiles()
    {
        var found = Walk(
            [Login("2026-10-10T09:00:00Z"), Codex("2026-10-10T10:00:00Z", 1, 18)],
            [Login("2026-10-11T09:00:00Z"), Codex("2026-10-11T10:00:00Z", 2, 18)]);

        Assert.Equal([1L, 2], found["F1"].Entries.Select(entry => entry.EntryId));
    }
}
