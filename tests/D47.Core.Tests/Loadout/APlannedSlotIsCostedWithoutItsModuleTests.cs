using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>
/// A planned slot whose blueprint name belongs to several module kinds is costed from the plan, the
/// table and the slot, with no remembered loadout (#474).
/// </summary>
public class APlannedSlotIsCostedWithoutItsModuleTests
{
    private static ShipBuild Intended(string hull, SlotPlan slot) =>
        new("F1", "build-1", hull, null, "Planned", [slot]);

    private static IReadOnlyList<string> Needs(GapReport report) =>
        [.. report.Ledgers.SelectMany(ledger => ledger.Lines).Select(line => line.Material.Symbol.ToLowerInvariant())
            .Order(StringComparer.Ordinal)];

    private static IReadOnlyList<string> Recipe(string module, string blueprint, int grade) =>
        [.. BlueprintCatalogue.Named(blueprint, module).Single(recipe => recipe.Grade == grade)
            .Ingredients.Select(ingredient => ingredient.Symbol.ToLowerInvariant()).Order(StringComparer.Ordinal)];

    private static void CostedAs(GapReport report, string module, string blueprint, int grade)
    {
        Assert.Empty(report.Uncovered);
        Assert.Empty(report.Assumed);
        Assert.Equal(Recipe(module, blueprint, grade), Needs(report));
    }

    [Fact]
    public void HeavyDutyOnAUtilityMountIsAShieldBooster()
    {
        var report = PlanGap.Of([Intended("anaconda", new SlotPlan("TinyHardpoint1", "Heavy Duty", 5))], [], null);

        CostedAs(report, "Shield Booster", "Heavy Duty", 5);
    }

    [Fact]
    public void HeavyDutyInTheArmourSlotIsArmour()
    {
        var report = PlanGap.Of([Intended("anaconda", new SlotPlan("Armour", "Heavy Duty", 5))], [], null);

        CostedAs(report, "Armour", "Heavy Duty", 5);
    }

    [Fact]
    public void HeavyDutyOnANamedBulkheadIsArmour()
    {
        var report = PlanGap.Of(
            [Intended("anaconda", new SlotPlan("Armour", "Heavy Duty", 5, Module: "Anaconda Lightweight Alloy"))],
            [],
            null);

        CostedAs(report, "Armour", "Heavy Duty", 5);
    }

    [Fact]
    public void FastScannerOnAUtilityMountCostsExactly()
    {
        var report = PlanGap.Of([Intended("anaconda", new SlotPlan("TinyHardpoint1", "Fast Scanner", 5))], [], null);

        CostedAs(report, "Kill Warrant Scanner", "Fast Scanner", 5);
    }

    [Fact]
    public void ABlueprintForOneModuleKindIgnoresWhatIsFitted()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-09-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-09-25T09:00:00Z","event":"Loadout","Ship":"anaconda","ShipID":5,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"Slot07_Size5","Item":"int_shieldcellbank_size5_class2","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var owned = new ShipBuild("F1", "build-1", "anaconda", 5, "Flamebrand",
            [new SlotPlan("Slot07_Size5", "Heavy Duty Hull Reinforcement", 5)]);

        var report = PlanGap.Of([owned], [], store.Active);

        Assert.Empty(report.Uncovered);
        Assert.Empty(report.Assumed);
        Assert.Contains(report.Ledgers.SelectMany(ledger => ledger.Lines), line => line.Needed > 0);
    }

    [Fact]
    public void AnAmbiguousBlueprintIsCostedAtItsDearestAndSaysSo()
    {
        // No such slot on the hull, so nothing narrows Heavy Duty past its name.
        var report = PlanGap.Of([Intended("anaconda", new SlotPlan("Nowhere", "Heavy Duty", 5))], [], null);

        var dearest = BlueprintCatalogue.Named("Heavy Duty")
            .Where(recipe => recipe.Kind == BlueprintKind.Modification && recipe.Grade == 5)
            .OrderByDescending(recipe => recipe.Ingredients.Sum(ingredient => ingredient.Size))
            .ThenBy(recipe => recipe.Module, StringComparer.Ordinal)
            .First();

        Assert.Empty(report.Uncovered);
        Assert.Single(report.Assumed);
        Assert.Contains(dearest.Module, report.Assumed[0], StringComparison.Ordinal);
        Assert.Equal(Recipe(dearest.Module, "Heavy Duty", 5), Needs(report));
    }
}
