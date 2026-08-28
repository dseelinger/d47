using D47.Core.Journal;

namespace D47.Core.Listening;

/// <summary>
/// Names drawn from the journal to bias transcription with (Phase 6, "Bias
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
    /// How much of the list is kept for names d47 ships rather than names the journal wrote
    /// (remediation.md 10, item 17).
    /// <para>
    /// Elite's engineers are the case that forced this. "Unlock Lei Cheung" came back as
    /// "Unlockly Chung", and no amount of journal biasing would have helped: an engineer the
    /// Commander has not unlocked appears nowhere in their journal, so the one name they were
    /// saying was the one name that could not be offered. There are 52 of them and they are a
    /// closed set, so they are shipped rather than derived.
    /// </para>
    /// <para>
    /// A share rather than the whole tail, because the prompt is bounded and the journal names
    /// are the ones that change. Where the Commander is now beats every engineer in the galaxy;
    /// the engineers only get what is left.
    /// </para>
    /// </summary>
    public const int ShippedShare = 20;

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

        var journal = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())

            // A ship type the Commander never renamed appears twice otherwise, and a
            // repeated name in a bias list spends the budget without adding a word.
            .Distinct(StringComparer.OrdinalIgnoreCase)

            // Single common words are already in every recogniser's vocabulary and would
            // displace a name that is not. "Sol" is the notable exception and is short
            // enough to be misheard, so length rather than word count is the filter.
            .Where(name => name.Length >= 3)
            .Take(Limit - ShippedShare)
            .ToList();

        // And the names d47 ships. Last, so a short journal list gets the whole budget and a long
        // one still leaves room for the engineers -- which is the case the Commander hit.
        return
        [
            .. journal
                .Concat(Shipped())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Limit),
        ];
    }

    /// <summary>
    /// Names that are facts about Elite rather than about this Commander (remediation.md 10,
    /// item 17).
    /// <para>
    /// The engineers, by name. They are the proper nouns a Commander says most often about people
    /// they have never met — "unlock Lei Cheung", "what does Felicity Farseer do" — and an
    /// engineer they have not unlocked is one their journal has never mentioned, so the journal
    /// half of this list cannot reach them by construction.
    /// </para>
    /// <para>
    /// Their systems are deliberately not added beside them. A system is already offered when the
    /// Commander is anywhere near it, and spending half the shipped share on places they are not
    /// would cost the names of the people they are asking about.
    /// </para>
    /// </summary>
    private static IEnumerable<string> Shipped() =>
        Knowledge.EngineerDirectory.All
            .Select(engineer => engineer.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(ShippedShare);
}
