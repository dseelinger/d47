using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Authored lines for Frontier's canned traffic from the Commander's own carrier, addressed to its owner
/// (#290). Each keeps every fact in Frontier's line and adds none.
/// </summary>
public static class CarrierOwnerLines
{
    /// <summary>Matched as a prefix: the suffix is the standing, and every standing gets the same owner's line.</summary>
    private const string DockingChatter = "$DockingChatter_";

    private static readonly Dictionary<string, Func<string, string>> Lines =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["$STATION_docking_granted"] = owner => $"Docking granted, {owner}.",
            ["$STATION_NoFireZone_entered"] = owner => $"No fire zone entered, {owner}.",
            ["$STATION_NoFireZone_exited"] = owner => $"No fire zone exited, {owner}.",
        };

    /// <summary>The authored line for a raw <c>$</c>-key, or null when there is none.</summary>
    public static string? For(string? messageKey, string? commanderName)
    {
        if (string.IsNullOrEmpty(messageKey))
        {
            return null;
        }

        var end = messageKey.IndexOf(';', StringComparison.Ordinal);
        var key = end < 0 ? messageKey : messageKey[..end];
        var owner = CommanderAddress.Said(commanderName);

        if (key.StartsWith(DockingChatter, StringComparison.OrdinalIgnoreCase))
        {
            return $"Welcome back, {owner}.";
        }

        return Lines.TryGetValue(key, out var line) ? line(owner) : null;
    }

    /// <summary>
    /// The announcement to speak when no rewrite is used: a carrier canned line with an authored line
    /// takes it, and anything else is returned unchanged.
    /// </summary>
    public static Announcement AsWritten(Announcement announcement, string? commanderName)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        return string.Equals(announcement.Key, IncomingMessages.CarrierCannedKey, StringComparison.Ordinal)
            && For(announcement.MessageKey, commanderName) is { } authored
            ? announcement with { Text = authored }
            : announcement;
    }
}
