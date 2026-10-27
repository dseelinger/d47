namespace D47.Core.Callouts;

/// <summary>Says once, on docking, that the station is under attack, damaged, under repairs or abandoned.</summary>
public sealed class StationDamageCallout : ICallout
{
    public string Id => "station-damage";

    public const string Key = "station.damage";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "Docked"
                && journalEvent.Named("StationName") is { Length: > 0 } station
                && Condition(journalEvent.String("StationState")) is { } condition)
            {
                yield return new Announcement(Key, $"{station} is {condition}.");
            }
        }
    }

    /// <summary>The spoken condition for a <c>StationState</c>, or null for one that is not announced.</summary>
    public static string? Condition(string? stationState) => stationState switch
    {
        "UnderAttack" => "under attack",
        "Damaged" or "DamagedHuman" => "damaged",
        "UnderRepairs" => "under repairs",
        "Abandoned" => "abandoned",
        _ => null,
    };
}
