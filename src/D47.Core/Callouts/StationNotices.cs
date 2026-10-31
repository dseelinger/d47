namespace D47.Core.Callouts;

/// <summary>
/// d47's own lines for Frontier's no-fire zone notices. They are never sent to a language model.
/// Each line holds an <c>{0}</c> slot for the form of address.
/// </summary>
public static class StationNotices
{
    public const string Entered = "$STATION_NoFireZone_entered";

    public const string Exited = "$STATION_NoFireZone_exited";

    /// <summary>How a station addresses the Commander.</summary>
    public const string StationAddress = "pilot";

    public static readonly IReadOnlyList<string> EnteredLines =
    [
        "You are now inside the no-fire zone, {0}. Hold your fire.",
        "You have entered the no-fire zone, {0}. Weapons are to be held.",
        "Inside the no-fire zone now, {0}. Hold your weapons.",
        "You've crossed into the no-fire zone, {0}. Hold all weapons.",
        "Welcome inside the no-fire zone, {0}. No shooting from here on.",
        "No-fire zone entered, {0}. Hold your fire until you leave it.",
        "{0}, that's the no-fire zone and you are inside it. Weapons held.",
        "You are inside the no-fire zone, {0}. Hold your guns holstered.",
    ];

    public static readonly IReadOnlyList<string> ExitedLines =
    [
        "You have left the no-fire zone, {0}.",
        "You're out of the no-fire zone, {0}. Weapons are yours again.",
        "No-fire zone cleared, {0}. You have left it behind.",
        "You've left the no-fire zone, {0}.",
        "That's you out of the no-fire zone, {0}.",
        "You have cleared the no-fire zone, {0}. Fly safe.",
        "No-fire zone left behind, {0}. You are out of it.",
        "{0}, you are out of the no-fire zone now.",
    ];

    /// <summary>The key without its trailing semicolon, or null when it is not a no-fire zone notice.</summary>
    public static string? Normalise(string? messageKey)
    {
        if (string.IsNullOrEmpty(messageKey))
        {
            return null;
        }

        var key = messageKey.TrimEnd(';');

        if (string.Equals(key, Entered, StringComparison.OrdinalIgnoreCase))
        {
            return Entered;
        }

        return string.Equals(key, Exited, StringComparison.OrdinalIgnoreCase) ? Exited : null;
    }

    public static bool IsNotice(string? messageKey) => Normalise(messageKey) is not null;

    /// <summary>
    /// The index of the line to speak: taken from the event's timestamp, and moved on by one
    /// when it would repeat <paramref name="previous"/>.
    /// </summary>
    public static int Pick(DateTimeOffset at, int count, int? previous)
    {
        var index = (int)(at.UtcTicks / TimeSpan.TicksPerSecond % count);

        return previous == index ? (index + 1) % count : index;
    }

    public static IReadOnlyList<string> PoolFor(string normalisedKey) =>
        normalisedKey == Entered ? EnteredLines : ExitedLines;
}
