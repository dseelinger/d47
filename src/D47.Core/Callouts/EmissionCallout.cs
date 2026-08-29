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
/// <b>And the line says how much room is left, which is what stopped it reading as a bug</b>
/// (<a href="https://github.com/dseelinger/d47/issues/132">#132</a>). Reported as <i>"it should
/// only tell me this when I am not full"</i> — and it already only did: the Commander had 5, 36 and
/// 20 units of headroom across the three materials it named. The filter was right and the sentence
/// was unfalsifiable, reading identically whether there was room for one unit or a hundred and
/// fifty. The numbers were in hand at the moment it spoke and were thrown away, so this costs
/// nothing but the wording and makes the callout self-explaining: a Commander can tell at once
/// whether dropping out is worth it, and never has to wonder why d47 spoke.
/// </para>
/// <para>
/// <b>No nearly-full threshold ships, and that is the decision rather than an omission.</b> Five
/// short of a hundred is arguably full, and silencing it was the obvious companion change. It is
/// declined because the complaint was never <em>stop talking</em> but <em>why are you telling me
/// this</em>, and a number answers that; because a Commander finishing one specific roll wants
/// those last five; and because a percentage and a flat count behave differently across grades —
/// 95% of a grade-1 cap is fifteen units of headroom and of a grade-5 cap is five — so shipping one
/// would mean choosing between two rules on no evidence. If one is ever wanted it is a settings row
/// with a stated default, never a constant here.
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

            if (Worth(journalEvent, state) is { Count: > 0 } materials)
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
    /// One material worth naming, and how much room is left for it — null where the capacity is
    /// not known, which is the same unknown that makes it worth naming at all.
    /// </summary>
    private readonly record struct Sayable(string Name, int? Room);

    /// <summary>
    /// Every material this system could put in an emission and the Commander has room for, each
    /// named once and in the order the groups are declared in, carrying that room with it.
    /// </summary>
    private List<Sayable> Worth(JournalEvent journalEvent, CommanderGameState state)
    {
        var said = new List<Sayable>();

        if (journalEvent.Long("Population") is not { } population || population < EmissionRules.MinimumPopulation)
        {
            return said;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in EmissionRules.For(journalEvent.String("SystemAllegiance"), States(journalEvent)))
        {
            foreach (var symbol in group.Materials)
            {
                if (!seen.Add(symbol))
                {
                    continue;
                }

                var room = RoomFor(symbol, state);

                // Full. The one reading where a known room decides not to speak rather than what
                // to say.
                if (room is <= 0)
                {
                    continue;
                }

                said.Add(new Sayable(MaterialCatalogue.Find(symbol)?.Name ?? symbol, room));
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
    /// How many more of a material the Commander can carry, or null where the capacity is not
    /// known. Zero or less is full.
    /// <para>
    /// <b>One lookup answers both questions</b>, which is the point of #132: what to skip and what
    /// to say are the same number, and computing them apart is how a sentence comes to disagree
    /// with the filter that produced it.
    /// </para>
    /// <para>
    /// <b>Null still means "say it"</b> — the behaviour this replaces, unchanged. The opposite of
    /// the milestone callout's choice and deliberately: there an unknown means a percentage that
    /// would have to be invented, here it means only that d47 cannot prove the Commander is full,
    /// and silence on that basis would be withholding something true. Such a material is named
    /// without a number rather than with a guessed one.
    /// </para>
    /// </summary>
    private int? RoomFor(string symbol, CommanderGameState state) =>
        Capacity(symbol) is { } capacity ? capacity - state.Materials.CountOf(symbol) : null;

    private static string Said(string system, IReadOnlyList<Sayable> materials) =>
        $"{system} could be running high grade emissions for {Listed(materials)}.";

    /// <summary>
    /// "A", "A and B", "A, B and C" — said the way a person says a list.
    /// <para>
    /// <b>Semicolons once any entry carries a number</b>, because those entries carry a comma of
    /// their own and two levels of list on one separator is unparseable in the ear: <i>"Proto Heat
    /// Radiators, 5 short, Proto Light Alloys, 36 short"</i> is four things or two depending on how
    /// you hear it. A list where nothing has a number keeps the ordinary commas.
    /// </para>
    /// </summary>
    private static string Listed(IReadOnlyList<Sayable> materials)
    {
        var said = materials.Select(Describe).ToList();
        var separator = materials.Any(material => material.Room is not null) ? "; " : ", ";

        return said.Count switch
        {
            1 => said[0],
            2 => $"{said[0]} and {said[1]}",
            _ => $"{string.Join(separator, said.Take(said.Count - 1))} and {said[^1]}",
        };
    }

    /// <summary>
    /// One material and its headroom. <b>The unit is repeated on every entry rather than stated
    /// once at the front</b> — the issue's own example elides it after the first, which reads well
    /// until the first material is the one with an unknown capacity and the list carries no unit
    /// anywhere.
    /// </summary>
    private static string Describe(Sayable material) =>
        material.Room is { } room ? $"{material.Name}, {room} short" : material.Name;
}
