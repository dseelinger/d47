using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The events Elite also sends as mail, less what d47 already said, per Commander (#618).</summary>
public sealed class MailCountsWhatWasNotAlreadySaidTests : IDisposable
{
    private const string Doug = "F1";
    private const string Alt = "F2";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-mail-" + Guid.NewGuid().ToString("N"));

    public MailCountsWhatWasNotAlreadySaidTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Store => Path.Combine(_folder, MailLedger.FileName);

    private MailLedger Ledger() => new(Store, NullLogger.Instance);

    private static string Commander(string fid, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"Commander","FID":"{{fid}}","Name":"Cmdr"}""";

    private static string Completed(long mission, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"MissionCompleted","Name":"Mission_Delivery","MissionID":{{mission}},"Reward":1000}""";

    private static string Failed(long mission, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"MissionFailed","Name":"Mission_Courier","MissionID":{{mission}}}""";

    private static string Promotion(string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"Promotion","Trade":8}""";

    private static string Squadron(string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"SquadronPromotion","SquadronName":"D47","OldRank":1,"NewRank":2}""";

    private static string Goal(string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"CommunityGoalReward","CGID":7,"Name":"Build it","System":"Sol","Reward":50000}""";

    private string Journal(string name, params string[] lines)
    {
        var path = Path.Combine(_folder, $"Journal.2026-09-05T{name}.01.log");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static void Walk(MailLedger ledger, IReadOnlyList<string> files) =>
        ledger.FoldHistory(files, TestContext.Current.CancellationToken);

    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void APromotionSpokenLiveIsNotMailButTheSameFromHistoryIs()
    {
        var live = Ledger();
        Walk(live, []);
        live.Fold([Parse(Promotion("10:05:00"))], live: true, Doug);

        Assert.StartsWith("Nothing I know of", live.Compose(Doug), StringComparison.Ordinal);

        var history = Ledger();
        Walk(history, [Journal("100000", Commander(Doug, "10:00:00"), Promotion("10:05:00"))]);

        Assert.Contains("a promotion to Elite in trade", history.Compose(Doug), StringComparison.Ordinal);
    }

    [Fact]
    public void MissionResultsCountWhetherLiveOrFromHistory()
    {
        var ledger = Ledger();
        Walk(ledger, [Journal("100000", Commander(Doug, "10:00:00"), Completed(1, "10:01:00"), Completed(2, "10:02:00"))]);
        ledger.Fold([Parse(Completed(3, "10:03:00")), Parse(Failed(4, "10:04:00"))], live: true, Doug);

        Assert.Contains("three missions completed and one failed", ledger.Compose(Doug), StringComparison.Ordinal);
    }

    [Fact]
    public void HistoryAndLiveOverlappingAtStartupCountOnce()
    {
        var journal = Journal("100000", Commander(Doug, "10:00:00"), Completed(1, "10:01:00"), Promotion("10:02:00"));

        var ledger = Ledger();
        ledger.Fold([Parse(Completed(1, "10:01:00")), Parse(Promotion("10:02:00"))], live: true, Doug);
        Walk(ledger, [journal]);

        var sentence = ledger.Compose(Doug);
        Assert.Contains("one mission completed", sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("promotion", sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoCommandersInOneFolderKeepSeparateLedgersAndWatermarks()
    {
        var files = new[]
        {
            Journal("100000", Commander(Doug, "10:00:00"), Completed(1, "10:01:00"), Completed(2, "10:30:00")),
            Journal("110000", Commander(Alt, "11:00:00"), Failed(3, "11:01:00")),
        };

        var first = Ledger();
        first.MarkRead(Doug, DateTimeOffset.Parse("2026-09-05T10:10:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var ledger = Ledger();
        Walk(ledger, files);

        Assert.Contains("one mission completed", ledger.Compose(Doug), StringComparison.Ordinal);
        Assert.DoesNotContain("failed", ledger.Compose(Doug), StringComparison.Ordinal);
        Assert.Contains("one mission failed", ledger.Compose(Alt), StringComparison.Ordinal);
        Assert.Null(ledger.ReadThrough(Alt));
    }

    [Fact]
    public void WithNoWatermarkOnlyTheNewestJournalCounts()
    {
        var ledger = Ledger();
        Walk(ledger, 
        [
            Journal("080000", Commander(Doug, "08:00:00"), Completed(1, "08:01:00"), Completed(2, "08:02:00")),
            Journal("100000", Commander(Doug, "10:00:00"), Completed(3, "10:01:00")),
        ]);

        Assert.Contains("one mission completed", ledger.Compose(Doug), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSentenceNamesAtMostThreeKindsAndSaysThePanelHasTheTrueCount()
    {
        var ledger = Ledger();
        Walk(ledger, 
        [
            Journal(
                "100000",
                Commander(Doug, "10:00:00"),
                Completed(1, "10:01:00"),
                Failed(2, "10:02:00"),
                Promotion("10:03:00"),
                Squadron("10:04:00"),
                Goal("10:05:00"),
                Goal("10:06:00")),
        ]);

        Assert.Equal(
            "I can't read the inbox itself, but since you last asked: one mission completed and one failed, "
            + "a promotion to Elite in trade, and three more notices. The comms panel has the true count.",
            ledger.Compose(Doug));
    }

    [Fact]
    public void MarkingReadEmptiesTheLedgerUpToThatMomentAndSurvivesAReload()
    {
        var files = new[] { Journal("100000", Commander(Doug, "10:00:00"), Completed(1, "10:01:00"), Completed(2, "10:30:00")) };
        var through = DateTimeOffset.Parse("2026-09-05T10:10:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var ledger = Ledger();
        Walk(ledger, files);
        ledger.MarkRead(Doug, through);

        Assert.Contains("one mission completed", ledger.Compose(Doug), StringComparison.Ordinal);

        var reloaded = Ledger();
        Walk(reloaded, files);

        Assert.Equal(through, reloaded.ReadThrough(Doug));
        Assert.Contains("one mission completed", reloaded.Compose(Doug), StringComparison.Ordinal);

        reloaded.MarkRead(Doug, through.AddHours(1));

        Assert.Equal(
            "Nothing I know of since you last asked. Frontier's own notices never reach the journal.",
            reloaded.Compose(Doug));
    }
}
