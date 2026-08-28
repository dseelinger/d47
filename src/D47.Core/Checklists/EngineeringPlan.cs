using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// One thing a Commander wants doing to one slot. The unit a build conversation actually produces.
/// </summary>
/// <param name="Slot">The slot, or the module, as it was said.</param>
/// <param name="Blueprint">
/// The blueprint by name. Null is a wildcard: "something on the thrusters, I don't mind what".
/// </param>
/// <param name="Grade">1 to 5, or null for wildcard — never "unknown".</param>
/// <param name="Engineer">Who would roll it, where the Commander has an opinion.</param>
/// <param name="Experimental">An experimental effect, which is its own item on the same slot.</param>
public sealed record BuildRequest(
    string Slot,
    string? Blueprint = null,
    int? Grade = null,
    string? Engineer = null,
    string? Experimental = null,

    // What the plan means to put there, where it says (asked for 2026-08-24). Carried so a line
    // about an empty slot can name the module rather than the mounting point — see
    // ChecklistIntent.Module, which is where the reasoning is. It is not a request to fit
    // anything: nothing emits a module item off the back of this.
    string? Module = null);

/// <summary>What one ingredient of a plan costs, and how far off the Commander is.</summary>
/// <param name="Held">What they have now. Recomputed every time; never stored.</param>
public sealed record PlanIngredient(MaterialEntry Material, int Needed, int Held)
{
    public int Short => Math.Max(0, Needed - Held);

    /// <summary>
    /// The per-grade cap, for materials that have one. Null for anything measured in tonnes or
    /// held in the ship locker, which is exactly why the ledgers must not be totalled together.
    /// </summary>
    public int? Capacity =>
        Material.Ledger == MaterialLedger.Material && Material.Grade is { } grade
            ? MaterialGrades.CapacityOfGrade(grade)
            : null;

    /// <summary>
    /// Whether this alone forces more than one trip. <b>A flat certainty rather than a
    /// possibility</b>: the total is exact — ingredients × rolls — so needing more than the cap
    /// means at least two visits however the rolls go.
    /// </summary>
    public bool ExceedsCapacity => Capacity is { } capacity && Needed > capacity;
}

/// <summary>What a plan costs and what stands in the way, computed fresh every time it is asked.</summary>
public sealed record PlanCosting
{
    public IReadOnlyList<PlanIngredient> Ingredients { get; init; } = [];

    /// <summary>Requests the Commander's rank cannot reach at all, with what clearing them costs.</summary>
    public IReadOnlyList<string> Gates { get; init; } = [];

    /// <summary>Requests no shipped table covers. Kept and marked, never refused.</summary>
    public IReadOnlyList<string> Uncovered { get; init; } = [];

    public IReadOnlyList<PlanIngredient> Shortfall =>
        [.. Ingredients.Where(ingredient => ingredient.Short > 0)
            .OrderByDescending(ingredient => ingredient.Short)];

    public IReadOnlyList<PlanIngredient> OverCapacity =>
        [.. Ingredients.Where(ingredient => ingredient.ExceedsCapacity)];
}

