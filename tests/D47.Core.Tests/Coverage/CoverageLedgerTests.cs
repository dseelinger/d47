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
        new(CoverageKind.Tool, id, $"Something - {id}", fingerprint);

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

        ledger.Record(item, Monday);

        var line = ledger.Report([item]).Lines.Single();

        Assert.Equal(CoverageStatus.Exercised, line.Status);
        Assert.Equal(Monday, line.LastSeen);
    }

    /// <summary>The case the whole thing exists for.</summary>
    [Fact]
    public void SomethingThatChangedSinceItWasRunIsStale()
    {
        var ledger = new CoverageLedger();

        ledger.Record(Item("get_status", "before"), Monday);

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
        var tool = new CoverageItem(CoverageKind.Tool, "listening", "Tool", "aaaa");
        var row = new CoverageItem(CoverageKind.Setting, "listening", "Row", "aaaa");

        ledger.Record(tool, Monday);

        var report = ledger.Report([tool, row]);

        Assert.Equal(CoverageStatus.Exercised, report.Lines[0].Status);
        Assert.Equal(CoverageStatus.Never, report.Lines[1].Status);
    }

    [Fact]
    public void TheSummaryCountsEachStateOnce()
    {
        var ledger = new CoverageLedger();

        ledger.Record(Item("done"), Monday);
        ledger.Record(Item("moved", "before"), Monday);

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
        ledger.Record(Item("done"), Monday);

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

        ledger.Record(Item("get_status"), Monday);
        Assert.True(ledger.Dirty);

        ledger.Saved();
        Assert.False(ledger.Dirty);
    }

    [Fact]
    public void AFingerprintIsStableAndSensitive()
    {
        Assert.Equal(CoverageLedger.Fingerprint("one"), CoverageLedger.Fingerprint("one"));
        Assert.NotEqual(CoverageLedger.Fingerprint("one"), CoverageLedger.Fingerprint("two"));
    }
}
