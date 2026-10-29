using System.Globalization;

namespace D47.App.Panel;

/// <summary>The query, minute and clock helpers the ledger pages and their models share.</summary>
internal static class PageKit
{
    /// <summary>Sets <paramref name="current"/> to the trimmed <paramref name="query"/>; false when it is unchanged.</summary>
    public static bool Retarget(string? query, ref string current)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed == current)
        {
            return false;
        }

        current = trimmed;

        return true;
    }

    public static long Minute(DateTimeOffset now) => now.ToUnixTimeSeconds() / 60;

    /// <summary>The UTC time as HH:mm.</summary>
    public static string Clock(DateTimeOffset at) => at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
}
