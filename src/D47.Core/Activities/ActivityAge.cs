using System.Globalization;
using D47.Core.Audio;

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

    /// <summary>The same gap with the numbers in words: "a year and eight months ago", "four weeks ago".</summary>
    public static string SayInWords(DateTimeOffset at, DateTimeOffset now)
    {
        var figures = Say(at, now);

        var parts = figures.Split(' ');

        if (parts.Length == 5)
        {
            var years = parts[0] == "1" ? "a year" : $"{Words(parts[0])} years";

            return $"{years} and {Words(parts[2])} {parts[3]} ago";
        }

        if (parts.Length == 3 && parts[0] == "1" && parts[1] == "year")
        {
            return "a year ago";
        }

        return parts.Length == 3 && parts[2] == "ago"
            ? $"{Words(parts[0])} {parts[1]} ago"
            : figures;
    }

    private static string Words(string digits) => SpokenNumbers.Expand(digits);

    private static string Ago(int count, string unit) => $"{Count(count, unit)} ago";

    private static string Count(int count, string unit) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {unit}{(count == 1 ? string.Empty : "s")}";
}
