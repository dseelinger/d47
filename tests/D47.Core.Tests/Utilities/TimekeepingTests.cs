using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Utilities;

/// <summary>
/// Two clocks over one instant, and the timers and alarms beside them (Phase 24).
/// </summary>
public class TimekeepingTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 8, 17, 21, 4, 0, TimeSpan.Zero);

    private static AlarmStore Store(TempInstall install) =>
        new(Path.Combine(install.Root, "alarms.json"), NullLogger<AlarmStore>.Instance);

    /// <summary>
    /// Elite runs 1286 years ahead, so 2026 is 3312. Arithmetic over the same instant rather than
    /// a second clock, which is what makes the two unable to disagree.
    /// </summary>
    [Fact]
    public void TheGalaxyIsTwelveHundredAndEightySixYearsAhead()
    {
        var clocks = GalacticTime.Read(Instant, TimeZoneInfo.Utc);

        Assert.Equal(2026, clocks.Local.Year);
        Assert.Equal(3312, clocks.Galactic.Year);

        // The same day and the same minute. Only the year moves.
        Assert.Equal(clocks.Local.Month, clocks.Galactic.Month);
        Assert.Equal(clocks.Local.Day, clocks.Galactic.Day);
        Assert.Equal(clocks.Local.TimeOfDay, clocks.Galactic.TimeOfDay);
    }

    /// <summary>
    /// The galactic date is written the same way for everybody: the galaxy's calendar is not a
    /// regional format, and 08/17 versus 17/08 reads as two different days.
    /// </summary>
    [Fact]
    public void TheGalacticDateIsWrittenTheSameWayEverywhere()
    {
        var clocks = GalacticTime.Read(Instant, TimeZoneInfo.Utc);

        Assert.Equal("17 August 3312", clocks.GalacticDate);
        Assert.Equal("21:04", clocks.GalacticTimeOfDay);
    }

    /// <summary>
    /// The zone is applied at the point of asking rather than baked into the instant — an instant
    /// recorded in local wall-clock is wrong twice a year and cannot be repaired afterwards.
    /// </summary>
    [Fact]
    public void TheZoneIsAppliedWhenItIsAsked()
    {
        var ahead = TimeZoneInfo.CreateCustomTimeZone("t+3", TimeSpan.FromHours(3), "t+3", "t+3");

        var utc = GalacticTime.Read(Instant, TimeZoneInfo.Utc);
        var local = GalacticTime.Read(Instant, ahead);

        Assert.Equal("21:04", utc.LocalTimeOfDay);
        Assert.Equal("00:04", local.LocalTimeOfDay);

        // The galactic side does not follow it. Elite runs galactic time on UTC, so the same
        // instant reads the same out there whoever is looking at it — which is what makes it
        // agree with the station clock the Commander is docked at (remediation.md 11, item 8).
        Assert.Equal("21:04", utc.GalacticTimeOfDay);
        Assert.Equal("21:04", local.GalacticTimeOfDay);
        Assert.Equal("17 August 3312", local.GalacticDate);
    }

    /// <summary>
    /// Both dates are supplied already worked out. A model doing arithmetic in prose is wrong
    /// occasionally and confidently.
    /// </summary>
    [Fact]
    public void TheSpokenAnswerCarriesBothDatesWorkedOut()
    {
        var said = GalacticTime.Read(Instant, TimeZoneInfo.Utc).Say();

        Assert.Contains("3312", said, StringComparison.Ordinal);
        Assert.Contains("2026", said, StringComparison.Ordinal);
    }

    [Fact]
    public void ATimerFiresOnceWhenItIsDue()
    {
        using var install = new TempInstall();
        var timekeeper = new Timekeeper(Store(install));

        var timer = timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);

        Assert.NotNull(timer);
        Assert.Empty(timekeeper.Poll(Instant.AddMinutes(39)));

        var fired = timekeeper.Poll(Instant.AddMinutes(41));

        Assert.Single(fired);
        Assert.Equal("mining run", fired[0].Reminder.Name);
        Assert.False(fired[0].Missed);

        // Once. A timer that fires on every tick after it is due is an alarm nobody can silence.
        Assert.Empty(timekeeper.Poll(Instant.AddMinutes(42)));
        Assert.Empty(timekeeper.Running);
    }

    /// <summary>
    /// Timers live only here, so a new Timekeeper over the same file has none of them. That is
    /// the asymmetry stated as behaviour rather than as a comment.
    /// </summary>
    [Fact]
    public void TimersDoNotSurviveARestartAndAlarmsDo()
    {
        using var install = new TempInstall();
        var alarms = Store(install);

        var before = new Timekeeper(alarms);

        before.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);
        before.SetAlarm("wake up", Instant.AddHours(9), Instant);

        Assert.Equal(2, before.Running.Count);

        var reopened = Store(install);
        reopened.Poll();

        var after = new Timekeeper(reopened);

        Assert.Single(after.Running);
        Assert.Equal("wake up", after.Running[0].Name);
    }

    /// <summary>
    /// An alarm that came round while d47 was closed is reported afterwards rather than sounded
    /// late as though nothing had happened.
    /// </summary>
    [Fact]
    public void AnAlarmThatCameRoundWhileD47WasClosedIsReportedAsMissed()
    {
        using var install = new TempInstall();
        var alarms = Store(install);

        new Timekeeper(alarms).SetAlarm("wake up", Instant.AddHours(9), Instant);

        var reopened = Store(install);
        reopened.Poll();

        // The next launch, a day later.
        var fired = new Timekeeper(reopened).Poll(Instant.AddDays(1));

        Assert.Single(fired);
        Assert.True(fired[0].Missed);
        Assert.Contains("was due at", fired[0].Reminder.AnnounceMissed(TimeZoneInfo.Utc), StringComparison.Ordinal);
        Assert.Contains("not running", fired[0].Reminder.AnnounceMissed(TimeZoneInfo.Utc), StringComparison.Ordinal);
    }

    /// <summary>And one that comes round with d47 running is not missed.</summary>
    [Fact]
    public void AnAlarmThatComesRoundWhileD47IsRunningIsNotMissed()
    {
        using var install = new TempInstall();
        var timekeeper = new Timekeeper(Store(install));

        timekeeper.SetAlarm("wake up", Instant.AddHours(9), Instant);

        // The first poll is before it is due, so the process was there for it.
        Assert.Empty(timekeeper.Poll(Instant));

        var fired = timekeeper.Poll(Instant.AddHours(10));

        Assert.Single(fired);
        Assert.False(fired[0].Missed);
    }

    [Fact]
    public void CancellingTakesItOffTheList()
    {
        using var install = new TempInstall();
        var timekeeper = new Timekeeper(Store(install));

        var timer = timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant)!;
        var alarm = timekeeper.SetAlarm("wake up", Instant.AddHours(9), Instant)!;

        Assert.True(timekeeper.Cancel(timer.Id));
        Assert.True(timekeeper.Cancel(alarm.Id));
        Assert.False(timekeeper.Cancel(timer.Id));

        Assert.Empty(timekeeper.Running);
        Assert.Empty(timekeeper.Poll(Instant.AddDays(1)));
    }

    /// <summary>
    /// Nought and several answer the same way rather than the second guessing: cancelling the
    /// wrong alarm of two is worse than being asked which.
    /// </summary>
    [Fact]
    public void NoughtAndSeveralAreBothAnsweredRatherThanGuessedAt()
    {
        using var install = new TempInstall();
        var timekeeper = new Timekeeper(Store(install));

        timekeeper.StartTimer("run", TimeSpan.FromMinutes(10), Instant);
        timekeeper.StartTimer("run", TimeSpan.FromMinutes(20), Instant);

        Assert.Null(timekeeper.Find("run"));
        Assert.Null(timekeeper.Find("nothing by that name"));
    }

    /// <summary>A timer for no time at all, or for a moment already gone, is not a reminder.</summary>
    [Fact]
    public void NonsenseIsRefused()
    {
        using var install = new TempInstall();
        var timekeeper = new Timekeeper(Store(install));

        Assert.Null(timekeeper.StartTimer("nothing", TimeSpan.Zero, Instant));
        Assert.Null(timekeeper.StartTimer("nothing", TimeSpan.FromMinutes(-5), Instant));
        Assert.Null(timekeeper.StartTimer("   ", TimeSpan.FromMinutes(5), Instant));
        Assert.Null(timekeeper.SetAlarm("gone", Instant.AddMinutes(-1), Instant));
    }

    /// <summary>
    /// A countdown shows how long is left and an alarm shows a clock time, because they answer
    /// different questions: "in 12 minutes" for a mining timer, "07:00" for an alarm.
    /// </summary>
    [Fact]
    public void EachKindIsDescribedByTheQuestionItAnswers()
    {
        var timer = new Reminder("a", "mining run", Instant.AddMinutes(12), ReminderKind.Timer);
        var alarm = new Reminder("b", "wake up", Instant.AddHours(9), ReminderKind.Alarm);

        Assert.Equal("12 min", timer.Describe(Instant, TimeZoneInfo.Utc));
        Assert.Equal("06:04", alarm.Describe(Instant, TimeZoneInfo.Utc));

        // And it never counts up past zero: a timer showing a negative is one that says it is
        // running after it has finished.
        Assert.Equal(TimeSpan.Zero, timer.Remaining(Instant.AddHours(1)));
        Assert.Equal("due", timer.Describe(Instant.AddHours(1), TimeZoneInfo.Utc));
    }

    /// <summary>The name is what d47 says, because the one shipped cue cannot carry it.</summary>
    [Fact]
    public void WhatIsSaidCarriesTheName()
    {
        var timer = new Reminder("a", "mining run", Instant, ReminderKind.Timer);

        Assert.Contains("mining run", timer.Announce(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The file is hand-editable, so a line it gets wrong is reported rather than silently
    /// dropped — and the rest still loads.
    /// </summary>
    [Fact]
    public void ABadLineIsReportedAndTheRestStillLoads()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "alarms.json");

        File.WriteAllText(path, """
        {
          "alarms": [
            { "id": "one", "name": "", "due": "2026-08-18T06:00:00+00:00" },
            { "id": "two", "name": "wake up", "due": "2026-08-18T07:00:00+00:00" }
          ]
        }
        """);

        var store = new AlarmStore(path, NullLogger<AlarmStore>.Instance);
        store.Poll();

        Assert.Single(store.Alarms);
        Assert.Equal("wake up", store.Alarms[0].Name);
        Assert.Single(store.Problems);
    }

    /// <summary>
    /// Change is detected by content rather than by a last-write time. Phase 21's correction:
    /// two writes inside one 15.6 ms tick of the file-system clock carry the same stamp.
    /// </summary>
    [Fact]
    public void TwoWritesInsideOneTickAreBothSeen()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "alarms.json");
        var store = new AlarmStore(path, NullLogger<AlarmStore>.Instance);

        const string One = """{ "alarms": [ { "id": "a", "name": "one", "due": "2026-08-18T06:00:00+00:00" } ] }""";
        const string Two = """{ "alarms": [ { "id": "b", "name": "two", "due": "2026-08-18T07:00:00+00:00" } ] }""";

        File.WriteAllText(path, One);
        Assert.True(store.Poll());
        Assert.Equal("one", store.Alarms[0].Name);

        // Immediately, with no delay at all — which is the whole point.
        File.WriteAllText(path, Two);
        Assert.True(store.Poll());
        Assert.Equal("two", store.Alarms[0].Name);

        // And an unchanged file is not a change.
        Assert.False(store.Poll());
    }
}
