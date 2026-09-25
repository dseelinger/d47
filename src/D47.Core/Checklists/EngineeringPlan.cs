using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>One thing a Commander wants doing to one slot.</summary>
/// <param name="Slot">The slot, or the module, as it was said.</param>
/// <param name="Blueprint">The blueprint by name.</param>
/// <param name="Grade">1 to 5, or null for wildcard — never "unknown".</param>
/// <param name="Experimental">
/// An experimental effect, which is its own item on the same slot.
/// </param>
public sealed record BuildRequest(
    string Slot,
    string? Blueprint = null,
    int? Grade = null,
    string? Experimental = null,

    // What the plan means to put there, where it says (asked for 2026-08-24).
    string? Module = null);

/// <summary>What one ingredient of a plan costs, and how far off the Commander is.</summary>
/// <param name="Held">What they have now.</param>
public sealed record PlanIngredient(MaterialEntry Material, int Needed, int Held)
{
    public int Short => Math.Max(0, Needed - Held);

    /// <summary>The per-grade cap, for materials that have one.</summary>
    public int? Capacity =>
        Material.Ledger == MaterialLedger.Material && Material.Grade is { } grade
            ? MaterialGrades.CapacityOfGrade(grade)
            : null;

    /// <summary>Whether this alone forces more than one trip.</summary>
    public bool ExceedsCapacity => Capacity is { } capacity && Needed > capacity;
}

/// <summary>What a plan costs and what stands in the way, computed fresh every time it is asked.</summary>
public sealed record PlanCosting
{
    public IReadOnlyList<PlanIngredient> Ingredients { get; init; } = [];

    /// <summary>Requests the Commander's rank cannot reach at all, with what clearing them costs.</summary>
    public IReadOnlyList<string> Gates { get; init; } = [];

    /// <summary>Requests no shipped table covers.</summary>
    public IReadOnlyList<string> Uncovered { get; init; } = [];

    /// <summary>Requests costed at the most expensive module kind they could be on.</summary>
    public IReadOnlyList<string> Assumed { get; init; } = [];

    public IReadOnlyList<PlanIngredient> Shortfall =>
        [.. Ingredients.Where(ingredient => ingredient.Short > 0)
            .OrderByDescending(ingredient => ingredient.Short)];

    public IReadOnlyList<PlanIngredient> OverCapacity =>
        [.. Ingredients.Where(ingredient => ingredient.ExceedsCapacity)];
}

/// <summary>
/// A build costed into the things a Commander actually has to do (Phase 17, "An engineering plan writes
/// the checklist").
/// </summary>
public static class EngineeringPlan
{
    /// <summary>Turns a set of build requests into checklist items.</summary>
    /// <param name="canonicalSlot">
    /// Turns what the Commander said into the slot the journal calls it — "thrusters" into
    /// "MainEngines".
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

