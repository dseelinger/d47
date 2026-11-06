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
    /// A resistance after a roll, as a fraction. The recipe's change is a share of the damage still taken,
    /// so 0.4 with +50% is 1 - 0.6 x 0.5 = 0.7.
    /// </summary>
    /// <param name="stock">The module's own resistance as a fraction, or null for none.</param>
    /// <param name="attribute">"Kinetic Resistance", "Thermal Resistance" and so on.</param>
    public static double Resistance(
        double? stock,
        string attribute,
        ModuleSpecification? module,
        string? blueprint,
        int grade,
        string? experimental)
    {
        var offered = BlueprintCatalogue.For(module);
        var modification = Modification(offered, blueprint, grade);
        var effect = Experimental(offered, experimental);

        return 1
               - (1 - (stock ?? 0))
               * (1 - ResistanceChange(modification, attribute))
               * (1 - ResistanceChange(effect, attribute));
    }

    /// <summary>
    /// A recipe's change to one resistance. Where it lists "All Resistances" that figure applies to each
    /// of the four and the per-type lines are not added: a Reinforced Shields roll measured in the
    /// journals lands its thermal resistance with the same change as its kinetic.
    /// </summary>
    private static double ResistanceChange(Blueprint? recipe, string attribute)
    {
        var all = Proportion(recipe, "All Resistances");

        return all != 0 ? all : Proportion(recipe, attribute);
    }

    /// <summary>
    /// A boost after a roll, where the boost is a fraction added to 1: a booster's shield boost or a
    /// bulkhead's hull boost. 0.2 with +38% is 1.2 x 1.38 - 1.
    /// </summary>
    /// <param name="attributes">The names the recipes use for it; each recipe states one.</param>
    public static double? Boost(
        double? stock,
        IReadOnlyList<string> attributes,
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
        var modification = Modification(offered, blueprint, grade);
        var effect = Experimental(offered, experimental);

        return (1 + figure)
               * (1 + attributes.Sum(name => Proportion(modification, name)))
               * (1 + attributes.Sum(name => Proportion(effect, name)))
               - 1;
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
