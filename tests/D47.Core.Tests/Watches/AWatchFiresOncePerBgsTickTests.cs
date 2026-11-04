using D47.Core.Knowledge;
using D47.Core.Watches;
using Xunit;

namespace D47.Core.Tests.Watches;

public class AWatchFiresOncePerBgsTickTests
{
    private static readonly DateTimeOffset Tick = new(2026, 11, 3, 6, 40, 0, TimeSpan.Zero);

    [Fact]
    public void WithNoTickKnownAWatchNeverFires() =>
        Assert.False(WatchRule.MayFire(null, null));

    [Fact]
    public void AWatchThatHasNeverFiredMayFire() =>
        Assert.True(WatchRule.MayFire(Tick, null));

    [Fact]
    public void AFiringThirtyMinutesBeforeTheSameTickCountsAsThatTick() =>
        Assert.False(WatchRule.MayFire(Tick, BgsTickWatch.Format(Tick.AddMinutes(-30))));

    [Fact]
    public void AFiringForTheSameTickBlocksAnother() =>
        Assert.False(WatchRule.MayFire(Tick, BgsTickWatch.Format(Tick)));

    [Fact]
    public void AFiringForThePreviousDaysTickAllowsANewOne() =>
        Assert.True(WatchRule.MayFire(Tick, BgsTickWatch.Format(Tick.AddDays(-1))));

    [Fact]
    public void AnUnreadableFiringIsTreatedAsNone() =>
        Assert.True(WatchRule.MayFire(Tick, "not a time"));
}
