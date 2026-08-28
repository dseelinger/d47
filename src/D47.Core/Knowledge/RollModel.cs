using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>
/// What a roll the Commander has not done yet would land on (Phase 38).
/// <para>
/// <b>No new source, and no new table.</b> <c>Blueprints.tsv</c>'s <c>effects</c> column has
/// carried an attribute, a change and a good-or-bad flag <em>per grade</em> since remediation 15
/// item 8 — the Loadout tab's <i>Effect</i> line has been reading it and printing it as a sentence
/// ever since. All this adds is applying the percentages instead of only quoting them. The figure
/// each one applies to is the module's own row in <see cref="EliteSpecifications"/>.
/// </para>
/// <para>
/// <b>A blueprint and an experimental compound; they do not add.</b> The table does not say which,
/// so it was measured: over every distinct engineered module in the 918-journal corpus, 366
/// comparisons against the <c>Modifiers</c> Elite actually wrote,
/// <c>stock × (1 + blueprint) × (1 + experimental)</c> is exact on all 27 that carry both and
/// adding them is out by a median 1.4%. See <c>docs/spikes/build-gauges.md</c>.
/// </para>
/// <para>
/// <b>It models a <em>finished</em> grade.</b> 335 of the 366 comparisons are on modules Elite
/// reports at quality 1.0 and every one of those is exact; the eight that miss are all part-rolled
/// and all land short of the model. That is the right semantics for a plan — a plan is what the
/// Commander gets when they finish — and it is why a modelled figure must never be drawn as
/// though it were measured.
/// </para>
/// </summary>
public static class RollModel
{
    /// <summary>
    /// The figure a completed roll lands on, or the stock figure where nothing modifies it.
    /// </summary>
    /// <param name="stock">The unengineered figure. Null in, null out.</param>
    /// <param name="attribute">
    /// The blueprint table's own name for it — "Power Draw", "Optimal Mass", "Mass". <b>Frontier's
    /// wording</b>, matching the outfitting screen, which is the same argument the slot headings
    /// already won.
    /// </param>
    /// <param name="module">The module being rolled, which decides <em>which</em> recipe applies.</param>
    /// <param name="blueprint">The blueprint by the name a Commander says, or null for none.</param>
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
    /// The modification row for one blueprint at one grade, out of the recipes this module can
    /// actually take. Null where the plan names none, or where the grade is not offered.
    /// <para>
    /// <b>Narrowed by module first.</b> "Lightweight" exists on fifteen module kinds with
    /// different figures, so a lookup by name alone would model an armour roll onto a sensor.
    /// </para>
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

    /// <summary>
    /// The experimental row, by the name a plan carries or by the symbol Elite writes. Null where
    /// there is none.
    /// </summary>
    private static Blueprint? Experimental(IReadOnlyList<Blueprint>? offered, string? experimental) =>
        experimental is not { Length: > 0 } named
            ? null
            : offered?.FirstOrDefault(recipe =>
                recipe.Kind == BlueprintKind.Experimental
                && (string.Equals(recipe.Name, named, StringComparison.OrdinalIgnoreCase)
                    || recipe.Symbols.Contains(named, StringComparer.OrdinalIgnoreCase)));

    /// <summary>
    /// One attribute's change as a fraction — <c>+45%</c> is 0.45 — or zero where the recipe does
    /// not touch it.
    /// <para>
    /// <b>Zero rather than null for the six conversions stated as a tick.</b> An effect the source
    /// records as happening rather than as a quantity ("Damage partially thermal") moves no number
    /// this can multiply, and treating it as no change is the only truthful reading of a figure
    /// nobody published.
    /// </para>
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
