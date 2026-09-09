using D47.Core;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Resetting the cost figures from the Details dialog.</summary>
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

    /// <summary>
 /// The breakdown adds up to the figure beside it, which is the whole property of the details
    /// column: a Commander who cannot reconcile the two learns to trust neither.
    /// </summary>
    [Fact]
    public void EveryModelsShareSumsToTheWindowsOwnTotal()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddHours(-1), 0.25m));
        ledger.Append(Model(Now.AddHours(-2), 0.50m) with { Model = "claude-haiku-4-5" });
        ledger.Append(Model(Now.AddHours(-3), 1.00m));

        ledger.Append(new SpendEntry
        {
            At = Now.AddHours(-1),
            Kind = SpendKind.Voice,
            ProviderId = "kokoro",
            Model = "Kokoro",
            Characters = 226,
            Priced = true,
        });

        var today = ledger.Total(SpendPeriods.Today(Now, Utc));

        Assert.Equal(today.Dollars, today.Shares.Sum(share => share.Dollars));

        // Most expensive first, because the column exists to say where the money went and alphabetical would
        // bury it.
        Assert.Equal(
            ["claude-opus-5", "claude-haiku-4-5", "Kokoro"],
            today.Shares.Select(share => share.Name));

        // The two opus charges are one group, not two rows.
        Assert.Equal(1.25m, today.Shares[0].Dollars);
        Assert.Equal(2, today.Shares[0].Charges);

        // A free provider is a group with a figure of zero rather than an absence — it was used, and a
        // provider that shows nothing looks like one that was not.
        Assert.Equal(SpendKind.Voice, today.Shares[2].Kind);
        Assert.Equal(226, today.Shares[2].Characters);
        Assert.True(today.Shares[2].Priced);
    }

 /// <summary>And a reset takes a model out of the breakdown as well as out of the total.</summary>
    [Fact]
    public void AResetRemovesAModelFromTheBreakdownAndNotOnlyFromTheFigure()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        ledger.Append(Model(Now.AddDays(-20), 2.00m) with { Model = "claude-sonnet-4" });
        ledger.Append(Model(Now.AddHours(-1), 0.25m));

        Assert.Equal(
            ["claude-sonnet-4", "claude-opus-5"],
            ledger.Total(SpendPeriods.Rolling("Last 30 days", Now, 30)).Shares.Select(share => share.Name));

        ledger.Reset(SpendPeriods.Today(Now, Utc));

        var thirty = ledger.Total(SpendPeriods.Rolling("Last 30 days", Now, 30));

        Assert.Equal(["claude-sonnet-4"], thirty.Shares.Select(share => share.Name));
        Assert.Equal(thirty.Dollars, thirty.Shares.Sum(share => share.Dollars));

        // And the mark itself is not a charge, so it never appears as a group of its own.
        Assert.DoesNotContain(thirty.Shares, share => string.IsNullOrEmpty(share.Name));
    }

    /// <summary>What each window comes to, by name, so an assertion reads as the dialog does.</summary>
    private static Dictionary<string, decimal> Windows(SpendLedger ledger) =>
        ledger.Summary(Utc).ToDictionary(row => row.Period.Name, row => row.Totals.Dollars);

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

    /// <summary>This month does not nest with the rolling windows, and that is correct.</summary>
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

    /// <summary>Nothing is deleted.</summary>
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

    /// <summary>A mark is not a charge.</summary>
    [Fact]
    public void AMarkIsNeverCountedAsSpending()
    {
        var clock = new StoppedClock(Now);
        var ledger = Ledger(clock);

        // Inside this month and outside today, so the mark is the only thing the month could wrongly pick up.
        ledger.Append(Model(Now.AddDays(-5), 1.00m));
        ledger.Reset(SpendPeriods.Today(Now, Utc));

        var month = ledger.Total(SpendPeriods.CurrentMonth(Now, Utc));

        Assert.Equal(1.00m, month.Dollars);
        Assert.Equal(1, month.Turns);
        Assert.True(month.Complete);
    }

    /// <summary>Resets compose, because a charge is dropped if any mark covers it.</summary>
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

    /// <summary>The session is the one window that is not a calendar idea, and it is offered first.</summary>
    [Fact]
    public void TheSessionLeadsTheResetListAndTheRestAreTheWindowsShown()
    {
        var clock = new StoppedClock(Now);
        var offered = Ledger(clock).Resettable(Utc, Now.AddHours(-1));

        Assert.Equal(
            ["This session", "Today", "This week", "Last 7 days", "This month", "Last 30 days"],
            offered.Select(period => period.Name));

        // Thirty, not thirty-one.
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
    /// The session's own counters are in memory and die with the process, so they are cleared alongside
    /// the mark.
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
