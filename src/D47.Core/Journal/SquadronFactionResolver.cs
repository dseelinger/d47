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
}
