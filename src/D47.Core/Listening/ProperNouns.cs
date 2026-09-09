using D47.Core.Journal;

namespace D47.Core.Listening;

/// <summary>
/// Names drawn from the journal to bias transcription with (Phase 6, "Bias transcription with proper
/// nouns from the journal").
/// </summary>
public static class ProperNouns
{
    /// <summary>
    /// How much of the list is kept for names d47 ships rather than names the journal wrote
    /// (remediation.md 10, item 17).
    /// </summary>
    public const int ShippedShare = 20;

    /// <summary>A cap on how many names are offered.</summary>
    public const int Limit = 60;

    /// <summary>The names worth biasing towards, most relevant first.</summary>
    public static IReadOnlyList<string> From(CommanderGameState? state, NavRoute? route = null)
    {
        if (state is null)
        {
            return [];
        }

        var names = new List<string?>
        {
            // Where they are, first.
            state.Location.StarSystem,
            state.Location.StationName,
            state.Location.Body,

            // Where they are going.
            state.Location.NextJumpSystem,

            // What they are flying, by name and by type.
            state.Ship.Name,
            state.Ship.TypeName ?? state.Ship.Type,

            // Their carrier, which they will refer to by name rather than by callsign.
            state.Carrier.Name,
            state.Carrier.CallSign,
            state.Carrier.StarSystem,
            state.Carrier.DestinationSystem,
        };

        // The route ahead: systems they are about to arrive in and may ask about by name.
        if (route is not null)
        {
            names.AddRange(route.Ahead(state.Location.StarSystem).Take(10).Select(hop => hop.StarSystem));
        }

        // Their fleet — ship names they chose themselves, which are exactly the words a general-purpose
        // recogniser has never seen.
        names.AddRange(state.Fleet.Ships.Select(ship => ship.Name));
        names.AddRange(state.Fleet.Systems);

        var journal = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())

            // A ship type the Commander never renamed appears twice otherwise, and a repeated name in a bias
            // list spends the budget without adding a word.
            .Distinct(StringComparer.OrdinalIgnoreCase)

            // Single common words are already in every recogniser's vocabulary and would displace a name that
            // is not. "Sol" is the notable exception and is short enough to be misheard, so length rather
            // than word count is the filter.
            .Where(name => name.Length >= 3)
            .Take(Limit - ShippedShare)
            .ToList();

        // And the names d47 ships.
        return
        [
            .. journal
                .Concat(Shipped())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Limit),
        ];
    }

    /// <summary>
    /// Names that are facts about Elite rather than about this Commander (remediation.md 10, item 17).
    /// </summary>
    private static IEnumerable<string> Shipped() =>
        Knowledge.EngineerDirectory.All
            .Select(engineer => engineer.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(ShippedShare);
}
