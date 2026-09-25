using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// Which module kind a planned blueprint is costed against, where the blueprint name belongs to several —
/// Heavy Duty on a Shield Booster is not Heavy Duty on Armour.
/// </summary>
public static class PlannedModule
{
    /// <summary>
    /// Every recipe of the named blueprint, narrowed by the planned module, then the blueprint name, then
    /// the fitted module, then the slot, stopping at the first that leaves one module kind.
    /// </summary>
    /// <param name="planned">The module the plan names, by the name a Commander reads.</param>
    /// <param name="fitted">What is fitted in the slot, only where it is the build's own ship.</param>
    public static IReadOnlyList<Blueprint> Candidates(
        string? blueprint,
        string? planned,
        ModuleSpecification? fitted,
        ShipSlot? slot)
    {
        IReadOnlyList<Blueprint> left = BlueprintCatalogue.Named(blueprint);

        if (Kinds(left) <= 1)
        {
            return left;
        }

        IEnumerable<Func<IReadOnlyList<Blueprint>, IReadOnlyList<Blueprint>>> steps =
        [
            recipes => ByPlanned(recipes, planned),
            recipes => fitted is null ? [] : ByTypes(recipes, fitted.Type is { } type ? [type] : []),
            recipes => slot is null
                ? []
                : ByTypes(recipes, [.. EliteSpecifications.ModulesFor(slot).Select(module => module.Type)
                    .OfType<string>()]),
        ];

        foreach (var step in steps)
        {
            var narrowed = step(left);

            if (narrowed.Count == 0)
            {
                continue;
            }

            left = narrowed;

            if (Kinds(left) == 1)
            {
                break;
            }
        }

        return left;
    }

    /// <summary>
    /// The one recipe to cost from a set at one kind and grade: the only module kind, else one of several
    /// identical recipes, else the most expensive. <paramref name="assumed"/> is true for the last.
    /// </summary>
    public static Blueprint? Choose(IReadOnlyList<Blueprint> recipes, out bool assumed)
    {
        assumed = false;

        if (recipes.Count == 0)
        {
            return null;
        }

        if (Kinds(recipes) <= 1 || recipes.Select(Recipe).Distinct(StringComparer.Ordinal).Count() == 1)
        {
            return recipes[0];
        }

        assumed = true;

        return recipes
            .OrderByDescending(recipe => recipe.Ingredients.Sum(ingredient => ingredient.Size))
            .ThenBy(recipe => recipe.Module, StringComparer.Ordinal)
            .First();
    }

    /// <summary>The one module kind the candidates settle on, or null where several remain.</summary>
    public static string? Kind(IReadOnlyList<Blueprint> candidates) =>
        Kinds(candidates) == 1 ? candidates[0].Module : null;

    private static IReadOnlyList<Blueprint> ByPlanned(IReadOnlyList<Blueprint> recipes, string? planned)
    {
        if (planned is not { Length: > 0 })
        {
            return [];
        }

        var types = EliteSpecifications.ModulesNamed(planned, null, null)
            .Select(module => module.Type)
            .OfType<string>()
            .ToList();

        var byType = ByTypes(recipes, types);

        return byType.Count > 0
            ? byType
            : [.. recipes.Where(recipe => string.Equals(recipe.Module, planned.Trim(), StringComparison.OrdinalIgnoreCase))];
    }

    private static IReadOnlyList<Blueprint> ByTypes(IReadOnlyList<Blueprint> recipes, IReadOnlyCollection<string> types) =>
        types.Count == 0
            ? []
            : [.. recipes.Where(recipe => recipe.ModuleTypes.Any(type => types.Contains(type, StringComparer.OrdinalIgnoreCase)))];

    private static int Kinds(IReadOnlyList<Blueprint> recipes) =>
        recipes.Select(recipe => recipe.Module).Distinct(StringComparer.Ordinal).Count();

    private static string Recipe(Blueprint recipe) =>
        string.Join(
            ",",
            recipe.Ingredients
                .OrderBy(ingredient => ingredient.Symbol, StringComparer.OrdinalIgnoreCase)
                .Select(ingredient => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{ingredient.Symbol.ToLowerInvariant()}*{ingredient.Size}")));
}
