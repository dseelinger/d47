using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.Core.Loadout;

/// <summary>Something that asked for a material, and how many units it wants.</summary>
/// <param name="What">The ship and slot, or the item and mod slot, that wants it.</param>
public sealed record GapDemand(string What, int Units)
{
    /// <summary>
    /// The blueprint this slot is being costed for, and its grade — <em>"Dirty Drive Tuning 3"</em>
    /// (change-requests.md 37).
    /// <para>
    /// <b>Recorded always and printed rarely</b>, which is the ruling: a fleet-wide shortfall can
    /// name a dozen demands, and hanging a blueprint off each would double a line that is read
    /// aloud as often as it is drawn. So the list goes on saying what it said, and this is here for
    /// the answer to <em>what is this for</em>, which asks about one material and has room.
    /// </para>
    /// <para>
    /// <b>Recorded at the fold rather than joined later.</b> The costing already walks one slot at
    /// a time precisely so the answer knows who asked, and the slot has its blueprint in hand right
    /// there. Re-deriving it afterwards would mean matching a printed string back to the plan that
    /// produced it, which is a join this record exists to avoid.
    /// </para>
    /// </summary>
    public string? Blueprint { get; init; }

    public string Describe() =>
        $"{What} — {Units.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// The demand with the blueprint named, for the one-material answer. Falls back to
    /// <see cref="What"/> alone where the plan named no blueprint — a slot can be planned as a
    /// module and nothing else.
    /// </summary>
    public string Fully() =>
        Blueprint is { Length: > 0 } blueprint ? $"{What} · {blueprint}" : What;
}

/// <summary>
/// What a material trader would charge to cover a shortfall (list.md Phase 27, "Gap analysis").
/// <para>
/// <b>Secondary, always.</b> The headline stays the honest raw shortfall; this is the second line
/// beside it, because a trade is a thing the Commander may choose to do and not a reason to
/// report that they need less than they do.
/// </para>
/// </summary>
/// <param name="From">What would be handed over.</param>
/// <param name="Give">How many of it.</param>
/// <param name="Get">How many units of the wanted material come back.</param>
public sealed record TradeOffer(MaterialEntry From, int Give, int Get)
{
    /// <summary>
    /// <b>The trader is named, and it is not a person</b>, reported 2026-08-20. This used to read
    /// "or trade 24 Iron for 8", which says the rate and not whose rate it is — and a rate with no
    /// owner, in a turn that also carried the opening callout naming the engineer the Commander was
    /// standing next to, came back to him as that engineer offering the trade. The same wording
    /// fix is in <c>EngineeringCapability.Netting</c>, which is where the reported sentence came
    /// from; both are read by the same model in the same turn, so both say it.
    /// </summary>
    public string Describe() =>
        $"A material trader would take {Give.ToString(CultureInfo.InvariantCulture)} {From.Name} "
        + $"for {Get.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>One material, what wants it, and how far off the Commander is.</summary>
public sealed record GapLine(MaterialEntry Material, int Needed, int Held)
{
    /// <summary>Every plan that asked for it, most demanding first.</summary>
    public IReadOnlyList<GapDemand> Wanted { get; init; } = [];

    /// <summary>What a trader could cover it with, or null. Never folded into the arithmetic.</summary>
    public TradeOffer? Trade { get; init; }

    public int Short => Math.Max(0, Needed - Held);

    /// <summary>The per-grade cap, for materials that have one.</summary>
    public int? Capacity =>
        Material.Ledger == MaterialLedger.Material && Material.Grade is { } grade
            ? MaterialGrades.CapacityOfGrade(grade)
            : null;

