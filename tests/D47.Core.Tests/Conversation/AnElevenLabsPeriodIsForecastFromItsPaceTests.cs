using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>An ElevenLabs period is forecast from the rate used so far, against the limit and the reset (#2541).</summary>
public sealed class AnElevenLabsPeriodIsForecastFromItsPaceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Reset = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset TenDaysIn = Start.AddDays(10);

    private static ElevenLabsFigures Figures(long used, LimitExtension? extension = null) => new(
        CharactersUsed: used,
        CharacterLimit: 100_000,
        NextReset: Reset,
        Refresh: RefreshPeriod.Monthly,
        Extension: extension ?? LimitExtension.Stops,
        Overage: new Money(0m, "usd"));

    [Fact]
    public void ASlowPaceLastsWithCharactersLeft()
    {
        var estimate = AccountPace.Estimate(Figures(20_000), TenDaysIn);

        Assert.Equal(PaceOutcome.Lasts, estimate.Outcome);
        Assert.Equal(40_000, estimate.Left);
        Assert.Null(estimate.LimitAt);
    }

    [Fact]
    public void AFastPaceRunsOutWhenTheLimitStopsIt()
    {
        var estimate = AccountPace.Estimate(Figures(50_000), TenDaysIn);

        Assert.Equal(PaceOutcome.RunsOut, estimate.Outcome);
        Assert.Equal(Start.AddDays(20), estimate.LimitAt);
        Assert.Equal(10, estimate.DaysBeforeReset);
    }

    [Fact]
    public void AFastPaceReachesTheLimitThenOverageWhenTheLimitExtends()
    {
        var estimate = AccountPace.Estimate(Figures(50_000, LimitExtension.UpTo(10_000)), TenDaysIn);

        Assert.Equal(PaceOutcome.ReachesLimitThenOverage, estimate.Outcome);
        Assert.Equal(Start.AddDays(20), estimate.LimitAt);
        Assert.Equal(10, estimate.DaysBeforeReset);
    }

    [Fact]
    public void AnUnlimitedExtensionAlsoReachesTheLimitThenOverage()
    {
        Assert.Equal(PaceOutcome.ReachesLimitThenOverage, AccountPace.Estimate(Figures(50_000, LimitExtension.Unlimited), TenDaysIn).Outcome);
    }

    [Fact]
    public void UsageAtALimitThatStopsIsAtTheLimit()
    {
        Assert.Equal(PaceOutcome.AtLimit, AccountPace.Estimate(Figures(100_000), TenDaysIn).Outcome);
    }

    [Fact]
    public void UsageOverALimitThatExtendsIsPastTheLimit()
    {
        Assert.Equal(PaceOutcome.PastLimit, AccountPace.Estimate(Figures(120_000, LimitExtension.UpTo(50_000)), TenDaysIn).Outcome);
    }

    [Fact]
    public void LessThanADayInGivesNoEstimateUntilTheDayIsOut()
    {
        var estimate = AccountPace.Estimate(Figures(5_000), Start.AddHours(12));

        Assert.Equal(PaceOutcome.NoEstimate, estimate.Outcome);
        Assert.Equal(NoEstimateReason.FirstDay, estimate.Reason);
        Assert.Equal(Start.AddDays(1), estimate.EstimateFrom);
    }

    [Fact]
    public void NothingUsedGivesNoEstimate()
    {
        var estimate = AccountPace.Estimate(Figures(0), TenDaysIn);

        Assert.Equal(PaceOutcome.NoEstimate, estimate.Outcome);
        Assert.Equal(NoEstimateReason.NothingUsed, estimate.Reason);
    }

    [Fact]
    public void NoResetTimeGivesNoEstimateAndNoPaceShare()
    {
        var estimate = AccountPace.Estimate(Figures(20_000) with { NextReset = null }, TenDaysIn);

        Assert.Equal(PaceOutcome.NoEstimate, estimate.Outcome);
        Assert.Equal(NoEstimateReason.NoResetTime, estimate.Reason);
        Assert.Null(estimate.PeriodGone);
    }

    [Fact]
    public void NoRefreshPeriodGivesNoEstimateAsNoResetTime()
    {
        var estimate = AccountPace.Estimate(Figures(20_000) with { Refresh = null }, TenDaysIn);

        Assert.Equal(PaceOutcome.NoEstimate, estimate.Outcome);
        Assert.Equal(NoEstimateReason.NoResetTime, estimate.Reason);
        Assert.Null(estimate.PeriodGone);
    }

    [Fact]
    public void HalfADayBeforeTheResetRoundsUpToThreeDays()
    {
        // Half used at 13.75 days: the limit falls at 27.5 days, 2.5 days before the reset.
        var estimate = AccountPace.Estimate(Figures(50_000), Start.AddHours(330));

        Assert.Equal(PaceOutcome.RunsOut, estimate.Outcome);
        Assert.Equal(3, estimate.DaysBeforeReset);
    }

    [Fact]
    public void RunningOutHoursBeforeTheResetCountsAsOneDay()
    {
        // Half used at 14.9 days: the limit falls at 29.8 days, under a fifth of a day before the reset.
        var estimate = AccountPace.Estimate(Figures(50_000), Start.AddHours(357.6));

        Assert.Equal(PaceOutcome.RunsOut, estimate.Outcome);
        Assert.Equal(1, estimate.DaysBeforeReset);
    }

    [Fact]
    public void ThePaceShareIsThePartOfThePeriodGone()
    {
        Assert.Equal(1d / 3, AccountPace.Estimate(Figures(20_000), TenDaysIn).PeriodGone!.Value, 9);
    }

    [Fact]
    public void ThePeriodStartsCalendarMonthsBeforeTheReset()
    {
        // A quarter ending 1 February starts 1 November: 92 days, 15 of them gone.
        var figures = Figures(1_000) with { NextReset = new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero), Refresh = RefreshPeriod.Quarterly };

        Assert.Equal(15d / 92, AccountPace.Estimate(figures, Start.AddDays(15)).PeriodGone!.Value, 9);
    }

    [Fact]
    public void ThePaceShareIsCarriedOnEveryResultThatHasAReset()
    {
        Assert.Equal(1d / 3, AccountPace.Estimate(Figures(100_000), TenDaysIn).PeriodGone!.Value, 9);
        Assert.Equal(1d / 3, AccountPace.Estimate(Figures(0), TenDaysIn).PeriodGone!.Value, 9);
        Assert.Equal(1d / 3, AccountPace.Estimate(Figures(50_000), TenDaysIn).PeriodGone!.Value, 9);
    }
}
