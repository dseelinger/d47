using System.Globalization;
using System.Text.RegularExpressions;

namespace D47.Core.Logbook;

/// <summary>The days a Commander has journals for, and the typed form of a day.</summary>
public static partial class LogDays
{
    public const string Form = "yyyy-MM-dd";

    public const string NotADay = "That is not a date I can read. Write it as year, month, day, like 2026-09-14.";

    [GeneratedRegex(@"^Journal\.(?:(?<new>\d{4}-\d{2}-\d{2})T\d{6}|(?<old>\d{2})(?<m>\d{2})(?<d>\d{2})\d{6})\.\d+\.log$")]
    private static partial Regex FileName();

    /// <summary>The distinct days the journal file names start a session on, newest first. Names that fit neither form are skipped.</summary>
    public static IReadOnlyList<DateOnly> FromFileNames(IEnumerable<string> paths)
    {
        var days = new HashSet<DateOnly>();

        foreach (var path in paths)
        {
            var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
            var match = FileName().Match(name);

            if (!match.Success)
            {
                continue;
            }

            var day = match.Groups["new"].Success
                ? DateOnly.TryParseExact(match.Groups["new"].Value, Form, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed
                    : (DateOnly?)null
                : TryOld(match);

            if (day is { } found)
            {
                days.Add(found);
            }
        }

        return [.. days.OrderByDescending(d => d)];
    }

    /// <summary>A typed <c>yyyy-MM-dd</c>, or null.</summary>
    public static DateOnly? Parse(string? typed) =>
        DateOnly.TryParseExact(typed?.Trim(), Form, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;

    public static string Write(DateOnly day) => day.ToString(Form, CultureInfo.InvariantCulture);

    public static string Weekday(DateOnly day) => day.ToString("dddd", CultureInfo.InvariantCulture);

    /// <summary>Midnight at the start of the day, in the zone.</summary>
    public static DateTimeOffset Start(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <summary>One second before the next midnight, in the zone.</summary>
    public static DateTimeOffset End(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue).AddDays(1).AddSeconds(-1);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static DateOnly? TryOld(Match match)
    {
        var year = 2000 + int.Parse(match.Groups["old"].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);

        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }
}
