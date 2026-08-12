namespace D47.Core.Journal;

/// <summary>
/// Where a Commander currently is. Deliberately minimal for Phase 2 — system, body, and
/// docked/station — because a fuller picture (loadout, fleet, carrier) is Phase 7's job, not
/// this one's.
/// </summary>
public sealed record JournalLocation(string? StarSystem, string? Body, bool Docked, string? StationName)
{
    public static readonly JournalLocation Unknown = new(null, null, false, null);

    /// <summary>
    /// Folds one event into the current location. An event this type does not recognise
    /// leaves the location unchanged rather than clearing it — an unrelated event is not
    /// evidence that the Commander's position is now unknown.
    /// </summary>
    public JournalLocation Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "Location" => this with
        {
            StarSystem = journalEvent.TryGetString("StarSystem", out var system) ? system : StarSystem,
            Body = journalEvent.TryGetString("Body", out var body) ? body : Body,
            Docked = journalEvent.TryGetBoolean("Docked", out var docked) && docked,
        },
        "FSDJump" => this with
        {
            StarSystem = journalEvent.TryGetString("StarSystem", out var system) ? system : StarSystem,
            Body = journalEvent.TryGetString("Body", out var body) ? body : Body,
            Docked = false,
            StationName = null,
        },
        "Docked" => this with
        {
            Docked = true,
            StationName = journalEvent.TryGetString("StationName", out var station) ? station : StationName,
            StarSystem = journalEvent.TryGetString("StarSystem", out var dockedSystem) ? dockedSystem : StarSystem,
        },
        "Undocked" => this with { Docked = false, StationName = null },
        _ => this,
    };
}
