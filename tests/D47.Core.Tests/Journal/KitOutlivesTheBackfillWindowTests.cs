using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A suit or weapon bought on foot is still answerable after a restart, however long ago it was last
/// worn — there is no re-listing event to bound the search the way StoredShips does for a fleet.
/// </summary>
public class KitOutlivesTheBackfillWindowTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-kit-backfill").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>A journal named the way Elite names them, since the walk selects on that.</summary>
    private string Journal(string stamp, params string[] lines)
    {
        var file = Path.Combine(_root, $"Journal.{stamp}.01.log");

        File.WriteAllLines(file, [.. lines.Select(line => line.ReplaceLineEndings(" "))]);

        return file;
    }

    private static string BoughtSuit(string timestamp, long suitId) =>
        $$"""
          {"timestamp":"{{timestamp}}","event":"BuySuit","Name":"tacticalsuit_class3",
           "Name_Localised":"Dominator Suit","Price":300000,"SuitID":{{suitId}},
           "SuitMods":["suit_increasedbackpackcapacity"]}
          """;

    private static string SoldSuit(string timestamp, long suitId) =>
        $$"""{"timestamp":"{{timestamp}}","event":"SellSuit","SuitID":{{suitId}},"Name":"tacticalsuit_class3","Price":1,"SuitMods":[]}""";

    private static string CommanderLine(string timestamp, string fid) =>
        $$"""{"timestamp":"{{timestamp}}","event":"Commander","FID":"{{fid}}","Name":"Jameson"}""";

    /// <summary>The headline claim: a suit bought in an older file, unmentioned since, is found by the walk.</summary>
    [Fact]
    public void ASuitBoughtInTheOlderFileIsOwnedAfterTheWalk()
    {
        var older = Journal(
            "2026-08-20T100000",
            CommanderLine("2026-08-20T10:00:00Z", "F1"),
            BoughtSuit("2026-08-20T10:05:00Z", 1));

        var newer = Journal(
            "2026-08-21T100000",
            CommanderLine("2026-08-21T10:00:00Z", "F1"),
            """{"timestamp":"2026-08-21T10:05:00Z","event":"FSDJump","StarSystem":"Sol"}""");

        var found = KitBackfill.FromHistory(
            [older, newer], NullLogger.Instance, cancellation: TestContext.Current.CancellationToken);

        Assert.NotNull(found["F1"].Suits.GetValueOrDefault(1));
    }

    /// <summary>A suit bought then sold across two files is not owned.</summary>
    [Fact]
    public void ASuitBoughtThenSoldAcrossTwoFilesIsNotOwned()
    {
        var older = Journal(
            "2026-08-20T100000",
            CommanderLine("2026-08-20T10:00:00Z", "F1"),
            BoughtSuit("2026-08-20T10:05:00Z", 1));

        var newer = Journal(
            "2026-08-21T100000",
            CommanderLine("2026-08-21T10:00:00Z", "F1"),
            SoldSuit("2026-08-21T10:05:00Z", 1));

        var found = KitBackfill.FromHistory(
            [older, newer], NullLogger.Instance, cancellation: TestContext.Current.CancellationToken);

        Assert.Null(found.GetValueOrDefault("F1")?.Suits.GetValueOrDefault(1));
    }

    /// <summary>A second run with a watermark after both files reads only the newest file.</summary>
    [Fact]
    public void ASecondRunWithAWatermarkAfterBothFilesReadsOnlyTheNewestFile()
    {
        var older = Journal(
            "2026-08-20T100000",
            CommanderLine("2026-08-20T10:00:00Z", "F1"),
            BoughtSuit("2026-08-20T10:05:00Z", 1));

        var newer = Journal(
            "2026-08-21T100000",
            CommanderLine("2026-08-21T10:00:00Z", "F1"),
            BoughtSuit("2026-08-21T10:05:00Z", 2));

        var window = KitBackfill.Window(
            [older, newer], new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal([newer], window);
    }

    /// <summary>With no watermark at all, the walk reaches every journal on disk rather than a fixed floor.</summary>
    [Fact]
    public void WithNoWatermarkTheWindowIsEveryFileOnDisk()
    {
        var files = new List<string>();

        for (var day = 1; day < 60; day++)
        {
            files.Add(Journal($"2026-01-{day:00}T100000"));
        }

        Assert.Equal(files.Count, KitBackfill.Window(files, since: null).Count);
    }

    /// <summary>A reset for one Commander leaves another Commander's kit alone.</summary>
    [Fact]
    public void ANewCommanderResetLeavesAnotherCommandersKitAlone()
    {
        var journal = Journal(
            "2026-08-20T100000",
            CommanderLine("2026-08-20T10:00:00Z", "F1"),
            BoughtSuit("2026-08-20T10:05:00Z", 1),
            CommanderLine("2026-08-20T11:00:00Z", "F2"),
            BoughtSuit("2026-08-20T11:05:00Z", 2),
            """{"timestamp":"2026-08-20T12:00:00Z","event":"NewCommander","FID":"F1","Name":"Jameson","Package":"Default3"}""");

        var found = KitBackfill.FromHistory(
            [journal], NullLogger.Instance, cancellation: TestContext.Current.CancellationToken);

        Assert.False(found.ContainsKey("F1"));
        Assert.NotNull(found["F2"].Suits.GetValueOrDefault(2));
    }
}
