using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>What a roll the Commander has not done yet would land on (Phase 38).</summary>
public static class RollModel
{
    /// <summary>The figure a completed roll lands on, or the stock figure where nothing modifies it.</summary>
    /// <param name="stock">The unengineered figure.</param>
    /// <param name="attribute">
    /// The blueprint table's own name for it — "Power Draw", "Optimal Mass", "Mass".
    /// </param>
    /// <param name="module">The module being rolled, which decides which recipe applies.</param>
    /// <param name="blueprint">
    /// The blueprint by the name a Commander says, or null for none.
    /// </param>
    /// <param name="grade">Its grade. 0 means the plan states no engineering.</param>
    /// <param name="experimental">The experimental effect, which is its own recipe.</param>
    public static double? Apply(
        double? stock,
        string attribute,
        ModuleSpecification? module,
        string? blueprint,
        int grade,
        string? experimental)
    {
        if (stock is not { } figure)
        {
            return null;
        }

        var offered = BlueprintCatalogue.For(module);

        return figure
               * (1 + Proportion(Modification(offered, blueprint, grade), attribute))
               * (1 + Proportion(Experimental(offered, experimental), attribute));
    }

    /// <summary>
    /// The modification row for one blueprint at one grade, out of the recipes this module can actually
    /// take.
    /// </summary>
    private static Blueprint? Modification(
        IReadOnlyList<Blueprint>? offered, string? blueprint, int grade) =>
        blueprint is not { Length: > 0 } named || grade <= 0
            ? null
            : offered?.FirstOrDefault(recipe =>
                recipe.Kind == BlueprintKind.Modification
                && recipe.Grade == grade
                && (string.Equals(recipe.Name, named, StringComparison.OrdinalIgnoreCase)
                    || recipe.Symbols.Contains(named, StringComparer.OrdinalIgnoreCase)));

    /// <summary>The experimental row, by the name a plan carries or by the symbol Elite writes.</summary>
    private static Blueprint? Experimental(IReadOnlyList<Blueprint>? offered, string? experimental) =>
        experimental is not { Length: > 0 } named
            ? null
            : offered?.FirstOrDefault(recipe =>
                recipe.Kind == BlueprintKind.Experimental
                && (string.Equals(recipe.Name, named, StringComparison.OrdinalIgnoreCase)
                    || recipe.Symbols.Contains(named, StringComparer.OrdinalIgnoreCase)));

    /// <summary>
    /// One attribute's change as a fraction — <c>+45%</c> is 0.45 — or zero where the recipe does not
    /// touch it.
    /// </summary>
    private static double Proportion(Blueprint? recipe, string attribute)
    {
        foreach (var effect in recipe?.Effects ?? [])
        {
            if (!string.Equals(effect.Property, attribute, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var change = effect.Change.AsSpan().TrimEnd('%');

            if (change.Length != effect.Change.Length
                && double.TryParse(change, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                return percent / 100;
            }
        }

        return 0;
    }
}
