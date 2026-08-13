using System.Text.Json;
using D47.Core.Coverage;
using Xunit;

namespace D47.Core.Tests.Coverage;

/// <summary>
/// The point of the ledger is the middle status. "I tested that" is only true of the thing as
/// it was when it was tested, and a definition that has changed since makes the old exercise
/// worthless — which is exactly the case a person's memory gets wrong.
/// </summary>
public class CoverageLedgerTests
{
    private static readonly DateTimeOffset Monday = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    private static CoverageItem Item(string id, string fingerprint = "aaaa") =>
        new(CoverageKind.Tool, id, "something", $"Something - {id}", fingerprint);

    [Fact]
    public void SomethingNeverRunIsNeverExercised()
    {
        var report = new CoverageLedger().Report([Item("get_status")]);

        Assert.Equal(CoverageStatus.Never, report.Lines.Single().Status);
        Assert.Equal(0, report.Exercised);
        Assert.Equal(1, report.Never);
    }

    [Fact]
    public void SomethingRunIsExercised()
    {
        var ledger = new CoverageLedger();
        var item = Item("get_status");

        ledger.Record(item, Monday, CoverageOutcome.Ok);

        var line = ledger.Report([item]).Lines.Single();

        Assert.Equal(CoverageStatus.Exercised, line.Status);
        Assert.Equal(Monday, line.LastSeen);
    }

    /// <summary>The case the whole thing exists for.</summary>
    [Fact]
    public void SomethingThatChangedSinceItWasRunIsStale()
    {
        var ledger = new CoverageLedger();

        ledger.Record(Item("get_status", "before"), Monday, CoverageOutcome.Ok);

        var line = ledger.Report([Item("get_status", "after")]).Lines.Single();

        Assert.Equal(CoverageStatus.Stale, line.Status);

        // The date it was last exercised survives, because "I looked at this in August and it
        // has moved since" is more useful than losing the fact entirely.
        Assert.Equal(Monday, line.LastSeen);
    }

    /// <summary>Ids are only unique within a kind - a tool and a row may share a name.</summary>
    [Fact]
    public void TheSameIdInTwoKindsIsTwoThings()
    {
        var ledger = new CoverageLedger();
        var tool = new CoverageItem(CoverageKind.Tool, "listening", "listening", "Tool", "aaaa");
        var row = new CoverageItem(CoverageKind.Setting, "listening", "listening", "Row", "aaaa");

        ledger.Record(tool, Monday, CoverageOutcome.Ok);

        var report = ledger.Report([tool, row]);

        Assert.Equal(CoverageStatus.Exercised, report.Lines[0].Status);
        Assert.Equal(CoverageStatus.Never, report.Lines[1].Status);
    }

    [Fact]
    public void TheSummaryCountsEachStateOnce()
    {
        var ledger = new CoverageLedger();

        ledger.Record(Item("done"), Monday, CoverageOutcome.Ok);
        ledger.Record(Item("moved", "before"), Monday, CoverageOutcome.Ok);

        var report = ledger.Report([Item("done"), Item("moved", "after"), Item("untouched")]);

        Assert.Equal(3, report.Total);
        Assert.Equal(1, report.Exercised);
        Assert.Equal(1, report.Stale);
        Assert.Equal(1, report.Never);
        Assert.Equal("1 of 3 exercised, 1 changed since, 1 never.", report.Summary);
    }

    /// <summary>
    /// The report opens with what still needs attention. A list that opens with the work is a
    /// list that gets used; one that opens with a wall of green is one that gets closed.
    /// </summary>
    [Fact]
    public void TheReportLeadsWithWhatIsLeftToDo()
    {
        var ledger = new CoverageLedger();
        ledger.Record(Item("done"), Monday, CoverageOutcome.Ok);

        var markdown = ledger.Report([Item("done"), Item("untouched")]).ToMarkdown(Monday);

        Assert.True(
            markdown.IndexOf("Never exercised", StringComparison.Ordinal)
            < markdown.IndexOf("## Exercised", StringComparison.Ordinal),
            "the never-exercised section comes first");
    }

    /// <summary>Recording is what makes it worth writing the file again.</summary>
    [Fact]
    public void TheLedgerKnowsWhenItHasSomethingNewToSave()
    {
        var ledger = new CoverageLedger();

        Assert.False(ledger.Dirty);

        ledger.Record(Item("get_status"), Monday, CoverageOutcome.Ok);
        Assert.True(ledger.Dirty);

        ledger.Saved();
        Assert.False(ledger.Dirty);
    }

