using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>One thing a Commander wants doing to one suit or one hand weapon.</summary>
/// <param name="Grade">
/// The grade to reach, or null for "leave the grade alone". Not a wildcard the way a ship
/// blueprint's is: on foot the grade is bought outright, so an unstated one means no upgrade
/// rather than any upgrade.
/// </param>
/// <param name="Modifications">
/// What to have an engineer fit. <b>At most four ever, and every one permanent.</b>
/// </param>
public sealed record OnFootRequest(
    string Equipment,
    int? Grade = null,
    IReadOnlyList<string>? Modifications = null);

/// <summary>
/// A suit or weapon build costed into the things a Commander has to do, on the same substrate as
/// the ship and colonisation plans (list.md Phase 20, "Plan a suit or a weapon").
/// <para>
/// <b>Permanence is the reason this needs planning more than ships do.</b> Four slots, no undo, no
/// way to practise, and a wrong modification recoverable only by buying and re-upgrading a fresh
/// item. Ships forgive a bad roll; this forgives nothing.
/// </para>
/// <para>
/// <b>Grade before modifications, always — as a step rather than a footnote.</b> A grade 1 item has
/// zero modification slots, so a trip to an engineer before Pioneer Supplies is a wasted trip. And
/// an engineer's base has no Pioneer Supplies, only an Apex desk, which turns the ordering
/// preference into a routing fact: it cannot be fixed on arrival.
/// </para>
/// <para>
/// <b>The arithmetic needs none of the ship plan's hedging.</b> Nothing on foot is rolled, so a
/// total is a sum rather than a product with a roll count in it — <see cref="EngineeringPlan"/>'s
/// rank gates, roll counts and wildcard-grade refusals have no analogue here.
/// </para>
/// </summary>
public static class OnFootPlan
{
    /// <summary>
    /// Turns requests into checklist items, <b>in the order they have to happen</b>: the grade
    /// first, then the modifications it pays for.
    /// <para>
    /// Independently abandonable intents, exactly as the ship plan produces — changing one's mind
    /// about one modification is a tombstone and a new item, not the whole plan rewritten.
    /// </para>
    /// </summary>
    /// <param name="scope">
    /// The suit or weapon this is about, keyed on the id that survives an upgrade. Item identity
    /// hangs off it, so a Maverick taken from grade 3 to 5 keeps its list.
    /// </param>
    public static IReadOnlyList<ChecklistItem> Items(ChecklistScope scope, OnFootRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Equipment))
        {
            return [];
        }

        var equipment = request.Equipment.Trim();
        var items = new List<ChecklistItem>();

        if (request.Grade is { } grade and >= 2 and <= 5)
        {
            items.Add(Item(
                scope,
                new ChecklistIntent(ChecklistIntentKind.Grade, equipment) { Grade = grade },
                $"Grade {grade.ToString(CultureInfo.InvariantCulture)} on {equipment}, at Pioneer Supplies"));
        }

        foreach (var wanted in request.Modifications ?? [])
        {
            if (string.IsNullOrWhiteSpace(wanted))
            {
                continue;
            }

            var modification = wanted.Trim();

            items.Add(Item(
                scope,
                new ChecklistIntent(ChecklistIntentKind.Modification, equipment)
                {
                    Detail = modification,

                    // Whoever offers it, where the table names exactly one. More than one is a
                    // choice the Commander makes on the way, not something a plan should fix.
                    Engineer = SoleEngineer(modification),
                },
                $"{modification} on {equipment}"));
        }

        return [.. items.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.First())];
    }

    /// <summary>
    /// The engineer who offers a modification, where exactly one does. Null where several do —
    /// Night Vision and Quieter Footsteps have one Bubble source each and are the routing-critical
    /// pair, and pinning the others to an arbitrary name would invent a trip.
    /// </summary>
    private static string? SoleEngineer(string modification)
    {
        var offered = BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon)
            .Where(blueprint => ChecklistKeys.Compact(blueprint.Name) == ChecklistKeys.Compact(modification))
            .SelectMany(blueprint => blueprint.Engineers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return offered.Length == 1 ? offered[0] : null;
    }

    private static ChecklistItem Item(ChecklistScope scope, ChecklistIntent intent, string text) =>
        new()
        {
            Key = ChecklistKeys.For(intent),
            Scope = scope,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.OnFootPlan,
            Text = text,
            Intent = intent,
            Provenance = ChecklistProvenance.Asserted,
        };

    /// <summary>
    /// What every live on-foot item still costs, netted against what the Commander is carrying.
    /// <para>
    /// Across all of them at once for the reason the ship plan does it that way: the ship locker's
    /// cap is shared, so two items that each fit can be jointly impossible, and a shopping trip is
    /// a trip for everything.
    /// </para>
    /// <para>
    /// <b>Held comes from the backpack and the locker together</b>, never from
    /// <see cref="MaterialsInventory"/>: on-foot recipes spend micro-resources, and totalling those
    /// against a ship material's per-grade cap produces a feasibility verdict that is nonsense
    /// delivered confidently.
    /// </para>
    /// </summary>
    public static PlanCosting Cost(IEnumerable<ChecklistItem> items, CommanderGameState? state)
    {
        var needed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var blocked = new List<string>();
        var uncovered = new List<string>();

        foreach (var item in items)
        {
            if (!item.IsLive || item.IsComplete || item.Intent is not { } intent)
            {
                continue;
            }

            switch (intent.Kind)
            {
                case ChecklistIntentKind.Grade:
                    Grade(item, intent, needed, uncovered, state);
                    break;

                case ChecklistIntentKind.Modification:
                    Modification(item, intent, needed, uncovered, blocked);
                    break;

                default:
                    continue;
            }
        }

        var ingredients = needed
            .Select(entry => (Material: MaterialCatalogue.Find(entry.Key), entry.Value))
            .Where(pair => pair.Material is not null)
            .Select(pair => new PlanIngredient(
                pair.Material!,
                pair.Value,
                state?.Suit.CountOf(pair.Material!.Symbol) ?? 0))
            .OrderBy(ingredient => ingredient.Material.Name, StringComparer.Ordinal)
            .ToList();

        return new PlanCosting { Ingredients = ingredients, Gates = blocked, Uncovered = uncovered };
    }

    private static void Grade(
        ChecklistItem item,
        ChecklistIntent intent,
        Dictionary<string, int> needed,
        List<string> uncovered,
        CommanderGameState? state)
    {
        var entry = OnFootCatalogue.Named(intent.Subject);

        if (entry is null)
        {
            uncovered.Add($"{item.Text} — I have no suit or weapon called \"{intent.Subject}\".");
            return;
        }

        if (intent.Grade is not { } grade)
        {
            uncovered.Add($"{item.Text} — no grade named, so there is nothing to buy.");
            return;
        }

        // Every step between here and the target, not just the last one. A Commander at grade 2
        // asking for grade 5 pays for 3, 4 and 5, and quoting only the last would understate the
        // trip by two thirds.
        //
        // "Here" is the item the Commander actually owns, not the grade 1 the name resolves to.
        // Falling back to 1 quotes the cost of buying the whole ladder afresh, which is the right
        // answer for a suit they have not got and the wrong one for the suit they are wearing.
        var from = Owned(item, state) ?? entry.Grade ?? 1;
        var found = false;

        for (var step = Math.Max(from + 1, 2); step <= grade; step++)
        {
            var recipe = BlueprintCatalogue.All.FirstOrDefault(blueprint =>
                blueprint.Kind == BlueprintKind.Vendor
                && blueprint.Grade == step
                && string.Equals(blueprint.Name, Vendor(entry.Name), StringComparison.OrdinalIgnoreCase));

            if (recipe is null)
            {
                uncovered.Add($"{item.Text} — my table has no grade {step} recipe for {entry.Name}.");
                continue;
            }

            found = true;
            Add(needed, recipe.Ingredients);
        }

        if (!found && grade > from)
        {
            uncovered.Add($"{item.Text} — I could price none of the steps to grade {grade}.");
        }
    }

    private static void Modification(
        ChecklistItem item,
        ChecklistIntent intent,
        Dictionary<string, int> needed,
        List<string> uncovered,
        List<string> blocked)
    {
        var recipes = BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon)
            .Where(blueprint => ChecklistKeys.Compact(Bare(blueprint.Name))
                                == ChecklistKeys.Compact(intent.Detail))
            .ToArray();

        if (recipes.Length == 0)
        {
            uncovered.Add($"{item.Text} — I have no recipe under the name \"{intent.Detail}\".");
            return;
        }

        if (recipes.Length > 1)
        {
            // Three modifications have a different recipe per weapon manufacturer, and the plan
            // names the item rather than the manufacturer. Costing one of them arbitrarily would
            // quote a Takada price for a Manticore, so it says so instead.
            blocked.Add(
                $"{item.Text}: {intent.Detail} has a different recipe per manufacturer "
                + $"({string.Join(", ", recipes.Select(recipe => recipe.Name))}), so I cannot total it "
                + "until the plan names which weapon.");
            return;
        }

        Add(needed, recipes[0].Ingredients);
    }

    /// <summary>
    /// The grade of the very suit or weapon this list is about, where the Commander has it on them.
    /// Null when they are in something else, which is what makes the total fall back to the whole
    /// ladder rather than silently pricing somebody else's suit.
    /// </summary>
    private static int? Owned(ChecklistItem item, CommanderGameState? state)
    {
        if (state?.OnFoot is not { IsKnown: true } loadout || item.Scope.Key is not { Length: > 0 } key)
        {
            return null;
        }

        if (item.Scope.Group == ChecklistGroup.Suit)
        {
            return loadout.SuitId?.ToString(CultureInfo.InvariantCulture) == key ? loadout.Grade : null;
        }

        return item.Scope.Group == ChecklistGroup.Weapon
            ? loadout.Weapons
                .FirstOrDefault(weapon => weapon.ModuleId?.ToString(CultureInfo.InvariantCulture) == key)?.Grade
            : null;
    }

    /// <summary>
    /// The name the upgrade recipes are filed under. EDDiscovery writes "Maverick Suit" and
    /// EDEngineer writes "Maverick".
    /// </summary>
    private static string Vendor(string name) =>
        name.EndsWith(" Suit", StringComparison.OrdinalIgnoreCase) ? name[..^" Suit".Length] : name;

    /// <summary>A recipe name with the manufacturer in brackets taken off.</summary>
    private static string Bare(string name)
    {
        var bracket = name.IndexOf('(', StringComparison.Ordinal);

        return bracket < 0 ? name : name[..bracket].TrimEnd();
    }

    private static void Add(Dictionary<string, int> into, IEnumerable<BlueprintIngredient> ingredients)
    {
        foreach (var ingredient in ingredients)
        {
            into[ingredient.Symbol] = into.GetValueOrDefault(ingredient.Symbol) + ingredient.Size;
        }
    }
}
