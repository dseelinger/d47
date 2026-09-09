using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// This system might be holding High Grade Emissions, and here is what would be in them (Phase 40,
/// asked for 2026-08-21).
/// </summary>
/// <param name="logger">
/// Where to say that neither the journal nor the remembered loadouts can describe the ship (#337).
/// </param>
public sealed class EmissionCallout(ILogger? logger = null) : ICallout
{
    public string Id => "emissions";

    public const string Key = "emissions.here";

    /// <summary>The Collector Limpet Controller symbol family.</summary>
    private const string CollectorController = "int_dronecontrol_collection";

    /// <summary>
    /// The Multi Limpet Controllers, which do not share the stem above: Elite writes them as
    /// <c>int_multidronecontrol_&lt;kind&gt;_size&lt;n&gt;_class&lt;n&gt;</c> — mining, operations,
    /// rescue, universal, xeno — and several of those launch collectors.
    /// </summary>
    private const string MultiController = "int_multidronecontrol_";

    /// <summary>
    /// Whether the ship can send a limpet after the materials, or null where nothing can say what is
    /// fitted on it.
    /// </summary>
    private static bool? Collects(CommanderGameState state, string asking, ILogger? logger) =>
        state.Fitted(CollectorController, asking, logger) is { } collector
            ? collector || state.Fitted(MultiController, asking, logger) is true
            : null;

    /// <summary>
    /// Whether there is a limpet aboard to send, or null where the hold says nothing about the ship —
    /// never written, or written for the SRV.
    /// </summary>
    private static bool? Stocked(CommanderGameState state) =>
        state.Hold is { IsKnown: true, IsShip: true } hold
            ? hold.Of(LimpetCallout.Limpet) > 0
            : null;

    /// <summary>How many of a material can be held, or null when that is not known.</summary>
    public Func<string, int?> Capacity { get; set; } = _ => null;

    /// <summary>
    /// The system last spoken about, so re-reading the same arrival — or dropping in and out of
    /// supercruise around one body — does not say it again.
    /// </summary>
    private string? _said;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        // Materials in a High Grade Emission come out by limpet or not at all.
        var collects = Collects(
            state, FlownShipLoadout.Asked(context.Events, "an emissions check"), logger);

        foreach (var journalEvent in context.Events)
        {
            // The three events that put a Commander in a system carrying its factions. `Location` is the one
            // written at startup and on re-entering the game, which is why priming matters below rather than
            // here.
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

            // Priming replays a backlog: every jump of the last session would otherwise be announced at once,
            // and the only one that could still be acted on is the last.
            if (!arrived || context.IsPriming)
            {
                continue;
            }

            // No controller and no limpets are one condition heard two ways: either leaves the Commander
            // unable to act on a word of this.
            if (collects is false || Stocked(state) is false)
            {
                continue;
            }

            if (Worth(journalEvent, state) is { Count: > 0 } materials)
            {
                yield return new Announcement(Key, Said(system, materials))
                {
                    // Long, because this is about a place rather than a moment.
                    Cooldown = TimeSpan.FromMinutes(20),
                };
            }
        }
    }

    /// <summary>
    /// One material worth naming, and how much room is left for it — null where the capacity is not
    /// known, which is the same unknown that makes it worth naming at all.
    /// </summary>
    private readonly record struct Sayable(string Name, int? Room);

    /// <summary>
    /// Every material this system could put in an emission and the Commander has room for, each named
    /// once and in the order the groups are declared in, carrying that room with it.
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

                // Full.
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
    /// Every state the controlling faction is in: its headline <c>FactionState</c> plus its
    /// <c>ActiveStates</c>.
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

        // The one journal in 205 whose controlling faction is not in its own Factions array.
        if (journalEvent.Object("SystemFaction")?.String("FactionState") is { Length: > 0 } only)
        {
            yield return only;
        }
    }

    /// <summary>
    /// How many more of a material the Commander can carry, or null where the capacity is not known.
    /// </summary>
    private int? RoomFor(string symbol, CommanderGameState state) =>
        Capacity(symbol) is { } capacity ? capacity - state.Materials.CountOf(symbol) : null;

    private static string Said(string system, IReadOnlyList<Sayable> materials) =>
        $"{system} could be running high grade emissions for {Listed(materials)}.";

    /// <summary>"A", "A and B", "A, B and C" — said the way a person says a list.</summary>
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

    /// <summary>One material and its headroom.</summary>
    private static string Describe(Sayable material) =>
        material.Room is { } room ? $"{material.Name}, {room} short" : material.Name;
}
