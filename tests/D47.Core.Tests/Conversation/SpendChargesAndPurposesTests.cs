using D47.Core;
using D47.Core.Conversation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class SpendChargesAndPurposesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly SpendPeriod Today = SpendPeriods.Today(Now, TimeZoneInfo.Utc);

    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow { get; set; } = at;
    }

    private static SpendLedger Ledger(IWallClock? clock = null) =>
        new("spend.jsonl", new MemoryFileSystem(), clock ?? new StoppedClock(Now), NullLogger.Instance);

    private static SpendEntry Charge(int hoursAgo, decimal dollars, string? purpose, SpendKind kind = SpendKind.Model, bool priced = true) => new()
    {
        At = Now.AddHours(-hoursAgo),
        Kind = kind,
        ProviderId = "anthropic",
        Model = "claude-opus-5",
        Dollars = dollars,
        Purpose = purpose,
        Priced = priced,
    };

    [Fact]
    public void AChargeInsideAResetsSpanIsReturnedByNeitherQuery()
    {
        var clock = new StoppedClock(Now.AddHours(-2));
        var ledger = Ledger(clock);
        ledger.Append(Charge(3, 1m, SpendPurposes.Conversation));
        ledger.Reset(Today);
        ledger.Append(Charge(1, 2m, SpendPurposes.Conversation));

        var charges = ledger.Charges(Today);

        Assert.Equal(2m, Assert.Single(charges).Dollars);
        Assert.Equal(2m, Assert.Single(ledger.ByPurpose(Today)).Dollars);
    }

    [Fact]
    public void AFreeChargeIsReturnedByNeitherQueryAndAnUnpricedOneIsCounted()
    {
        var ledger = Ledger();
        ledger.Append(Charge(2, 0m, SpendPurposes.Conversation));
        ledger.Append(Charge(1, 0m, SpendPurposes.Debrief, priced: false));

        var charge = Assert.Single(ledger.Charges(Today));
        Assert.False(charge.Priced);

        var share = Assert.Single(ledger.ByPurpose(Today));
        Assert.Equal(SpendPurposes.Debrief, share.Purpose);
        Assert.Equal(1, share.Charges);
        Assert.Equal(1, share.Unpriced);
        Assert.False(share.Complete);
    }

    [Fact]
    public void ARowWithNoPurposeGroupsUnderNull()
    {
        var ledger = Ledger();
        ledger.Append(Charge(2, 1m, null));
        ledger.Append(Charge(1, 2m, null));

        var share = Assert.Single(ledger.ByPurpose(Today));
        Assert.Null(share.Purpose);
        Assert.Equal(2, share.Charges);
        Assert.Equal(3m, share.Dollars);
        Assert.True(share.Complete);
    }

    [Fact]
    public void AModelRowAndAVoiceRowWithTheSamePurposeStayTwoShares()
    {
        var ledger = Ledger();
        ledger.Append(Charge(2, 1m, "shared"));
        ledger.Append(Charge(1, 2m, "shared", SpendKind.Voice));

        var shares = ledger.ByPurpose(Today);

        Assert.Equal(2, shares.Count);
        Assert.Equal([SpendKind.Model, SpendKind.Voice], shares.Select(share => share.Kind).Order());
    }

    [Fact]
    public void ThePurposeDollarsForAWindowSumToItsTotal()
    {
        var ledger = Ledger();
        ledger.Append(Charge(5, 1.25m, SpendPurposes.Conversation));
        ledger.Append(Charge(4, 0.50m, SpendPurposes.Debrief));
        ledger.Append(Charge(3, 0.75m, null, SpendKind.Voice));
        ledger.Append(Charge(2, 0.10m, "x", SpendKind.Transcription));
        ledger.Append(Charge(1, 0m, SpendPurposes.Logbook));
        ledger.Append(Charge(1, 0m, SpendPurposes.Logbook, priced: false));

        Assert.Equal(ledger.Total(Today).Dollars, ledger.ByPurpose(Today).Sum(share => share.Dollars));
    }
}
