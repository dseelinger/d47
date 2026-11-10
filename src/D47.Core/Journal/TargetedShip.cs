namespace D47.Core.Journal;

/// <summary>The ship the Commander has targeted. A Commander's name is never held.</summary>
/// <param name="Ship">The hull, as the journal names it.</param>
/// <param name="Bounty">The bounty in credits, written at scan stage 3 on some wanted ships; null otherwise.</param>
/// <param name="ShieldHealth">Shields in percent at the last scan; null before scan stage 2.</param>
/// <param name="HullHealth">Hull in percent at the last scan; null before scan stage 2.</param>
/// <param name="Subsystem">The localised name of the targeted subsystem, or null.</param>
/// <param name="SubsystemHealth">The targeted subsystem's health in percent, or null.</param>
/// <param name="ScannedAt">The timestamp of the event the figures come from.</param>
/// <param name="PilotName">The NPC pilot's localised name; null before scan stage 1 and always null for a Commander.</param>
public sealed record TargetedShip(
    string? Ship,
    string? PilotName = null,
    string? Rank = null,
    string? Faction = null,
    string? LegalStatus = null,
    bool IsCommander = false,
    long? Bounty = null,
    int? ShieldHealth = null,
    int? HullHealth = null,
    string? Subsystem = null,
    int? SubsystemHealth = null,
    DateTimeOffset? ScannedAt = null)
{
    /// <summary>Whether a pilot name is known and may be spoken.</summary>
    public bool IsNameable => PilotName is { Length: > 0 };

    /// <summary>Reads a <c>ShipTargeted</c> event; null for a dropped target.</summary>
    public static TargetedShip? From(JournalEvent journalEvent)
    {
        if (!journalEvent.Bool("TargetLocked"))
        {
            return null;
        }

        var isCommander = journalEvent.String("PilotName") is { } raw
            && raw.Contains("$cmdr_decorate", StringComparison.OrdinalIgnoreCase);

        return new TargetedShip(
            JournalNames.Ship(journalEvent.Raw, "Ship"),
            isCommander ? null : journalEvent.String("PilotName_Localised"),
            journalEvent.String("PilotRank"),
            journalEvent.String("Faction"),
            journalEvent.String("LegalStatus"),
            isCommander,
            journalEvent.Long("Bounty"),
            Percent(journalEvent.Double("ShieldHealth")),
            Percent(journalEvent.Double("HullHealth")),
            journalEvent.String("Subsystem_Localised") is { Length: > 0 } subsystem ? subsystem : null,
            Percent(journalEvent.Double("SubsystemHealth")),
            journalEvent.Timestamp);
    }

    private static int? Percent(double? value) => value is { } v ? (int)Math.Clamp(Math.Round(v), 0, 100) : null;
}
