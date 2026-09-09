using System.Globalization;

namespace D47.Core.Utilities;

/// <summary>Whether this is a stretch of time or a moment (Phase 24, "Timers and alarms").</summary>
public enum ReminderKind
{
    /// <summary>A countdown. "Forty minutes for the mining run."</summary>
    Timer,

    /// <summary>A wall-clock moment. "Seven in the morning."</summary>
    Alarm,
}

/// <summary>One thing d47 has been asked to say something about later (Phase 24).</summary>
/// <param name="Id">Stable, unique, and never shown.</param>
/// <param name="Name">What the Commander called it.</param>
/// <param name="Due">When it goes off, as an instant.</param>
/// <param name="Kind">A countdown or a moment.</param>
public sealed record Reminder(string Id, string Name, DateTimeOffset Due, ReminderKind Kind)
{
    /// <summary>Whether this is due at or before an instant.</summary>
    public bool DueBy(DateTimeOffset now) => Due <= now;

    /// <summary>How long is left, or <see cref="TimeSpan.Zero"/> when it is already due.</summary>
    public TimeSpan Remaining(DateTimeOffset now) =>
        Due > now ? Due - now : TimeSpan.Zero;

    /// <summary>
    /// What the panel shows beside the name — a countdown for a timer, a clock time for an alarm.
    /// </summary>
    public string Describe(DateTimeOffset now, TimeZoneInfo zone) => Kind == ReminderKind.Timer
        ? Left(Remaining(now))
        : GalacticTime.Local(Due, zone).ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>What d47 says when it goes off.</summary>
    public string Announce() => Kind == ReminderKind.Timer
        ? $"{Name}: that is your timer."
        : $"{Name}: that is your alarm.";

    /// <summary>What d47 says about one that was due while it was closed.</summary>
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

        // Rounded up, because a timer showing "0 minutes" for the last fifty-nine seconds of its life reads
        // as one that has stalled.
        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);

        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        return $"{minutes / 60}h {minutes % 60:00}m";
    }
}
