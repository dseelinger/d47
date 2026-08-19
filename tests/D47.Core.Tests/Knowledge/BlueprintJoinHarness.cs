using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Remediation 15 item 6, step one: push every module name the Loadout tab can offer through
/// <see cref="BlueprintCatalogue.ForModule"/> and print what comes back empty.
/// <para>
/// Kept skipped rather than deleted: it is the instrument that produced the classification in
/// remediation.md item 6, and it will be needed again whenever either table is regenerated.
/// <para>
/// It exists to produce the classification the fix needs — which empty results
/// *should* have joined and which are modules that genuinely take no engineering — and the
/// report is explicit that reading the matcher will not settle it and running it will, because
/// `ForModule` goes through the fuzzy <c>Catalogue.Match</c>.
/// </para>
/// </summary>
public class BlueprintJoinHarness
{
    [Fact(Skip = "Harness. Unskip to reprint the classification, then skip it again.")]
    public void PrintWhichModuleNamesFindNoBlueprint()
    {
        var names = EliteSpecifications.Modules
            .Where(module => !module.IsBulkhead)
            .Select(module => module.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var empty = new List<string>();
        var found = new List<string>();

        foreach (var name in names)
        {
            var recipes = BlueprintCatalogue.ForModule(name);

            (recipes.Count == 0 ? empty : found).Add(
                recipes.Count == 0
                    ? name
                    : $"{name} -> {recipes.Count} ({recipes.Select(r => r.Name).Distinct().Count()} distinct)");
        }

        var lines = new List<string>
        {
            $"module names (non-bulkhead): {names.Count}",
            $"  find blueprints: {found.Count}",
            $"  find none:       {empty.Count}",
            string.Empty,
            "=== FIND NONE ===",
        };

        lines.AddRange(empty);
        lines.Add(string.Empty);
        lines.Add("=== FIND SOME ===");
        lines.AddRange(found);

        // Also: which blueprint-table module keys nothing in the specification table reaches.
        // The other direction of the same join, and the one that names what case 2 must map to.
        var reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            foreach (var recipe in BlueprintCatalogue.ForModule(name))
            {
                reached.Add(recipe.Module);
            }
        }

        var unreached = BlueprintCatalogue.Modules
            .Where(module => !reached.Contains(module))
            .Order(StringComparer.Ordinal)
            .ToList();

        lines.Add(string.Empty);
        lines.Add($"=== BLUEPRINT MODULE KEYS NO SPECIFICATION NAME REACHES ({unreached.Count}) ===");
        lines.AddRange(unreached);

        lines.Add(string.Empty);
        lines.Add($"=== ALL {BlueprintCatalogue.Modules.Count} BLUEPRINT MODULE KEYS ===");
        lines.AddRange(BlueprintCatalogue.Modules);

        File.WriteAllLines(
            Path.Combine(Path.GetTempPath(), "d47-blueprint-join.txt"),
            lines);

        Assert.True(true);
    }
}
