using System.Globalization;

namespace D47.Core.Watches;

/// <summary>How a watch's last firing is worded.</summary>
public static class WatchFired
{
    public const string NotYet = "Not fired yet";

    /// <summary>"3 Nov · tick 14:07" in <paramref name="zone"/>, or <see cref="NotYet"/> when the watch has not fired.</summary>
    public static string Text(string? firedForTick, TimeZoneInfo zone)
    {
        if (!DateTimeOffset.TryParse(
                firedForTick,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var tick))
        {
            return NotYet;
        }

        var local = TimeZoneInfo.ConvertTime(tick, zone);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{local.Day} {local.ToString("MMM", CultureInfo.InvariantCulture)} · tick {local:HH:mm}");
    }
}
