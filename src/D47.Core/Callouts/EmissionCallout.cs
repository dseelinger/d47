using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// This system might be holding High Grade Emissions, and here is what would be in them
/// (Phase 40, asked for 2026-08-21).
/// <para>
/// <b>Said on arrival, because that is when it is worth anything.</b> A Commander who has already
/// jumped out cannot act on it, and one who is about to has a decision in front of them. So it
/// fires on the events that put them in a system and on nothing else.
/// </para>
/// <para>
/// <b>Allegiance and state both come from the system's controlling faction.</b> Two earlier
/// readings got this wrong in opposite directions — see <see cref="EmissionRules"/>, which holds
/// the Commander's own table and the trail behind it. A system can still offer two unrelated
/// groups, from one faction wearing two states at once.
/// </para>
/// <para>
/// <b>What is already full is not mentioned.</b> Being told to go and collect a material there is
/// no room for is worse than silence: it is a callout that costs attention and cannot be acted on.
/// A material at capacity is dropped, and a group whose materials are all at capacity says nothing
/// at all — so a Commander who has finished gathering stops hearing about it without switching
/// anything off.
/// </para>
/// <para>
/// <b>Capacity is injected rather than looked up here</b>, exactly as
/// <see cref="MaterialMilestoneCallout"/> does it and for the same reason: Elite reports a
/// per-material capacity nowhere, and a material whose grade the table does not know answers null.
/// <b>Null means say it</b> — the opposite of the milestone callout's choice, and deliberately.
/// There, an unknown capacity means a percentage that would have to be invented; here it means
/// only that d47 cannot prove the Commander is full, and staying quiet about a material on that
/// basis is withholding something true.
/// </para>
/// </summary>
public sealed class EmissionCallout : ICallout
{
    public string Id => "emissions";

    public const string Key = "emissions.here";

    /// <summary>
    /// How many of a material can be held, or null when that is not known. Same contract as
    /// <see cref="MaterialMilestoneCallout.Capacity"/>, so both are wired from one place.
    /// </summary>
    public Func<string, int?> Capacity { get; set; } = _ => null;

    /// <summary>
    /// The system last spoken about, so re-reading the same arrival — or dropping in and out of
    /// supercruise around one body — does not say it again. Cleared by arriving somewhere else,
    /// which is the only thing that makes it worth saying twice.
    /// </summary>
    private string? _said;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            // The three events that put a Commander in a system carrying its factions. `Location`
            // is the one written at startup and on re-entering the game, which is why priming
            // matters below rather than here.
            if (journalEvent.Kind is not ("FSDJump" or "Location" or "CarrierJump"))
            {
                continue;
            }

            if (journalEvent.String("StarSystem") is not { Length: > 0 } system)
            {
                continue;
            }

            var arrived = !string.Equals(_said, system, StringComparison.OrdinalIgnoreCase);
            _said = system;

            // Priming replays a backlog: every jump of the last session would otherwise be
            // announced at once, and the only one that could still be acted on is the last.
            if (!arrived || context.IsPriming)
            {
                continue;
            }

            if (Sayable(journalEvent, state) is { Count: > 0 } materials)
            {
                yield return new Announcement(Key, Said(system, materials))
                {
                    // Long, because this is about a place rather than a moment. Arriving back in
                    // the same system twenty minutes later is not news.
                    Cooldown = TimeSpan.FromMinutes(20),
                };
            }
        }
    }

    /// <summary>
    /// Every material this system could put in an emission and the Commander has room for, each
    /// named once and in the order the groups are declared in.
    /// </summary>
    private List<string> Sayable(JournalEvent journalEvent, CommanderGameState state)
    {
        var said = new List<string>();

        if (journalEvent.Long("Population") is not { } population || population < EmissionRules.MinimumPopulation)
        {
            return said;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in EmissionRules.For(journalEvent.String("SystemAllegiance"), States(journalEvent)))
        {
            foreach (var symbol in group.Materials)
            {
                if (!seen.Add(symbol) || IsFull(symbol, state))
                {
                    continue;
                }

                said.Add(MaterialCatalogue.Find(symbol)?.Name ?? symbol);
            }
        }

        return said;
    }

    /// <summary>
    /// Every state the <b>controlling</b> faction is in: its headline <c>FactionState</c> plus its
    /// <c>ActiveStates</c>.
    /// <para>
    /// <b>Found through <c>Factions</c> rather than read off <c>SystemFaction</c>, and that is not
    /// tidiness.</b> Measured over 205 recent <c>FSDJump</c> events: <c>SystemFaction</c> carries
    /// <c>Name</c> on all of them and <c>FactionState</c> on only <b>118</b> — absent two times in
    /// five. The controlling faction is findable by name in the <c>Factions</c> array in 204 of the
    /// 205, and that entry carries both the headline state and the list. So the name comes from
    /// <c>SystemFaction</c> and everything else from the array.
    /// </para>
    /// <para>
    /// Both the headline and the list, because neither is a superset of the other in practice, and
    /// the list is what makes two groups at once expressible at all.
    /// </para>
    /// </summary>
    private static IEnumerable<string> States(JournalEvent journalEvent)
    {
        var controlling = journalEvent.Object("SystemFaction")?.String("Name");

        if (controlling is not { Length: > 0 })
        {
            yield break;
        }

        foreach (var faction in journalEvent.Items("Factions"))
        {
            if (!string.Equals(faction.String("Name"), controlling, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (faction.String("FactionState") is { Length: > 0 } headline)
            {
                yield return headline;
            }

            foreach (var active in faction.Items("ActiveStates"))
            {
                if (active.String("State") is { Length: > 0 } state)
                {
                    yield return state;
                }
            }

            yield break;
        }

        // The one journal in 205 whose controlling faction is not in its own Factions array. The
        // headline state on SystemFaction is all there is, and it is better than nothing.
        if (journalEvent.Object("SystemFaction")?.String("FactionState") is { Length: > 0 } only)
        {
            yield return only;
        }
    }

    /// <summary>
    /// Whether there is no room for another. <b>False where the capacity is not known</b>, so an
    /// unknown answers "say it" — d47 cannot prove the Commander is full, and silence would be
    /// withholding something true.
    /// </summary>
    private bool IsFull(string symbol, CommanderGameState state) =>
        Capacity(symbol) is { } capacity && state.Materials.Find(symbol) is { } held && held.Count >= capacity;

    private static string Said(string system, IReadOnlyList<string> materials) =>
        $"{system} could be running high grade emissions for {Listed(materials)}.";

    /// <summary>"A", "A and B", "A, B and C" — said the way a person says a list.</summary>
    private static string Listed(IReadOnlyList<string> materials) => materials.Count switch
    {
        1 => materials[0],
        2 => $"{materials[0]} and {materials[1]}",
        _ => $"{string.Join(", ", materials.Take(materials.Count - 1))} and {materials[^1]}",
    };
}
