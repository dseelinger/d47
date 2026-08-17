using D47.Core;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Charges kept between runs, so "what has this cost this month" has an answer
/// (docs/plans/change-requests.md item 2).
/// <para>
/// Nothing here reads a clock. The ledger is handed one, which is what lets a month of spending
/// be written in a millisecond and queried at a date that is not today.
/// </para>
/// </summary>
public class SpendLedgerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-spend-tests",
        Guid.NewGuid().ToString("n"));

    private string File => Path.Combine(_root, "spend.jsonl");

    /// <summary>A clock that is wherever the test puts it.</summary>
    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow { get; set; } = at;
    }

    private static readonly DateTimeOffset Noon =
        new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    public SpendLedgerTests() => Directory.CreateDirectory(_root);

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

    private SpendLedger Ledger(IWallClock clock) =>
        new(File, clock, NullLogger.Instance);

    private static SpendEntry Model(decimal dollars, bool priced = true) => new()
    {
        Kind = SpendKind.Model,
        ProviderId = "anthropic",
        Model = "claude-opus-5",
        Dollars = dollars,
        Priced = priced,
        InputTokens = 1200,
        CacheReadTokens = 8000,
        OutputTokens = 300,
    };

    [Fact]
    public void ChargesSurviveTheProcess()
    {
        var clock = new StoppedClock(Noon);

        Ledger(clock).Append(Model(0.0125m));

        // A second ledger over the same file, which is what the next launch has.
        var next = Ledger(clock);

        Assert.Single(next.Entries);
        Assert.Equal(0.0125m, next.Entries[0].Dollars);
        Assert.Equal("claude-opus-5", next.Entries[0].Model);
        Assert.Equal(Noon, next.Entries[0].At);
    }

    /// <summary>
    /// The instant comes from the injected clock, so a row says when it happened rather than when
    /// it was read back.
    /// </summary>
    [Fact]
    public void EachRowIsStampedWhenItHappened()
    {
        var clock = new StoppedClock(Noon);
        var ledger = Ledger(clock);

        ledger.Append(Model(0.01m));

        clock.UtcNow = Noon.AddDays(3);
        ledger.Append(Model(0.02m));

        Assert.Equal([Noon, Noon.AddDays(3)], ledger.Entries.Select(e => e.At));
    }

    /// <summary>
    /// The windows are the point. A charge from last month is outside "this month" and inside
    /// "last 30 days", and no session-scoped total could tell them apart.
    /// </summary>
    [Fact]
    public void TotalsAreReportedPerWindow()
    {
        var clock = new StoppedClock(Noon);
        var ledger = Ledger(clock);

        clock.UtcNow = Noon.AddDays(-40);
        ledger.Append(Model(1.00m));

        clock.UtcNow = Noon.AddDays(-20);
        ledger.Append(Model(0.50m));

        clock.UtcNow = Noon.AddHours(-2);
        ledger.Append(Model(0.25m));

        clock.UtcNow = Noon;

        var summary = ledger.Summary(TimeZoneInfo.Utc).ToDictionary(row => row.Period.Name, row => row.Totals);

        Assert.Equal(0.25m, summary["Last 7 days"].Dollars);
        Assert.Equal(0.75m, summary["Last 30 days"].Dollars);

        // 17 August 2026 is a Monday, so "this week" began yesterday and holds only the newest.
        Assert.Equal(0.25m, summary["This week"].Dollars);

        // The month began on 1 August. Both older charges are in July — 20 days before the 17th
        // is the 28th — so this is the window that most obviously is not "the last 30 days".
        Assert.Equal(0.25m, summary["This month"].Dollars);
    }

    /// <summary>
    /// Model and voice are summed apart and reported together. Ledgering only the model would
    /// leave a figure that looks authoritative while covering half the cost.
    /// </summary>
    [Fact]
    public void ModelAndVoiceAreBothCountedAndKeptApart()
    {
        var clock = new StoppedClock(Noon);
        var ledger = Ledger(clock);

        ledger.Append(Model(0.20m));
        ledger.Append(new SpendEntry
        {
            Kind = SpendKind.Voice,
            ProviderId = "elevenlabs",
            Model = "ElevenLabs",
            Dollars = 0.05m,
            Priced = true,
            Characters = 420,
        });

        var totals = ledger.Total(SpendPeriods.Rolling("day", clock.UtcNow.AddDays(1), 2));

        Assert.Equal(0.20m, totals.ModelDollars);
        Assert.Equal(0.05m, totals.VoiceDollars);
        Assert.Equal(0.25m, totals.Dollars);
        Assert.Equal(1, totals.Turns);
        Assert.Equal(420, totals.Characters);
    }

    /// <summary>
    /// A model with no price behind it makes the window a floor rather than a total, and the
    /// figure has to say so. Treating an unknown cost as zero is wrong in the direction that
    /// reassures.
    /// </summary>
    [Fact]
    public void AnUnpricedRowMakesTheWindowIncomplete()
    {
        var clock = new StoppedClock(Noon);
        var ledger = Ledger(clock);

        ledger.Append(Model(0.20m));
        Assert.True(ledger.Total(SpendPeriods.Rolling("d", Noon.AddHours(1), 1)).Complete);

        ledger.Append(Model(0m, priced: false));
        Assert.False(ledger.Total(SpendPeriods.Rolling("d", Noon.AddHours(1), 1)).Complete);
    }

    /// <summary>
    /// The commonest way this file breaks is a process killed mid-append, leaving half a line.
    /// One truncated row must cost one row — not the history, and not the launch.
    /// </summary>
    [Fact]
    public void AHalfWrittenRowIsSkippedRatherThanTakingTheFileWithIt()
    {
        var clock = new StoppedClock(Noon);
        Ledger(clock).Append(Model(0.10m));

        System.IO.File.AppendAllText(File, "{\"at\":\"2026-08-17T12:00:00+00:00\",\"doll");

        var next = Ledger(clock);

        Assert.Single(next.Entries);
        Assert.Equal(0.10m, next.Entries[0].Dollars);

        // And it can still be appended to afterwards, so a broken tail is not a dead ledger.
        next.Append(Model(0.30m));
        Assert.Equal(2, Ledger(clock).Entries.Count);
    }

    /// <summary>Nothing written yet is an empty history, not a failure.</summary>
    [Fact]
    public void AMissingFileReadsAsNoHistory()
    {
        var ledger = Ledger(new StoppedClock(Noon));

        Assert.Empty(ledger.Entries);
        Assert.Equal(SpendTotals.Nothing, ledger.Total(SpendPeriods.Rolling("d", Noon, 30)));
    }
}