    /// <summary>Whether this alone forces more than one trip. A flat certainty, not a possibility.</summary>
    public bool ExceedsCapacity => Capacity is { } capacity && Needed > capacity;
}

/// <summary>
/// One inventory's worth of the gap. <b>Never summed with another one</b> — see
/// <see cref="PlanGap"/>.
/// </summary>
public sealed record GapLedger(MaterialLedger Ledger, string Name, IReadOnlyList<GapLine> Lines)
{
    /// <summary>Units still to find in this ledger, which is a shopping list rather than a balance.</summary>
    public int UnitsToFind => Lines.Sum(line => line.Short);
}

/// <summary>What the plans need that the Commander is not carrying.</summary>
public sealed record GapReport
{
    public IReadOnlyList<GapLedger> Ledgers { get; init; } = [];

    /// <summary>Requests the Commander's rank cannot reach at all.</summary>
    public IReadOnlyList<string> Gates { get; init; } = [];

    /// <summary>Requests no shipped table covers. Kept and marked, never refused.</summary>
    public IReadOnlyList<string> Uncovered { get; init; } = [];

    /// <summary>How many plans were read, so a page can say what an empty answer means.</summary>
    public int Plans { get; init; }

    /// <summary>How many of them are for something not owned yet.</summary>
    public int Intended { get; init; }

    /// <summary>Whether those were counted. The filter, reported rather than assumed.</summary>
    public bool IncludesIntended { get; init; }

    /// <summary>
    /// The one figure that spans everything: units still to find, across every ledger.
    /// <para>
    /// <b>A count of things to go and get, never a balance.</b> Adding a tonne of gold to a unit of
    /// iron would be adding two different things; counting how many units are outstanding is a
    /// shopping list, and a shopping list is the same shape whatever is on it.
    /// </para>
    /// </summary>
    public int UnitsToFind => Ledgers.Sum(ledger => ledger.UnitsToFind);

