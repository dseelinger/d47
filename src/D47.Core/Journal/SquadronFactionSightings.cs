namespace D47.Core.Journal;

/// <summary>
/// The faction each squadron backs, from the <c>SquadronFaction</c> flag Elite writes on the squadron's entry in
/// <c>Factions[]</c>. The newest sighting per squadron key wins.
/// </summary>
public sealed record SquadronFactionSightings
{
    public static readonly SquadronFactionSightings Empty = new();

    private IReadOnlyDictionary<string, Sighting> Seen { get; init; } = new Dictionary<string, Sighting>(StringComparer.Ordinal);

    public bool IsKnown => Seen.Count > 0;

    /// <summary>The faction last seen flagged for the squadron <paramref name="key"/>, or null.</summary>
    public string? For(string? key) => key is not null && Seen.TryGetValue(key, out var sighting) ? sighting.Faction : null;

    /// <summary>Records the flagged faction of a system event; nothing is recorded outside a squadron.</summary>
    public SquadronFactionSightings Apply(JournalEvent journalEvent, SquadronState squadron)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);
        ArgumentNullException.ThrowIfNull(squadron);

        if (journalEvent.Kind is not ("Location" or "FSDJump" or "CarrierJump") || squadron.Key is not { } key)
        {
            return this;
        }

        var flagged = journalEvent.Items("Factions")
            .Where(faction => faction.Bool("SquadronFaction"))
            .Select(faction => faction.String("Name"))
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        return flagged is null ? this : With(key, new Sighting(flagged, journalEvent.Timestamp), replaceOnTie: true);
    }

    /// <summary>This record plus the sightings <paramref name="earlier"/> holds that are newer or that this has none for.</summary>
    public SquadronFactionSightings With(SquadronFactionSightings earlier)
    {
        ArgumentNullException.ThrowIfNull(earlier);

        var merged = this;

        foreach (var (key, sighting) in earlier.Seen)
        {
            merged = merged.With(key, sighting, replaceOnTie: false);
        }

        return merged;
    }

    private SquadronFactionSightings With(string key, Sighting sighting, bool replaceOnTie)
    {
        if (Seen.TryGetValue(key, out var held)
            && (held.At > sighting.At || (held.At == sighting.At && !replaceOnTie) || held == sighting))
        {
            return this;
        }

        return this with { Seen = new Dictionary<string, Sighting>(Seen, StringComparer.Ordinal) { [key] = sighting } };
    }

    private sealed record Sighting(string Faction, DateTimeOffset At);
}
