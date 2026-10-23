using D47.Core.Capabilities;
using D47.Core.Usage;
using Xunit;

namespace D47.Core.Tests.Usage;

public class UsageLedgerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 10, 22);

    [Fact]
    public void CountsAddPerDayToolAndCaller()
    {
        var ledger = new UsageLedger();
        ledger.Record("plot_route", ToolCaller.Commander, Noon);
        ledger.Record("plot_route", ToolCaller.Commander, Noon);
        ledger.Record("plot_route", ToolCaller.Model, Noon);
        ledger.Record("get_status", ToolCaller.Commander, Noon);
        ledger.Record("plot_route", ToolCaller.Commander, Noon.AddDays(1));

        Assert.Equal(
            """{"day":"2026-10-22","uses":[{"tool":"get_status","caller":"commander","count":1},{"tool":"plot_route","caller":"commander","count":2},{"tool":"plot_route","caller":"model","count":1}]}""",
            ledger.Report(Day));
        Assert.Contains("\"count\":1", ledger.Report(Day.AddDays(1)), StringComparison.Ordinal);
    }

    [Fact]
    public void AUseJustBeforeMidnightUtcAndOneAtMidnightLandOnDifferentDays()
    {
        var ledger = new UsageLedger();
        ledger.Record("a", ToolCaller.Commander, new DateTimeOffset(2026, 10, 22, 23, 59, 59, TimeSpan.Zero));
        ledger.Record("a", ToolCaller.Commander, new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal([Day, Day.AddDays(1)], ledger.CompletedDays(Day.AddDays(2)));
    }

    [Fact]
    public void AnOffsetTimeIsCountedOnItsUtcDay()
    {
        var ledger = new UsageLedger();
        ledger.Record("a", ToolCaller.Commander, new DateTimeOffset(2026, 10, 23, 1, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal([Day], ledger.CompletedDays(Day.AddDays(1)));
    }

    [Fact]
    public void CompletedDaysExcludeTheGivenDay()
    {
        var ledger = new UsageLedger();
        ledger.Record("a", ToolCaller.Commander, Noon);
        ledger.Record("a", ToolCaller.Commander, Noon.AddDays(1));

        Assert.Equal([Day], ledger.CompletedDays(Day.AddDays(1)));
        Assert.Empty(ledger.CompletedDays(Day));
    }

    [Fact]
    public void ADayWithNoCountsHasNoReport()
    {
        Assert.Null(new UsageLedger().Report(Day));
    }

    [Fact]
    public void ARemovedDayIsGone()
    {
        var ledger = new UsageLedger();
        ledger.Record("a", ToolCaller.Commander, Noon);
        ledger.Remove(Day);

        Assert.Null(ledger.Report(Day));
        Assert.Empty(ledger.CompletedDays(Day.AddDays(1)));
    }

    [Fact]
    public void DaysOverThirtyOldAreDropped()
    {
        var ledger = new UsageLedger();
        ledger.Record("a", ToolCaller.Commander, Noon);
        ledger.Record("a", ToolCaller.Commander, Noon.AddDays(1));

        ledger.DropOld(Day.AddDays(31));

        Assert.Null(ledger.Report(Day));
        Assert.NotNull(ledger.Report(Day.AddDays(1)));
    }

    [Fact]
    public void ClearEmptiesTheLedger()
    {
        var ledger = new UsageLedger();
        ledger.Record("a", ToolCaller.Commander, Noon);
        ledger.Clear();

        Assert.Empty(ledger.CompletedDays(Day.AddDays(1)));
    }

    [Fact]
    public void JsonRoundTrips()
    {
        var ledger = new UsageLedger();
        ledger.Record("plot_route", ToolCaller.Commander, Noon);
        ledger.Record("plot_route", ToolCaller.Model, Noon.AddDays(-1));
        ledger.Record("plot_route", ToolCaller.Model, Noon.AddDays(-1));

        var parsed = UsageLedger.Parse(ledger.ToJson());

        Assert.Equal(ledger.ToJson(), parsed.ToJson());
        Assert.Equal(ledger.Report(Day), parsed.Report(Day));
        Assert.Equal(ledger.Report(Day.AddDays(-1)), parsed.Report(Day.AddDays(-1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"days":[]}""")]
    [InlineData("""{"days":{"yesterday":[]}}""")]
    [InlineData("""{"days":{"2026-10-22":[{"tool":"a","caller":"alien","count":1}]}}""")]
    [InlineData("""{"days":{"2026-10-22":[{"tool":"a","caller":"model"}]}}""")]
    [InlineData("""{"days":{"2026-10-22":[{"tool":"a","caller":"model","count":0}]}}""")]
    public void MalformedJsonParsesToAnEmptyLedger(string json)
    {
        var ledger = UsageLedger.Parse(json);

        Assert.Empty(ledger.CompletedDays(DateOnly.MaxValue));
    }
}