/// <summary>
/// A build costed into the things a Commander actually has to <em>do</em> (Phase 17, "An
/// engineering plan writes the checklist").
/// <para>
/// <b>A total is exact, and it is a multiplication d47 is entitled to make.</b> An application
/// costs exactly one of each ingredient — measured across 786 blueprints and 1,885 ingredient
/// entries, with not one size other than 1 — and the roll count is a published function of the
/// recipe grade and the Commander's rank: 5, 4, 3, 2, 1 as rank exceeds grade. Known unit cost
/// times known count is a real number, so this states one rather than a floor.
/// </para>
/// <para>
/// <b>Rank gates rather than merely slows.</b> Grade <i>N</i> cannot be rolled below rank
/// <i>N</i>, so an engineer the Commander is rank 3 with is not a slow route to a grade 5
/// blueprint — it is no route at all, and the plan says so instead of listing materials nobody
/// can spend.
/// </para>
/// <para>
/// <b>Caps run first, and they are shared.</b> <c>needed &gt; cap</c> is a flat certainty — at
/// least two trips — and two plans that each fit can be jointly impossible, which is precisely
/// the arithmetic nobody can do in their head. So <see cref="Cost"/> takes every live plan at
/// once rather than one.
/// </para>
/// <para>
/// <b>Unlock tributes join the same list and must not join the same ledger.</b> Meta-alloys are a
/// material, Gold ×200 is two hundred tonnes of cargo, and Opinion Polls ×40 are ship locker;
/// totalling them together produces a feasibility verdict that is nonsense delivered confidently.
/// <see cref="MaterialLedger"/> is what keeps them apart.
/// </para>
/// </summary>
public static class EngineeringPlan
{
    /// <summary>
    /// Turns a set of build requests into checklist items. <b>Independently abandonable intents,
    /// never a target loadout</b>: a Krait has around twenty slots and a conversation about a
    /// build produces opinions about six, so a target loadout means inventing the other fourteen
    /// and then reporting "6 of 20" where the honest number is "6 of 9 things you asked for".
    /// </summary>
    /// <param name="canonicalSlot">
    /// Turns what the Commander said into the slot the journal calls it — "thrusters" into
    /// "MainEngines". <b>Part of identity, not presentation.</b> Two conversations about the same
    /// slot in different words must produce the same item, or a revision reads as the old plan
    /// abandoned and an identical new one opened. Null where no ship is known, and the words then
    /// stand as given, which is the best that can be done without a Loadout.
    /// </param>
    public static IReadOnlyList<ChecklistItem> Items(
        ChecklistScope scope,
        string? hull,
        IReadOnlyList<BuildRequest> requests,
        Func<string, string>? canonicalSlot = null)
    {
        var items = new List<ChecklistItem>();

        foreach (var raw in requests)
        {
            if (string.IsNullOrWhiteSpace(raw.Slot))
            {
                continue;
            }

            var request = raw with { Slot = canonicalSlot?.Invoke(raw.Slot.Trim()) ?? raw.Slot.Trim() };

            if (request.Blueprint is not null || request.Grade is not null || request.Experimental is null)
            {
                items.Add(Item(
                    scope,
                    hull,
                    new ChecklistIntent(ChecklistIntentKind.Blueprint, request.Slot.Trim())
                    {
                        Detail = Blank(request.Blueprint),
                        Grade = request.Grade,
                        Engineer = Blank(request.Engineer),

                        // So an empty slot's line can name what is going in it rather than where
                        // (asked for 2026-08-24).
                        Module = Blank(request.Module),
                    },
                    Say(request)));
            }

            if (Blank(request.Experimental) is { } experimental)
            {
                items.Add(Item(
                    scope,
                    hull,
                    new ChecklistIntent(ChecklistIntentKind.Experimental, request.Slot.Trim())
                    {
                        Detail = experimental,
                        Engineer = Blank(request.Engineer),
                        Module = Blank(request.Module),
                    },
                    $"{experimental} on {request.Slot.Trim()}"));
            }

            // The engineer's own access, where one is named and a grade is wanted. Its own item
            // because it is its own next action, and because rank blocks as surely as materials
            // do — listing the materials under a gate nobody can pass would be listing work
            // nobody can start.
            if (Blank(request.Engineer) is { } named && request.Grade is { } grade)
            {
                items.Add(Item(
                    scope,
                    hull,
                    new ChecklistIntent(ChecklistIntentKind.EngineerAccess, named) { Grade = grade },
                    $"Rank {grade} with {named}"));
            }
        }

        // Keyed, so two requests about the same slot are one item rather than two. Identity is
        // the slot for the slot-shaped intents, so this falls out of the key rather than needing
        // a rule of its own - and a later request about a slot already spoken for wins, because
        // it is the Commander changing their mind rather than a second opinion to keep beside the
        // first (Phase 26).
        return [.. items.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.Last())];
    }

    private static ChecklistItem Item(ChecklistScope scope, string? hull, ChecklistIntent intent, string text) =>
        new()
        {
            Key = ChecklistKeys.For(intent),
            Scope = scope,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = text,
            Intent = intent,
            Hull = hull,

            // Asserted: a shipped table confirmed the blueprint exists at that grade. What a
            // conversation merely settled on is quoted, and the Commander's own note attributed.
            Provenance = ChecklistProvenance.Asserted,
        };

    private static string Say(BuildRequest request)
    {
        var built = new StringBuilder();

        if (request.Grade is { } grade)
        {
            built.Append(CultureInfo.InvariantCulture, $"Grade {grade} ");
        }

        built.Append(request.Blueprint ?? "engineering");
        built.Append(CultureInfo.InvariantCulture, $" on {request.Slot.Trim()}");

        if (request.Engineer is { Length: > 0 } engineer)
        {
            built.Append(CultureInfo.InvariantCulture, $", with {engineer}");
        }

        return built.ToString();
    }

    /// <summary>
    /// What every live engineering item across every list still costs, netted against what the
    /// Commander holds.
    /// <para>
    /// Across all of them at once, deliberately. Caps are shared, so pricing one plan in isolation
    /// answers a question nobody asked — and a shopping trip is a trip for everything, not for one
    /// blueprint.
    /// </para>
    /// </summary>
    public static PlanCosting Cost(IEnumerable<ChecklistItem> items, CommanderGameState? state)
    {
        var needed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var gates = new List<string>();
        var uncovered = new List<string>();

        foreach (var item in items)
        {
            if (!item.IsLive || item.IsComplete || item.Intent is not { } intent)
            {
                continue;
            }

            if (intent.Kind != ChecklistIntentKind.Blueprint && intent.Kind != ChecklistIntentKind.Experimental)
            {
                continue;
            }

            var blueprints = BlueprintCatalogue.Named(intent.Detail);

            if (blueprints.Count == 0)
            {
                // Kept and marked, never refused. A macro refuses an unknown action because it
                // presses keys and would half-configure a ship; a checklist line presses nothing.
                if (intent.Detail is { Length: > 0 } unknown)
                {
                    uncovered.Add($"{item.Text} — I have no recipe under the name \"{unknown}\".");
                }

                continue;
            }

            var rank = RankFor(intent, state);

            if (intent.Kind == ChecklistIntentKind.Experimental)
            {
                Add(needed, blueprints.FirstOrDefault(b => b.Kind == BlueprintKind.Experimental)?.Ingredients);
                continue;
            }

            var grade = intent.Grade;

            if (grade is null)
            {
                // A wildcard grade is a real intent and an uncostable one: which grade decides the
                // multiplication, and the Commander has not said. Marked rather than guessed.
                uncovered.Add($"{item.Text} — no grade named, so there is no total to compute.");
                continue;
            }

            var recipe = blueprints.FirstOrDefault(b => b.Kind == BlueprintKind.Modification && b.Grade == grade);

            if (recipe is null)
            {
                uncovered.Add($"{item.Text} — my table has no grade {grade} for that blueprint.");
                continue;
            }

            if (rank is not { } known)
            {
                // The roll count is a function of rank, and rank is the Commander's own. Without
                // it there is no exact total, and a total is the one thing this item promises.
                uncovered.Add(
                    $"{item.Text} — I do not know your rank with the engineer who would craft it, "
                    + "so I cannot state a total.");
                continue;
            }

            if (recipe.TotalFor(known) is not { } total)
            {
                gates.Add(Gate(item.Text, grade.Value, known, intent.Engineer));
                continue;
            }

            Add(needed, total);
        }

        var ingredients = needed
            .Select(entry => (Material: MaterialCatalogue.Find(entry.Key), entry.Value))
            .Where(pair => pair.Material is not null)
            .Select(pair => new PlanIngredient(
                pair.Material!,
                pair.Value,
                state?.Materials.CountOf(pair.Material!.Symbol) ?? 0))
            .OrderBy(ingredient => ingredient.Material.Name, StringComparer.Ordinal)
            .ToList();

        return new PlanCosting { Ingredients = ingredients, Gates = gates, Uncovered = uncovered };
    }

    private static string Gate(string what, int grade, int rank, string? engineer)
    {
        var who = engineer is { Length: > 0 } named ? $" with {named}" : string.Empty;

        var price = $" {EngineeringRules.RankRises}";

        return $"{what}: grade {grade} cannot be crafted at rank {rank}{who} at all.{price}";
    }

    private static int? RankFor(ChecklistIntent intent, CommanderGameState? state) =>
        EngineerDirectory.ByName(intent.Engineer) is { } engineer
            ? state?.Engineers.For(engineer.Id)?.Rank
            : null;

    private static void Add(Dictionary<string, int> into, IEnumerable<BlueprintIngredient>? ingredients)
    {
        foreach (var ingredient in ingredients ?? [])
        {
            into[ingredient.Symbol] = into.GetValueOrDefault(ingredient.Symbol) + ingredient.Size;
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
