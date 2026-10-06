using System.Globalization;

namespace D47.Core.Activities;

/// <summary>How long ago an activity was last done, in the words the Activities page shows.</summary>
public static class ActivityAge
{
    /// <summary>"today", "yesterday", "13 days ago", "7 weeks ago", "5 months ago" or "1 year 8 months ago".</summary>
    public static string Say(DateTimeOffset at, DateTimeOffset now)
    {
        var days = (int)Math.Floor((now.UtcDateTime.Date - at.UtcDateTime.Date).TotalDays);

        if (days <= 0)
        {
            return "today";
        }

        if (days == 1)
        {
            return "yesterday";
        }

        if (days < 14)
        {
            return Ago(days, "day");
        }

        if (days < 60)
        {
            return Ago(days / 7, "week");
        }

        var then = at.UtcDateTime;
        var current = now.UtcDateTime;
        var months = ((current.Year - then.Year) * 12) + current.Month - then.Month - (current.Day < then.Day ? 1 : 0);

        if (months < 12)
        {
            return Ago(months, "month");
        }

        var years = months / 12;
        var rest = months % 12;

        return rest == 0
            ? Ago(years, "year")
            : $"{Count(years, "year")} {Count(rest, "month")} ago";
    }

    private static string Ago(int count, string unit) => $"{Count(count, unit)} ago";

    private static string Count(int count, string unit) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {unit}{(count == 1 ? string.Empty : "s")}";
}
