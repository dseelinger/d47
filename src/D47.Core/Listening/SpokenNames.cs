using D47.Core.Journal;

namespace D47.Core.Listening;

/// <summary>
/// Every name this Commander has actually met — systems, stations and minor factions — as a catalogue
/// to match a misheard one against (#134).
/// </summary>
public sealed record SpokenNames
{
    /// <summary>The most names one Commander's catalogue may hold.</summary>
    public const int Limit = 40_000;

    public static readonly SpokenNames Empty = new();

    /// <summary>Every name, in the order they were first met.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    public bool IsKnown => Names.Count > 0;

    /// <summary>Whether this Commander has met this name.</summary>
    public bool Knows(string name) =>
        Knowledge.Catalogue.Match(Names, name ?? string.Empty) is not null;

    /// <summary>Names close enough to what was said to be worth offering back, sound-alikes included.</summary>
    public IReadOnlyList<string> Near(string spoken) =>
        Knowledge.Catalogue.NearSpoken(Names, spoken ?? string.Empty);

    /// <summary>Files whatever names this event carried.</summary>
    public SpokenNames Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var found = new List<string?>
        {
            // Where they are, where they are going, where they have been.
            journalEvent.String("StarSystem"),
            journalEvent.String("SystemName"),
            journalEvent.String("StationName"),

            // Who holds it.
            journalEvent.Object("SystemFaction")?.String("Name"),
            journalEvent.Object("StationFaction")?.String("Name"),
            journalEvent.String("Faction"),
        };

        // The full presence list on an FSDJump or Location, which is where most of the 9,422 come from — a
        // system the Commander flew through carries every faction standing in it.
        found.AddRange(journalEvent.Items("Factions").Select(faction => faction.Named("Name")));

        return With(found);
    }

    /// <summary>Adds names, keeping the ones already held and their order.</summary>
    public SpokenNames With(IEnumerable<string?> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        List<string>? grown = null;
        HashSet<string>? seen = null;

        foreach (var name in names)
        {
            if (!IsWorthKeeping(name))
            {
                continue;
            }

            seen ??= new HashSet<string>(Names, StringComparer.OrdinalIgnoreCase);

            if (!seen.Add(name!.Trim()))
            {
                continue;
            }

            grown ??= [.. Names];
            grown.Add(name.Trim());
        }

        if (grown is null)
        {
            return this;
        }

        // Oldest first out, which is what keeping the order buys: a Commander who has met forty thousand
        // names is likelier to say one of the recent ones.
        return this with
        {
            Names = grown.Count <= Limit ? grown : [.. grown.Skip(grown.Count - Limit)],
        };
    }

    /// <summary>
    /// "unknown" is not a name, and it is what several journal fields carry when Elite has none — so
    /// without this the catalogue would offer it back as a near miss.
    /// </summary>
    private static bool IsWorthKeeping(string? name) =>
        name is { Length: > 1 }
        && name.Trim() is { Length: > 1 } trimmed
        && !string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase);
}
