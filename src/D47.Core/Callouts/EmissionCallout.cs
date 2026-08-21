using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// This system might be holding High Grade Emissions, and here is what would be in them
/// (list.md Phase 40, asked for 2026-08-21).
/// <para>
/// <b>Said on arrival, because that is when it is worth anything.</b> A Commander who has already
/// jumped out cannot act on it, and one who is about to has a decision in front of them. So it
/// fires on the events that put them in a system and on nothing else.
/// </para>
/// <para>
/// <b>Allegiance is the system's; states are each faction's.</b> That split is the wiki's, it was
/// read wrong in the first version of this, and the correction is why Oppi — an Independent system
/// holding one Federal faction out of seven — no longer has Federal composites announced in it. A
/// system still offers several unrelated groups at once, and always did for the right reason: two
/// of its factions in different <em>states</em>. See <see cref="EmissionRules"/> for the conditions,
/// where they were sourced, and the four readings the Commander ruled on.
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
    /// Every material this system's factions could put in an emission and the Commander has room
    /// for, each named once and in the rank order the groups are declared in.
    /// </summary>
    private List<string> Sayable(JournalEvent journalEvent, CommanderGameState state)
    {
        var said = new List<string>();

        if (journalEvent.Long("Population") is not { } population || population < EmissionRules.MinimumPopulation)
        {
            return said;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Read once, outside the loop, because it is a fact about the system rather than about any
        // faction in it — which is the whole of what was wrong here.
        var allegiance = journalEvent.String("SystemAllegiance");

        foreach (var faction in journalEvent.Items("Factions"))
        {
            if (EmissionRules.For(allegiance, States(faction)) is not { } group)
            {
                continue;
            }

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
    /// Every state a faction is actually in: the headline one plus <c>ActiveStates</c>.
    /// <para>
    /// Both, because neither is a superset of the other in practice — <c>FactionState</c> is what
    /// Elite considers the faction's current state and <c>ActiveStates</c> is the list it is
    /// drawn from, and a journal that carries one without the other is a journal d47 still has to
    /// read.
    /// </para>
    /// </summary>
    private static IEnumerable<string> States(JsonElement faction)
    {
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
