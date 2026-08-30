using D47.Core;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Resetting the cost figures from the Details dialog
/// (<a href="https://github.com/dseelinger/d47/issues/197">#197</a>).
/// <para>
/// <b>The rule the ask states is that a reset affects the things it rolls up to</b> — and the data
/// model already made that true. Every period figure is a query over an append-only ledger rather
/// than a running counter, so a charge that stops counting leaves every window that contained it
/// at once. There is nothing to keep in step; what these assert is that the mark is honoured
/// everywhere and that nothing outside it moved.
/// </para>
/// <para>
/// <b>A mark rather than a deletion</b>, settled by the Commander on 2026-08-30: the file stays
/// append-only, the history stays recoverable, and an accidental reset is undone by deleting one
/// line. This is the one number in the app that represents real money.
/// </para>
/// <para>
/// Nothing here reads a clock. The ledger is handed one, which is what lets a month of spending be
/// written in a millisecond and reset at a date that is not today.
/// </para>
/// </summary>
public class ResettingTheSpendFiguresTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-spend-reset-tests",
        Guid.NewGuid().ToString("n"));

    private string File => Path.Combine(_root, "spend.jsonl");

    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow { get; set; } = at;
    }

    /// <summary>Midday on a Wednesday, well inside a month and a week.</summary>
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    public ResettingTheSpendFiguresTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private SpendLedger Ledger(IWallClock clock) => new(File, clock, NullLogger.Instance);

    private static SpendEntry Model(DateTimeOffset at, decimal dollars) => new()
    {
        At = at,
        Kind = SpendKind.Model,
        ProviderId = "anthropic",
        Model = "claude-opus-5",
        Dollars = dollars,
        Priced = true,
    };

    /// <summary>What each window comes to, by name, so an assertion reads as the dialog does.</summary>
    private static Dictionary<string, decimal> Windows(SpendLedger ledger) =>
        ledger.Summary(Utc).ToDictionary(row => row.Period.Name, row => row.Totals.Dollars);

    /// <summary>
    /// <b>The whole of the ask, in one assertion.</b> Reset today and today's charges leave
    /// today, this week, the last 7 days, the last 30 days and this month — every window whose
    /// span contained them — while everything older stands untouched.
    /// </summary>
    [Fact]
    public void ResettingAWindowShrinksEveryWindowThatContainedIt()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddDays(-20), 1.00m));   // in the 30-day window and this month
        ledger.Append(Model(Now.AddDays(-3), 0.50m));    // and in the last 7 days
        ledger.Append(Model(Now.AddHours(-2), 0.25m));   // and in today

        var before = Windows(ledger);

        Assert.Equal(0.25m, before["Today"]);
        Assert.Equal(0.75m, before["Last 7 days"]);
        Assert.Equal(1.75m, before["Last 30 days"]);

        ledger.Reset(SpendPeriods.Today(Now, Utc));

        var after = Windows(ledger);

        Assert.Equal(0m, after["Today"]);

        // Every larger window dropped by exactly what today held, and by nothing else.
        Assert.Equal(before["Last 7 days"] - 0.25m, after["Last 7 days"]);
        Assert.Equal(before["Last 30 days"] - 0.25m, after["Last 30 days"]);
        Assert.Equal(before["This week"] - 0.25m, after["This week"]);
        Assert.Equal(before["This month"] - 0.25m, after["This month"]);
    }

    /// <summary>
    /// And resetting a wide window takes the narrow ones inside it with it. The rule is set
    /// semantics rather than a hierarchy, so it holds in both directions.
    /// </summary>
    [Fact]
    public void ResettingAWideWindowEmptiesTheOnesInsideIt()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddDays(-20), 1.00m));
        ledger.Append(Model(Now.AddHours(-2), 0.25m));

        ledger.Reset(SpendPeriods.Rolling("Last 30 days", Now, 30));

        Assert.All(Windows(ledger).Values, dollars => Assert.Equal(0m, dollars));
    }

    /// <summary>
    /// <b>This month does not nest with the rolling windows, and that is correct.</b> Resetting it
    /// on the 19th drops eighteen days and leaves the rest of the last 30 standing — set
    /// semantics, not a tree. Written down so a surviving figure is not later mistaken for a
    /// defect and "fixed".
    /// </summary>
    [Fact]
    public void ResettingThisMonthLeavesTheOlderPartOfTheRollingWindow()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        // The 10th of the previous month: inside Last 30 days, outside This month.
        ledger.Append(Model(new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero), 2.00m));
        ledger.Append(Model(Now.AddDays(-2), 0.50m));

        ledger.Reset(SpendPeriods.CurrentMonth(Now, Utc));

        var after = Windows(ledger);

        Assert.Equal(0m, after["This month"]);
        Assert.Equal(2.00m, after["Last 30 days"]);
    }

    /// <summary>
    /// <b>Nothing is deleted.</b> The rows are still there and the file is still append-only —
    /// which is what makes the act auditable and an accidental reset undoable by deleting one
    /// line by hand.
    /// </summary>
    [Fact]
    public void TheRowsAreStillOnDiskAfterAReset()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddHours(-2), 0.25m));
        ledger.Reset(SpendPeriods.Today(Now, Utc));

        var charges = ledger.Entries.Where(entry => !entry.IsReset).ToList();

        Assert.Equal(0.25m, Assert.Single(charges).Dollars);
        Assert.Contains(ledger.Entries, entry => entry.IsReset);
    }

    /// <summary>And the mark survives a restart, or the figures would come back on the next launch.</summary>
    [Fact]
    public void TheMarkSurvivesARestart()
    {
        var clock = new StoppedClock(Now);

        var first = Ledger(clock);
        first.Append(Model(Now.AddHours(-2), 0.25m));
        first.Reset(SpendPeriods.Today(Now, Utc));

        Assert.Equal(0m, Windows(Ledger(clock))["Today"]);
    }

    /// <summary>
    /// A mark is not a charge. It must not be counted as a turn, summed, or make a window read as
    /// partly unpriced — the last of which is why it is written as priced despite pricing nothing.
    /// </summary>
    [Fact]
    public void AMarkIsNeverCountedAsSpending()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        // Inside this month and outside today, so the mark is the only thing the month could
        // wrongly pick up.
        ledger.Append(Model(Now.AddDays(-5), 1.00m));
        ledger.Reset(SpendPeriods.Today(Now, Utc));

        var month = ledger.Total(SpendPeriods.CurrentMonth(Now, Utc));

        Assert.Equal(1.00m, month.Dollars);
        Assert.Equal(1, month.Turns);
        Assert.True(month.Complete);
    }

    /// <summary>
    /// Resets compose, because a charge is dropped if <em>any</em> mark covers it. Two resets in a
    /// row must not resurrect what the first one cleared.
    /// </summary>
    [Fact]
    public void TwoResetsDoNotUndoEachOther()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddDays(-3), 0.50m));
        ledger.Append(Model(Now.AddHours(-2), 0.25m));

        ledger.Reset(SpendPeriods.Today(Now, Utc));
        ledger.Reset(SpendPeriods.Rolling("Last 7 days", Now, 7));

        Assert.Equal(0m, Windows(ledger)["Last 7 days"]);
        Assert.Equal(0m, Windows(ledger)["Today"]);
    }

    /// <summary>
    /// <b>A charge made after a reset counts again.</b> The mark is a closed interval ending at
    /// the instant it was written, not a floor that swallows the future — which is what "start the
    /// counter from here" has to mean.
    /// </summary>
    [Fact]
    public void SpendingAfterAResetCountsAgain()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddHours(-2), 0.25m));
        ledger.Reset(SpendPeriods.Today(Now, Utc));

        clock.UtcNow = Now.AddMinutes(5);
        ledger.Append(Model(clock.UtcNow, 0.10m));

        Assert.Equal(0.10m, Windows(ledger)["Today"]);
    }

    /// <summary>What the caller is told, so the confirmation can name the figure it is clearing.</summary>
    [Fact]
    public void AResetReportsWhatItCleared()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddHours(-2), 0.25m));

        Assert.Equal(0.25m, ledger.Reset(SpendPeriods.Today(Now, Utc)).Dollars);
    }

    /// <summary>
    /// <b>The session is the one window that is not a calendar idea</b>, and it is offered first.
    /// The ledger records instants and not session ids, so it can only mean everything since
    /// launch.
    /// </summary>
    [Fact]
    public void TheSessionLeadsTheResetListAndTheRestAreTheWindowsShown()
    {
        var clock = new StoppedClock(Now);
        var offered = Ledger(clock).Resettable(Utc, Now.AddHours(-1));

        Assert.Equal(
            ["This session", "Today", "Last 7 days", "Last 30 days", "This week", "This month"],
            offered.Select(period => period.Name));

        // Thirty, not thirty-one. Settled on 2026-08-30: the reason 31 was asked for is what
        // This month already does, and a reset list offering a span the figures list does not
        // show would be two lists disagreeing about what a window is.
        Assert.DoesNotContain(offered, period => period.Name.Contains("31", StringComparison.Ordinal));
        Assert.Equal(offered.Skip(1).Select(p => p.Name), SpendPeriods.All(Now, Utc).Select(p => p.Name));
    }

    [Fact]
    public void ResettingTheSessionLeavesWhatWasSpentBeforeItStarted()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);
        var launched = Now.AddHours(-1);

        ledger.Append(Model(Now.AddHours(-5), 0.90m));   // before this session started
        ledger.Append(Model(Now.AddMinutes(-10), 0.10m)); // during it

        ledger.Reset(ledger.Resettable(Utc, launched)[0]);

        Assert.Equal(0.90m, Windows(ledger)["Today"]);
    }

    /// <summary>
    /// The session's own counters are in memory and die with the process, so they are cleared
    /// alongside the mark. Half of that would leave the session block showing figures the totals
    /// below no longer include, which is the confusing outcome.
    /// </summary>
    [Fact]
    public void TheSessionCountersEmptyToo()
    {
        var tracker = new SpendTracker();

        tracker.Record(
            new TurnCost(new LlmUsage(200, 50, 4_000, 0), 0.25m, Priced: true),
            coldPrefixExpected: true);

        Assert.Equal(1, tracker.TurnCount);

        tracker.Forget();

        Assert.Equal(0, tracker.TurnCount);
        Assert.Equal(0m, tracker.RunningTotalDollars);
        Assert.Null(tracker.Last);
        Assert.Equal(0, tracker.UnexplainedColdPrefixes);
        Assert.Equal(0, tracker.UnmeasuredPrefixes);
    }

    [Fact]
    public void AndSoDoTheSpeechCounters()
    {
        var speech = new D47.Core.Audio.SpeechSpend();

        speech.Record("elevenlabs", 400);

        Assert.Equal(400, speech.TotalCharacters);

        speech.Forget();

        Assert.Equal(0, speech.TotalCharacters);
        Assert.Empty(speech.Charges);
    }
}
