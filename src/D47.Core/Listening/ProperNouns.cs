using D47.Core.Journal;

namespace D47.Core.Listening;

/// <summary>
/// Names drawn from the journal to bias transcription with (list.md Phase 6, "Bias
/// transcription with proper nouns from the journal").
/// <para>
/// <b>Proper nouns are where speech recognition fails hardest and most silently.</b> A misheard
/// system name does not come back as an error or as a low-confidence marker — it comes back as a
/// plausible English phrase. "Shinrarta Dezhra" becomes "shin arta desha", the turn proceeds
/// confidently on the wrong system, and nothing anywhere reports a problem.
/// </para>
/// <para>
/// Journal-derived and network-free, which is the item's own constraint. Everything here is a
/// name Elite already wrote to disk on this machine, so biasing costs no egress and works with
/// no provider configured.
/// </para>
/// </summary>
public static class ProperNouns
{
    /// <summary>
    /// A cap on how many names are offered. Whisper's initial prompt is bounded, and a list
    /// long enough to overflow it does not fail loudly — it silently displaces the model's own
    /// context, making transcription worse than no biasing at all.
    /// </summary>
    public const int Limit = 60;

    /// <summary>
    /// The names worth biasing towards, most relevant first. Relevance is proximity to what the
    /// Commander is doing: the system they are in and the station they are docked at are far
    /// likelier to be said than a ship parked 200 jumps away, and the cap means the tail is
    /// what gets dropped.
    /// </summary>
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

        // Their fleet — ship names they chose themselves, which are exactly the words a
        // general-purpose recogniser has never seen.
        names.AddRange(state.Fleet.Ships.Select(ship => ship.Name));
        names.AddRange(state.Fleet.Systems);

        return
        [
            .. names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())

                // A ship type the Commander never renamed appears twice otherwise, and a
                // repeated name in a bias list spends the budget without adding a word.
                .Distinct(StringComparer.OrdinalIgnoreCase)

                // Single common words are already in every recogniser's vocabulary and would
                // displace a name that is not. "Sol" is the notable exception and is short
                // enough to be misheard, so length rather than word count is the filter.
                .Where(name => name.Length >= 3)
                .Take(Limit),
        ];
    }
}
