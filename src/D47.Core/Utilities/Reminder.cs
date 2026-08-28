using System.Globalization;

namespace D47.Core.Utilities;

/// <summary>
/// Whether this is a stretch of time or a moment (Phase 24, "Timers and alarms").
/// </summary>
public enum ReminderKind
{
    /// <summary>
    /// A countdown. "Forty minutes for the mining run."
    /// <para>
    /// <b>Timers do not survive a restart</b>, and that is deliberate rather than an omission: a
    /// forty-minute timer through a crash is a question nobody can answer — half elapsed, or
    /// start again? Neither answer is right, so it is not asked.
    /// </para>
    /// </summary>
    Timer,

    /// <summary>
    /// A wall-clock moment. "Seven in the morning."
    /// <para>
    /// <b>Alarms do survive a restart</b>, because an alarm for 07:00 is a promise about a moment
    /// that outlives the process. And <b>one that could not sound says so afterwards</b>: d47
    /// cannot fire while it is closed, so a missed alarm is reported when it next starts, with
    /// when it was due, rather than failing silently or sounding hours late as though nothing had
    /// happened.
    /// </para>
    /// </summary>
    Alarm,
}

/// <summary>
/// One thing d47 has been asked to say something about later (Phase 24).
/// <para>
/// <b>The first thing d47 does that nothing external triggers.</b> Every other behaviour is
/// reactive — a journal event arrives, a callout goes out — or the Commander started it in that
/// moment. A timer fires because time passed and nothing happened at all.
/// </para>
/// </summary>
/// <param name="Id">Stable, unique, and never shown. What a cancel names in code.</param>
/// <param name="Name">
/// What the Commander called it. Shown, said back when it fires, and matchable when they ask for
/// it to be cancelled — which is why the cue does not need to be per-timer: <b>the cue says
/// something finished and d47 speaks the name</b>.
/// </param>
/// <param name="Due">When it goes off, as an instant. UTC, like everything else recorded here.</param>
/// <param name="Kind">A countdown or a moment. See <see cref="ReminderKind"/>.</param>
public sealed record Reminder(string Id, string Name, DateTimeOffset Due, ReminderKind Kind)
{
    /// <summary>Whether this is due at or before an instant.</summary>
    public bool DueBy(DateTimeOffset now) => Due <= now;

    /// <summary>
    /// How long is left, or <see cref="TimeSpan.Zero"/> when it is already due. Never negative:
    /// a panel counting up from zero says a timer is running when it has finished.
    /// </summary>
    public TimeSpan Remaining(DateTimeOffset now) =>
        Due > now ? Due - now : TimeSpan.Zero;

    /// <summary>
    /// What the panel shows beside the name — a countdown for a timer, a clock time for an alarm.
    /// <para>
    /// Different by kind because they answer different questions. "In 12 minutes" is what a
    /// Commander wants of a mining timer and "07:00" is what they want of an alarm; showing an
    /// alarm as "in 9 hours 14 minutes" is arithmetic they then have to undo.
    /// </para>
    /// </summary>
    public string Describe(DateTimeOffset now, TimeZoneInfo zone) => Kind == ReminderKind.Timer
        ? Left(Remaining(now))
        : GalacticTime.Local(Due, zone).ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>What d47 says when it goes off. The name, because the cue cannot carry it.</summary>
    public string Announce() => Kind == ReminderKind.Timer
        ? $"{Name}: that is your timer."
        : $"{Name}: that is your alarm.";

    /// <summary>
    /// What d47 says about one that was due while it was closed.
    /// <para>
    /// <b>Reported afterwards, never faked.</b> Sounding an alarm hours late as though nothing
    /// had happened is worse than not sounding it: the Commander acts on a chime, and a chime that
    /// means "nine hours ago" has to say so.
    /// </para>
    /// </summary>
    public string AnnounceMissed(TimeZoneInfo zone) =>
        $"{Name}: that alarm was due at "
        + $"{GalacticTime.Local(Due, zone).ToString("HH:mm", CultureInfo.CurrentCulture)} and I was "
        + "not running. I have not sounded it since.";

    private static string Left(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "due";
        }

        // Rounded up, because a timer showing "0 minutes" for the last fifty-nine seconds of its
        // life reads as one that has stalled.
        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);

        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        return $"{minutes / 60}h {minutes % 60:00}m";
    }
}
