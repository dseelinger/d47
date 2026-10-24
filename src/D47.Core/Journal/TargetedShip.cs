namespace D47.Core.Journal;

/// <summary>The ship the Commander has targeted. A Commander's name is never held.</summary>
/// <param name="Ship">The hull, as the journal names it.</param>
/// <param name="PilotName">The NPC pilot's localised name; null before scan stage 1 and always null for a Commander.</param>
public sealed record TargetedShip(
    string? Ship,
    string? PilotName = null,
    string? Rank = null,
    string? Faction = null,
    string? LegalStatus = null,
    bool IsCommander = false)
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
            journalEvent.String("Ship_Localised") ?? journalEvent.String("Ship"),
            isCommander ? null : journalEvent.String("PilotName_Localised"),
            journalEvent.String("PilotRank"),
            journalEvent.String("Faction"),
            journalEvent.String("LegalStatus"),
            isCommander);
    }
}
