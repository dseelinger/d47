using D47.Core.Knowledge;
using D47.Core.Watches;
using Xunit;

namespace D47.Core.Tests.Watches;

public class AStockWatchHoldsOnlyOnAFreshReadingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static Watch Above(int threshold) => new(
        Guid.NewGuid(), "Relics", WatchKind.Stock,
        MarketId: 128000001, Station: "Sample Port", System: "Sample", Commodity: "Soontill Relics",
        Comparison: StockComparison.Above, Threshold: threshold);

    private static StationQuote Quote(int stock, DateTimeOffset? updated) => new(stock, 2, 0, 0, 0, updated);

    [Fact]
    public void StockAtTheThresholdDoesNotHold() =>
        Assert.False(WatchRule.StockHolds(Above(10), Quote(10, Now.AddMinutes(-5)), Now));

    [Fact]
    public void StockOneAboveTheThresholdHolds() =>
        Assert.True(WatchRule.StockHolds(Above(10), Quote(11, Now.AddMinutes(-5)), Now));

    [Fact]
    public void StockBelowTheThresholdHoldsForABelowWatch() =>
        Assert.True(WatchRule.StockHolds(Above(10) with { Comparison = StockComparison.Below }, Quote(9, Now), Now));

    [Fact]
    public void AReadingEightHoursAndOneMinuteOldDoesNotHold() =>
        Assert.False(WatchRule.StockHolds(Above(10), Quote(50, Now.AddHours(-8).AddMinutes(-1)), Now));

    [Fact]
    public void AReadingExactlyEightHoursOldStillHolds() =>
        Assert.True(WatchRule.StockHolds(Above(10), Quote(50, Now.AddHours(-CommunityGoalSearch.MaxPriceAgeHours)), Now));

    [Fact]
    public void AnUndatedReadingDoesNotHold() =>
        Assert.False(WatchRule.StockHolds(Above(10), Quote(50, null), Now));

    [Fact]
    public void NoReadingDoesNotHold() =>
        Assert.False(WatchRule.StockHolds(Above(10), null, Now));
}
