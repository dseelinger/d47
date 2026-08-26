using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The five windows the spend dialog reports (docs/plans/change-requests.md item 2; Today added
/// by <a href="https://github.com/dseelinger/d47/issues/62">#62</a>).
/// <para>
/// Two of them are elapsed durations and three are local-calendar ideas, and the difference is the
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
    /// every total — visible as "This week: nothing yet" on a page reporting a turn that had
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

    /// <summary>
    /// Today leads the list, because the freshest window is the most-read one — the same instinct
    /// that closes every window at both ends.
    /// </summary>
    [Fact]
    public void TodayIsFirstAndTheListIsFive()
    {
        var now = new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);
        var all = SpendPeriods.All(now, London);

        Assert.Equal(5, all.Count);
        Assert.Equal("Today", all[0].Name);
        Assert.Equal(
            ["Today", "Last 7 days", "Last 30 days", "This week", "This month"],
            all.Select(period => period.Name));
    }

    /// <summary>
    /// Named as a decision rather than left as an absence. "What have I spent today" is a question
    /// a Commander asks; "what have I spent in the last twenty-four hours" is the same number with
    /// a boundary they cannot point at, and the class doc's defence of carrying both kinds of
    /// window would otherwise read as an invitation to complete the set.
    /// </summary>
    [Fact]
    public void ThereIsNoRollingTwentyFourHourTwin()
    {
        var now = new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);

        Assert.DoesNotContain(
            SpendPeriods.All(now, London),
            period => period.Name.Contains("24", StringComparison.Ordinal));
    }

    /// <summary>
    /// The trap Today inherits rather than re-solves. On 29 March 2026 London goes forward at
    /// 01:00, so the day still starts at midnight GMT — but the day is 23 hours long, and a naive
    /// "now minus 24 hours" would reach back into the previous day. The window is anchored to the
    /// midnight that happened, not to a duration.
    /// </summary>
    [Fact]
    public void TodayStartsAtTheMidnightThatHappened()
    {
        // 14:00 local on the day the clocks went forward, which is 13:00 UTC (BST is +1 by then).
        var now = new DateTimeOffset(2026, 3, 29, 13, 0, 0, TimeSpan.Zero);
        var today = SpendPeriods.Today(now, London);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.Zero), today.From);
        Assert.Equal(now, today.To);

        // A charge from 23:30 the previous evening is yesterday's, and a 24-hour window would
        // have swept it in.
        Assert.False(today.Holds(new DateTimeOffset(2026, 3, 28, 23, 30, 0, TimeSpan.Zero)));
        Assert.True(today.Holds(new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// The other direction: 25 October 2026, when London falls back at 02:00 and 01:30 happens
    /// twice. The day begins at the midnight the Commander's clock first showed, so the repeated
    /// hour is inside the window rather than outside it.
    /// </summary>
    [Fact]
    public void TodayCoversTheRepeatedHourWhenTheClocksGoBack()
    {
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var today = SpendPeriods.Today(now, London);

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.FromHours(1)), today.From);

        // 01:30 BST — the first pass through the ambiguous hour, 00:30 UTC.
        Assert.True(today.Holds(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero)));

        // 01:30 GMT — the second pass, an hour later in absolute terms. Also today.
        Assert.True(today.Holds(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// A zone whose offset is not a whole number of hours, which is where "subtract the offset"
    /// arithmetic goes wrong. Chatham is +12:45 in winter.
    /// </summary>
    [Fact]
    public void TodayIsRightInAZoneWithAQuarterHourOffset()
    {
        // 08:00 on 17 August 2026 in Chatham is 19:15 UTC on the 16th (+12:45).
        var now = new DateTimeOffset(2026, 8, 16, 19, 15, 0, TimeSpan.Zero);
        var today = SpendPeriods.Today(now, Chatham);

        Assert.Equal(new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.FromMinutes(765)), today.From);
        Assert.True(today.Holds(now));
    }

    /// <summary>
    /// Expected, and written down so it is not "fixed" later. On a Sunday, Today and This week
    /// begin at the same midnight; on the 1st, Today and This month do; on a Sunday the 1st, all
    /// three agree. 1 November 2026 is a Sunday.
    /// </summary>
    [Fact]
    public void OnASundayTheFirstAllThreeCalendarWindowsAgree()
    {
        var now = new DateTimeOffset(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);
        var all = SpendPeriods.All(now, London);

        var today = all.Single(period => period.Name == "Today");
        var week = all.Single(period => period.Name == "This week");
        var month = all.Single(period => period.Name == "This month");

        Assert.Equal(today.From, week.From);
        Assert.Equal(today.From, month.From);
    }
}