    public bool IsEmpty => Ledgers.Count == 0;
}

/// <summary>
/// The arithmetic between what the Commander's plans need and what they are carrying (list.md
/// Phase 27, "Gap analysis").
/// <para>
/// <b>Not a wishlist.</b> A wishlist is a list of things you want, which is what the plans
/// themselves are. This is the subtraction, read across every plan at once — because a Commander
/// gathering materials does not care which ship wanted them.
/// </para>
/// <para>
/// <b>The ledgers are never totalled together.</b> Raw, manufactured and encoded materials, the
/// ship locker and the cargo hold have separate caps and no exchange between them, which is why
/// <see cref="GapLine.Capacity"/> is per ledger and why a single headline number over the top of
/// them would be a lie. The one figure that spans everything counts units still to find.
/// </para>
/// <para>
/// <b>A shortfall reads back to what wants it</b> — the ships and the slots that asked. That is
/// what makes the roll-up navigable rather than merely a total, and it is why this costs each
/// planned item on its own and merges afterwards, rather than handing the whole set to
/// <see cref="EngineeringPlan.Cost"/> and losing the attribution.
/// </para>
/// <para>
/// Core-pure: no clock, no thread, no file. Everything it reads is handed in.
/// </para>
/// </summary>
public static class PlanGap
{
    /// <param name="includeIntended">
    /// Whether hulls and items the Commander does not own yet are counted. <b>A filter rather
    /// than a decision taken on their behalf</b>: counting them is honest about the whole
    /// ambition, and excluding them answers what can be finished now. Both are real questions.
    /// </param>
    /// <param name="canonicalSlot">
    /// Turns what a plan says into the slot the journal calls it, exactly as the ship plan does.
    /// </param>
    public static GapReport Of(
        IReadOnlyList<ShipBuild> ships,
        IReadOnlyList<OnFootBuild> onFoot,
        CommanderGameState? state,
        bool includeIntended = true,
        Func<string, string>? canonicalSlot = null)
    {
        var needed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var held = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var wanted = new Dictionary<string, List<GapDemand>>(StringComparer.OrdinalIgnoreCase);
        var gates = new List<string>();
        var uncovered = new List<string>();

        var plans = 0;
        var intended = 0;

        foreach (var build in ships)
        {
            if (!build.IsOwned)
            {
                intended++;

                if (!includeIntended)
                {
                    continue;
                }
            }

            var planned = build.Slots.Where(slot => !slot.IsEmpty).ToList();

            if (planned.Count == 0)
            {
                continue;
            }

            plans++;

            foreach (var slot in planned)
            {
                // Costed one slot at a time so the answer knows who asked. The totals are the
                // same either way - a sum is a sum - and the caps below are checked on the merged
                // figure, which is where two plans that each fit can be jointly impossible.
                var items = EngineeringPlan.Items(
                    build.Scope ?? ChecklistScope.Universal,
                    build.Hull,
                    [slot.ToRequest()],
                    canonicalSlot);

                Fold(
                    EngineeringPlan.Cost(items, state),
                    $"{build.Describe()} · {slot.Slot}",
                    Named(slot.Blueprint, slot.Grade),
                    needed,
                    held,
                    wanted,
                    gates,
                    uncovered);
            }
        }

        foreach (var build in onFoot)
        {
            if (!build.IsOwned)
            {
                intended++;

                if (!includeIntended)
                {
                    continue;
                }
            }

            var slots = build.Slots.Where(slot => !slot.IsEmpty).ToList();

            if (slots.Count == 0)
            {
                continue;
            }

            plans++;

            var scope = build.Scope ?? ChecklistScope.Universal;

            foreach (var slot in slots)
            {
                var items = OnFootPlan.Items(
                    scope,
                    new OnFootRequest(
                        build.Equipment,
                        slot.Grade,
                        slot.Modification is { Length: > 0 } modification ? [modification] : null));

                Fold(
                    OnFootPlan.Cost(items, state),
                    $"{build.Describe()} · {slot.Slot}",
                    Named(slot.Modification, slot.Grade ?? 0),
                    needed,
                    held,
                    wanted,
                    gates,
                    uncovered);
            }
        }

        var lines = needed
            .Select(entry => (Material: MaterialCatalogue.Find(entry.Key), entry.Value))
            .Where(pair => pair.Material is not null)
            .Select(pair => new GapLine(
                pair.Material!,
                pair.Value,
                held.GetValueOrDefault(pair.Material!.Symbol))
            {
                Wanted = [.. wanted.GetValueOrDefault(pair.Material!.Symbol, [])
                    .OrderByDescending(demand => demand.Units)
                    .ThenBy(demand => demand.What, StringComparer.Ordinal)],
            })
            .Where(line => line.Short > 0)
            .Select(line => line with { Trade = Trades(line, needed, state) })
            .ToList();

        var ledgers = lines
            .GroupBy(line => line.Material.Ledger)
            .OrderBy(group => group.Key)
            .Select(group => new GapLedger(
                group.Key,
                Name(group.Key),
                [.. group.OrderByDescending(line => line.Short)
                    .ThenBy(line => line.Material.Name, StringComparer.Ordinal)]))
            .ToList();

        return new GapReport
        {
            Ledgers = ledgers,
            Gates = gates,
            Uncovered = uncovered,
            Plans = plans,
            Intended = intended,
            IncludesIntended = includeIntended,
        };
    }

    /// <summary>
    /// Adds one costing to the running totals, remembering who asked for what.
    /// <para>
    /// <c>Held</c> is taken from the costing rather than recomputed, because each of the two
    /// costings reads the inventory its own materials actually live in — ship materials from the
    /// <c>Materials</c> event, micro-resources from the backpack and locker. Recomputing here
    /// would mean choosing one of those for both, which is the mistake the ledgers exist to
    /// prevent.
    /// </para>
    /// </summary>
    /// <summary>
    /// What a slot is being costed for, said the way a Commander says it — <em>"Dirty Drive Tuning
    /// 3"</em> — or null where the plan named no blueprint at all (change-requests.md 37).
    /// <para>
    /// Grade 0 is "no grade stated" throughout <see cref="SlotPlan"/>, not grade zero, so it is
    /// left off rather than printed as a number nobody would recognise.
    /// </para>
    /// </summary>
    private static string? Named(string? blueprint, int grade) =>
        blueprint is not { Length: > 0 } named
            ? null
            : grade > 0
                ? $"{ChecklistNaming.Readable(named)} {grade.ToString(CultureInfo.InvariantCulture)}"
                : ChecklistNaming.Readable(named);