                        // So an empty slot's line can name what is going in it rather than where (asked for
                        // 2026-08-24).
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
                        Module = Blank(request.Module),
                    },
                    $"{experimental} on {request.Slot.Trim()}"));
            }
        }

        // Keyed, so two requests about the same slot are one item rather than two.
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

            // Asserted: a shipped table confirmed the blueprint exists at that grade.
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

        return built.ToString();
    }

    /// <summary>
    /// What every live engineering item across every list still costs, netted against what the
    /// Commander holds.
    /// </summary>
    public static PlanCosting Cost(IEnumerable<ChecklistItem> items, CommanderGameState? state)
    {
        var needed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var gates = new List<string>();
        var uncovered = new List<string>();
        var assumed = new List<string>();

        foreach (var item in items)
        {
            if (item.IsComplete || item.Intent is not { } intent)
            {
                continue;
            }

            if (intent.Kind != ChecklistIntentKind.Blueprint && intent.Kind != ChecklistIntentKind.Experimental)
            {
                continue;
            }

            var named = BlueprintCatalogue.Named(intent.Detail);

            if (named.Count == 0)
            {
                // Kept and marked, never refused.
                if (intent.Detail is { Length: > 0 } unknown)
                {
                    uncovered.Add($"{item.Text} — I have no recipe under the name \"{unknown}\".");
                }

                continue;
            }

            var grade = intent.Kind == ChecklistIntentKind.Blueprint ? intent.Grade : null;

            if (intent.Kind == ChecklistIntentKind.Blueprint && grade is null)
            {
                // A wildcard grade is a real intent and an uncostable one: which grade decides the
                // multiplication, and the Commander has not said.
                uncovered.Add($"{item.Text} — no grade named, so there is no total to compute.");
                continue;
            }

            var wanted = intent.Kind == ChecklistIntentKind.Experimental
                ? BlueprintKind.Experimental
                : BlueprintKind.Modification;

            // The fitted module speaks only for a line about one ship: anything else would read whatever
            // hull is being flown.
            var fitted = item.Scope.Group == ChecklistGroup.Ship ? FittedModule.Of(item, state) : null;

            var options = PlannedModule.Candidates(
                    intent.Detail,
                    Blank(intent.Module),
                    fitted,
                    EliteSpecifications.Slot(item.Hull, intent.Subject))
                .Where(recipe => recipe.Kind == wanted);

            if (grade is { } wantedGrade)
            {
                options = options.Where(recipe => recipe.Grade == wantedGrade);
            }

            var recipe = PlannedModule.Choose([.. options], out var guessed);

            if (recipe is null)
            {
                if (intent.Kind == ChecklistIntentKind.Blueprint)
                {
                    uncovered.Add($"{item.Text} — my table has no grade {grade} for that blueprint.");
                }

                continue;
            }

            if (guessed)
            {
                assumed.Add($"{item.Text} — I can't tell which module that is, so it is counted as a {recipe.Module}, "
                            + "the most expensive it could be.");
            }

            if (intent.Kind == ChecklistIntentKind.Experimental)
            {
                Add(needed, recipe.Ingredients);
                continue;
            }

            var rank = RankFor(recipe, intent, state);

            if (rank is { } known && recipe.TotalFor(known) is { } total)
            {
                Add(needed, total);
                continue;
            }

            // Nobody unlocked can reach it yet: counted at the worst case rather than left uncosted, and the
            // gate line says why.
            Add(needed, recipe.TotalFor(grade!.Value));
            gates.Add(Gate(item.Text, grade.Value));
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

        return new PlanCosting { Ingredients = ingredients, Gates = gates, Uncovered = uncovered, Assumed = assumed };
    }

    private static string Gate(string what, int grade) =>
        $"{what} — no engineer you have unlocked offers grade "
        + $"{grade.ToString(CultureInfo.InvariantCulture)} yet; counted at the most rolls it can take.";

    /// <summary>
    /// The highest rank among the engineers who could craft this, unlocked ones only — the recipe's own
    /// list, or just the one the item names where it names one.
    /// </summary>
    private static int? RankFor(Blueprint recipe, ChecklistIntent intent, CommanderGameState? state)
    {
        if (state is null)
        {
            return null;
        }

        IReadOnlyList<string> candidates = Blank(intent.Engineer) is { } named ? [named] : recipe.Engineers;

        int? best = null;

        foreach (var candidate in candidates)
        {
            if (EngineerDirectory.ByName(candidate) is not { } engineer)
            {
                continue;
            }

            if (state.Engineers.For(engineer.Id) is not { IsUnlocked: true, Rank: { } rank })
            {
                continue;
            }

            if (best is null || rank > best)
            {
                best = rank;
            }
        }

        return best;
    }

    private static void Add(Dictionary<string, int> into, IEnumerable<BlueprintIngredient>? ingredients)
    {
        foreach (var ingredient in ingredients ?? [])
        {
            into[ingredient.Symbol] = into.GetValueOrDefault(ingredient.Symbol) + ingredient.Size;
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