    /// <summary>
    /// "It ran" and "it ran and worked" are different answers, and only the second one says
    /// whether to go back to it.
    /// </summary>
    [Fact]
    public void SomethingThatFailedIsRecordedAsFailed()
    {
        var ledger = new CoverageLedger();
        var item = Item("honk");

        ledger.Record(item, Monday, CoverageOutcome.Failed);

        var line = ledger.Report([item]).Lines.Single();

        // Still exercised — it ran. What changed is how it went.
        Assert.Equal(CoverageStatus.Exercised, line.Status);
        Assert.Equal(CoverageOutcome.Failed, line.Outcome);
        Assert.True(line.Failed);
    }

    /// <summary>Never run is not the same as ran and said nothing.</summary>
    [Fact]
    public void SomethingNeverRunHasNoOutcomeAtAll()
    {
        var line = new CoverageLedger().Report([Item("honk")]).Lines.Single();

        Assert.Null(line.Outcome);
        Assert.False(line.Failed);
    }

    /// <summary>
    /// Last write wins in both directions: something fixed since it failed goes green, and
    /// something that has started failing goes red however long it worked before.
    /// </summary>
    [Theory]
    [InlineData(CoverageOutcome.Failed, CoverageOutcome.Ok)]
    [InlineData(CoverageOutcome.Ok, CoverageOutcome.Failed)]
    public void TheLatestRunIsTheOneThatCounts(CoverageOutcome first, CoverageOutcome second)
    {
        var ledger = new CoverageLedger();
        var item = Item("honk");

        ledger.Record(item, Monday, first);
        ledger.Record(item, Monday.AddDays(1), second);

        Assert.Equal(second, ledger.Report([item]).Lines.Single().Outcome);
    }

    /// <summary>
    /// Failures are not a share of the exercised count — a failure can be stale too — so the
    /// summary keeps them in a sentence of their own rather than in the list of proportions.
    /// </summary>
    [Fact]
    public void TheSummaryCallsOutFailuresSeparately()
    {
        var ledger = new CoverageLedger();

        ledger.Record(Item("worked"), Monday, CoverageOutcome.Ok);
        ledger.Record(Item("broke"), Monday, CoverageOutcome.Failed);

        var report = ledger.Report([Item("worked"), Item("broke"), Item("untouched")]);

        Assert.Equal(1, report.Failed);
        Assert.Equal("2 of 3 exercised, 1 never. 1 came back with an error.", report.Summary);
    }

    /// <summary>
    /// Something that ran and errored is a stronger call to action than something never tried,
    /// so it leads — and it appears once, not in its failure section and again under its status.
    /// </summary>
    [Fact]
    public void TheReportLeadsWithFailuresAndListsThemOnce()
    {
        var ledger = new CoverageLedger();

        ledger.Record(Item("broke"), Monday, CoverageOutcome.Failed);
        ledger.Record(Item("worked"), Monday, CoverageOutcome.Ok);

        var markdown = ledger.Report([Item("broke"), Item("worked"), Item("untouched")]).ToMarkdown(Monday);

        Assert.True(
            markdown.IndexOf("Came back with an error", StringComparison.Ordinal)
            < markdown.IndexOf("Never exercised", StringComparison.Ordinal),
            "failures come before things never tried");

        Assert.Equal(1, markdown.Split("**broke**").Length - 1);
        Assert.Contains("## Exercised (1)", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// A coverage.json written before outcomes existed has no outcome field. It must load as
    /// "ran, nothing said otherwise" rather than as a failure nobody ever saw.
    /// </summary>
    [Fact]
    public void ARecordWrittenBeforeOutcomesExistedLoadsAsOk()
    {
        var marks = JsonSerializer.Deserialize<Dictionary<string, CoverageMark>>(
            """{"tool:honk":{"Fingerprint":"aaaa","When":"2026-08-10T09:00:00+00:00"}}""");

        var line = new CoverageLedger(marks!).Report([Item("honk")]).Lines.Single();

        Assert.Equal(CoverageStatus.Exercised, line.Status);
        Assert.Equal(CoverageOutcome.Ok, line.Outcome);
    }

    [Fact]
    public void AFingerprintIsStableAndSensitive()
    {
        Assert.Equal(CoverageLedger.Fingerprint("one"), CoverageLedger.Fingerprint("one"));
        Assert.NotEqual(CoverageLedger.Fingerprint("one"), CoverageLedger.Fingerprint("two"));
    }
}
