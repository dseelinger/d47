using System.Globalization;

namespace D47.Core.Journal;

/// <summary>The time left on a mission, as the Missions pane prints it.</summary>
public static class MissionCountdown
{
    /// <summary><c>3h 12m</c> under a day, <c>1d 4h</c> from a day, <c>EXPIRED</c> at or past the expiry.</summary>
    public static string Format(DateTimeOffset expiry, DateTimeOffset now)
    {
        var left = expiry - now;

        if (left <= TimeSpan.Zero)
        {
            return "EXPIRED";
        }

        return left.TotalDays >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{left.Days}d {left.Hours}h")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)left.TotalHours}h {left.Minutes}m");
    }
}
