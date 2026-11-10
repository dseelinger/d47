namespace D47.Core.Journal;

/// <summary>The ship-launched fighter that is out. <c>CommanderFlies</c> is false when the crew member flies it.</summary>
public sealed record LaunchedFighter(long Id, bool CommanderFlies, DateTimeOffset LaunchedAt)
{
    /// <summary>The fighter after one event: launched, kept, or null once it docks, is destroyed or the ship changes place.</summary>
    public static LaunchedFighter? Apply(LaunchedFighter? held, JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "LaunchFighter" when journalEvent.Long("ID") is { } id
            => new LaunchedFighter(id, journalEvent.Bool("PlayerControlled"), journalEvent.Timestamp),
        "DockFighter" or "FighterDestroyed" => held is not null && journalEvent.Long("ID") == held.Id ? null : held,
        "Died" or "LoadGame" or "Docked" or "SupercruiseEntry" or "FSDJump" or "CarrierJump" => null,
        _ => held,
    };
}
