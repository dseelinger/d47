namespace D47.Core.Callouts;

/// <summary>
/// Whether one line with a <see cref="FlavourBriefs"/> brief is put to the model, or spoken as
/// written without asking it (#214).
/// </summary>
public sealed class RewordChance(Random? choice = null)
{
    private readonly Random _choice = choice ?? Random.Shared;

    /// <summary>
    /// True if this announcement should be reworded. An ambient remark and a canned line from the
    /// Commander's own carrier always are; every other line is decided independently against
    /// <paramref name="rewordPercent"/> (0-100, clamped).
    /// </summary>
    public bool ShouldReword(Announcement announcement, int rewordPercent)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        return announcement.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal)
            || string.Equals(announcement.Key, IncomingMessages.CarrierCannedKey, StringComparison.Ordinal)
            || _choice.Next(100) < Math.Clamp(rewordPercent, 0, 100);
    }
}