    private static void Fold(
        PlanCosting costing,
        string who,
        string? blueprint,
        Dictionary<string, int> needed,
        Dictionary<string, int> held,
        Dictionary<string, List<GapDemand>> wanted,
        List<string> gates,
        List<string> uncovered)
    {
        foreach (var ingredient in costing.Ingredients)
        {
            var symbol = ingredient.Material.Symbol;

            needed[symbol] = needed.GetValueOrDefault(symbol) + ingredient.Needed;
            held[symbol] = ingredient.Held;

            if (!wanted.TryGetValue(symbol, out var asked))
            {
                asked = [];
                wanted[symbol] = asked;
            }

            var at = asked.FindIndex(demand => demand.What == who);

            if (at >= 0)
            {
                asked[at] = asked[at] with { Units = asked[at].Units + ingredient.Needed };
            }
            else
            {
                asked.Add(new GapDemand(who, ingredient.Needed) { Blueprint = blueprint });
            }
        }

        foreach (var gate in costing.Gates.Where(gate => !gates.Contains(gate, StringComparer.Ordinal)))
        {
            gates.Add(gate);
        }

        foreach (var unknown in costing.Uncovered.Where(line => !uncovered.Contains(line, StringComparer.Ordinal)))
        {
            uncovered.Add(unknown);
        }
    }

    /// <summary>
    /// The cheapest trade that would close a shortfall outright, or null when none would.
    /// <para>
    /// <b><see cref="MaterialEntry.Line"/>, never the journal's category.</b> That field names the
    /// Raw/Manufactured/Encoded type, which no trade ever crosses, so using it would read Iron for
    /// Vanadium as a same-line trade and price the commonest trade there is at a sixth of what it
    /// costs — 560 of 1,096 real trades are cross-line.
    /// </para>
    /// <para>
    /// <b>Only from a surplus, and only the whole shortfall.</b> A trade that leaves the Commander
    /// short of the thing they traded away has moved the problem rather than solved it, and half
    /// an answer offered beside an exact one reads as though the exact one were negotiable.
    /// </para>
    /// </summary>
    private static TradeOffer? Trades(
        GapLine line,
        IReadOnlyDictionary<string, int> needed,
        CommanderGameState? state)
    {
        if (!line.Material.IsTradeable || line.Material.Grade is not { } grade)
        {
            return null;
        }

        TradeOffer? best = null;

        foreach (var candidate in MaterialCatalogue.All)
        {
            if (!candidate.IsTradeable
                || candidate.Grade is not { } from
                || candidate.Symbol == line.Material.Symbol)
            {
                continue;
            }

            var sameLine = string.Equals(candidate.Line, line.Material.Line, StringComparison.OrdinalIgnoreCase);

            if (EngineeringRules.TradeRate(from, grade, sameLine) is not { } exchange
                || EngineeringRules.IsBeyondCapacity(from, grade, sameLine))
            {
                continue;
            }

            var batches = (int)Math.Ceiling((double)line.Short / exchange.Received);
            var give = batches * exchange.Paid;

            // What is on them now, less what their own plans already want out of it: trading away
            // a material another slot is waiting on is not a trade, it is a shortfall moved.
            var spare = (state?.Materials.CountOf(candidate.Symbol) ?? 0)
                        - needed.GetValueOrDefault(candidate.Symbol);

            if (give > spare || (best is not null && give >= best.Give))
            {
                continue;
            }

            best = new TradeOffer(candidate, give, batches * exchange.Received);
        }

        return best;
    }

    private static string Name(MaterialLedger ledger) => ledger switch
    {
        MaterialLedger.Material => "Materials",
        MaterialLedger.Cargo => "Cargo, in tonnes",
        MaterialLedger.RareCargo => "Rare cargo, in tonnes",
        MaterialLedger.ShipLocker => "Ship locker",
        _ => "Not in any ledger I recognise",
    };
}
