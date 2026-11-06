namespace D47.Core.Journal;

/// <summary>Which faction the current squadron backs.</summary>
public static class SquadronFactionResolver
{
    /// <summary>
    /// The newest sighting for the squadron, else the name the Commander stored, else null. Null outside a squadron.
    /// </summary>
    public static string? Resolve(SquadronFactionSightings sightings, SquadronState squadron, string? stored)
    {
        ArgumentNullException.ThrowIfNull(sightings);
        ArgumentNullException.ThrowIfNull(squadron);

        return !squadron.IsMember
            ? null
            : sightings.For(squadron.Key) ?? (string.IsNullOrWhiteSpace(stored) ? null : stored);
    }

    /// <summary>The squadron faction for the Commander in <paramref name="state"/>, reading the stored name from <paramref name="store"/>.</summary>
    public static string? Resolve(CommanderGameState? state, Knowledge.SquadronFactions? store) =>
        state is null
            ? null
            : Resolve(state.SquadronFactions, state.Squadron, store?.For(state.Identity.FrontierId, state.Squadron));
}
