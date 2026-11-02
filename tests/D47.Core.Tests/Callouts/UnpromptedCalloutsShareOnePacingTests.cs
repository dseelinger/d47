using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

public sealed class UnpromptedCalloutsShareOnePacingTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Longest = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(90);

    [Fact]
    public void TheFirstTickIsHeldAndRecordedAsASpokenLine()
    {
        var pace = new UnpromptedPace(0);
        pace.Watch(AmbientSituation.Docked, Start);

        Assert.False(pace.Due(Start, Interval, Longest, Settle));
        Assert.False(pace.Due(Start + Interval - TimeSpan.FromSeconds(1), Interval, Longest, Settle));
        Assert.True(pace.Due(Start + Longest, Interval, Longest, Settle));
        Assert.Equal(0, pace.Picks);
    }

    [Fact]
    public void ASituationMustHoldForTheSettleTimeBeforeALineIsDue()
    {
        var pace = new UnpromptedPace(0);
        pace.Watch(AmbientSituation.Docked, Start);
        pace.Due(Start, Interval, Longest, Settle);

        var later = Start + Longest + TimeSpan.FromMinutes(10);
        pace.Watch(AmbientSituation.NormalSpace, later);

        Assert.False(pace.Due(later + Settle - TimeSpan.FromSeconds(1), Interval, Longest, Settle));
        Assert.True(pace.Due(later + Settle, Interval, Longest, Settle));
    }

    [Fact]
    public void AnUnchangedSituationKeepsItsStartTime()
    {
        var pace = new UnpromptedPace(0);
        pace.Watch(AmbientSituation.Docked, Start);
        pace.Due(Start, Interval, Longest, Settle);
        pace.Watch(AmbientSituation.Docked, Start + Longest);

        Assert.True(pace.Due(Start + Longest, Interval, Longest, Settle));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheWaitIsSpreadByThePickCountAndTheOffset(int offset)
    {
        var pace = new UnpromptedPace(offset);
        pace.Watch(AmbientSituation.Docked, Start);
        var now = Start;
        pace.Due(now, Interval, Longest, Settle);

        for (var picks = 0; picks < 8; picks++)
        {
            var wait = ExpectedWait(picks + offset);

            Assert.False(pace.Due(now + wait - TimeSpan.FromMilliseconds(1), Interval, Longest, Settle));
            Assert.True(pace.Due(now + wait, Interval, Longest, Settle));

            now += wait;
            pace.Spoke(now);
            Assert.Equal(picks + 1, pace.Picks);
        }
    }

    [Fact]
    public void ALongestNoGreaterThanTheIntervalGivesTheInterval()
    {
        var pace = new UnpromptedPace(2);
        pace.Watch(AmbientSituation.Docked, Start);
        pace.Due(Start, Interval, Interval, Settle);

        Assert.False(pace.Due(Start + Interval - TimeSpan.FromSeconds(1), Interval, Interval, Settle));
        Assert.True(pace.Due(Start + Interval, Interval, Interval, Settle));
    }

    private static TimeSpan ExpectedWait(int count)
    {
        var fraction = unchecked((uint)count * 2654435761u) / 4294967296.0;

        return Interval + (Longest - Interval) * fraction;
    }
}
