using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The four windows the spend dialog reports (docs/plans/change-requests.md item 2).
/// <para>
/// Two of them are elapsed durations and two are local-calendar ideas, and the difference is the
/// reason rows are stored as absolute instants: a boundary computed at query time against the
/// Commander's zone is right across a clock change, and one baked in at write time is not.
/// </para>
/// </summary>
public class SpendPeriodTests
{
    /// <summary>
    /// London, because it changes offset twice a year at a civil hour, so an hour can be skipped
    /// or repeated without midnight itself moving.
    /// </summary>
    private static readonly TimeZoneInfo London =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    /// <summary>
    /// Chatham Islands: the clocks go forward at 02:45 and the offset is not a whole number of
    /// hours, which is the case a naive "subtract the offset" would get wrong.
    /// </summary>
    private static readonly TimeZoneInfo Chatham =
        TimeZoneInfo.FindSystemTimeZoneById("Pacific/Chatham");

    [Fact]
    public void ARollingWindowIsJustElapsedTime()
    {
        var now = new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);
        var week = SpendPeriods.Rolling("Last 7 days", now, 7);

        Assert.Equal(new DateTimeOffset(2026, 8, 10, 9, 30, 0, TimeSpan.Zero), week.From);
        Assert.Equal(now, week.To);

        Assert.True(week.Holds(week.From));
        Assert.False(week.Holds(week.To.AddTicks(1)));
    }

    /// <summary>
    /// The charge a Commander is most likely to be looking for is the one just made, stamped at
    /// the same instant the windows end. An exclusive upper bound left the current turn out of
    /// all four totals — visible as "This week: nothing yet" on a page reporting a turn that had
    /// just been charged.
    /// </summary>
    [Fact]
    public void AChargeMadeThisInstantIsInsideEveryWindow()
    {
        var now = new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);

        Assert.All(
            SpendPeriods.All(now, London),
            period => Assert.True(period.Holds(now), $"{period.Name} excluded a charge made now"));
    }

    /// <summary>Sunday, as asked for — not the ISO week, which starts on Monday.</summary>
    [Fact]
    public void TheWeekStartsOnSundayInTheCommandersOwnZone()
    {
        // A Monday, so a Sunday start and a Monday start give different answers and the test can
        // tell them apart.
        var now = new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(1));
        var week = SpendPeriods.CurrentWeek(now, London);

        var localStart = TimeZoneInfo.ConvertTime(week.From, London);

        Assert.Equal(DayOfWeek.Sunday, localStart.DayOfWeek);
        Assert.Equal(new DateTime(2026, 8, 16), localStart.Date);
        Assert.Equal(TimeSpan.Zero, localStart.TimeOfDay);
    }

    [Fact]
    public void TheMonthStartsAtTheFirstLocalMidnight()
    {
        var now = new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(1));
        var month = SpendPeriods.CurrentMonth(now, London);

        var localStart = TimeZoneInfo.ConvertTime(month.From, London);

        Assert.Equal(new DateTime(2026, 8, 1), localStart.Date);
        Assert.Equal(TimeSpan.Zero, localStart.TimeOfDay);
    }

    /// <summary>
    /// The claim the storage format exists for. A charge made at 00:30 local on the first of the
    /// month is inside that month — which is only true if the boundary was worked out in the
    /// Commander's zone rather than in UTC, where it is still the previous month.
    /// </summary>
    [Fact]
    public void AChargeJustAfterLocalMidnightCountsInTheRightMonth()
    {
        // Sydney: well ahead of UTC, so local and UTC disagree about which month it is.
        var sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

        var now = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(10));
        var month = SpendPeriods.CurrentMonth(now, sydney);

        var charge = new DateTimeOffset(2026, 8, 1, 0, 30, 0, TimeSpan.FromHours(10));

        Assert.True(month.Holds(charge), "a charge after local midnight fell outside its own month");

        // And the instant before it did not.
        Assert.False(month.Holds(charge.AddHours(-1)));
    }

    /// <summary>
    /// A zone that moves its clocks at midnight itself, so one local midnight never happens and
    /// another happens twice.
    /// <para>
    /// Built rather than found. Brazil used to spring forward at midnight and would have been the
    /// natural example, but it abolished DST in 2019 — so a test naming a real zone would pass
    /// today by never reaching the branch it claims to cover. A constructed zone is the only kind
    /// that stays true.
    /// </para>
    /// </summary>
    private static TimeZoneInfo MidnightShift()
    {
        static TimeZoneInfo.TransitionTime At(int hour, int month) =>
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                new DateTime(1, 1, 1, hour, 0, 0), month, 4, DayOfWeek.Sunday);

        // Forward at 00:00 in March, so 00:00-00:59 does not exist that day. Back at 01:00 in
        // October, so 00:00-00:59 happens twice.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            At(0, 3),
            At(1, 10));

        return TimeZoneInfo.CreateCustomTimeZone(
            "D47 Midnight Shift", TimeSpan.Zero, "Midnight Shift", "Standard", "Daylight", [rule]);
    }

    /// <summary>
    /// A month beginning on a midnight that never happened. Converting it throws, so the window
    /// has to move to the first minute that exists rather than assuming 00:00 is always there.
    /// </summary>
    [Fact]
    public void AMonthStartingOnASkippedMidnightMovesToTheFirstRealMinute()
    {
        var zone = MidnightShift();

        // The fourth Sunday of March 2026 is the 22nd, and midnight that day is skipped.
        var skipped = new DateTime(2026, 3, 22, 0, 0, 0);
        Assert.True(zone.IsInvalidTime(skipped), "the constructed zone no longer skips this midnight");

        var week = SpendPeriods.CurrentWeek(new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.FromHours(1)), zone);

        // It landed on the day it names, at the first minute of it that exists.
        var localStart = TimeZoneInfo.ConvertTime(week.From, zone);

        Assert.Equal(new DateTime(2026, 3, 22), localStart.Date);
        Assert.Equal(TimeSpan.FromHours(1), localStart.TimeOfDay);
    }

    /// <summary>
    /// A week whose first midnight is repeated. The window has to start at the earlier of the two
    /// or the repeated hour falls outside it — an hour of charges silently missing from the total.
    /// </summary>
    [Fact]
    public void AnAmbiguousMidnightTakesTheEarlierInstant()
    {
        var zone = MidnightShift();

        // The fourth Sunday of October 2026 is the 25th, and midnight happens twice that day.
        var repeated = new DateTime(2026, 10, 25, 0, 30, 0);
        Assert.True(zone.IsAmbiguousTime(repeated), "the constructed zone no longer repeats this midnight");

        var week = SpendPeriods.CurrentWeek(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), zone);

        // A charge in the first pass through the repeated hour is inside the week. Taking the
        // later instant instead would put it before the window began.
        var firstPass = new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.FromHours(1));

        Assert.True(week.Holds(firstPass), "the repeated hour fell outside its own week");
    }

    /// <summary>
    /// A zone whose clocks move at a quarter past the hour, on a fractional offset. This is the
    /// arithmetic that a hand-rolled "take the offset and subtract it" gets wrong.
    /// </summary>
    [Fact]
    public void AFractionalOffsetZoneStillLandsOnLocalMidnight()
    {
        var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(12.75));
        var month = SpendPeriods.CurrentMonth(now, Chatham);

        var localStart = TimeZoneInfo.ConvertTime(month.From, Chatham);

        Assert.Equal(new DateTime(2026, 9, 1), localStart.Date);
        Assert.Equal(TimeSpan.Zero, localStart.TimeOfDay);
    }

    [Fact]
    public void AllFourWindowsAreReportedInOrder()
    {
        var now = new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);

        Assert.Equal(
            ["Last 7 days", "Last 30 days", "This week", "This month"],
            SpendPeriods.All(now, London).Select(window => window.Name));
    }
}
