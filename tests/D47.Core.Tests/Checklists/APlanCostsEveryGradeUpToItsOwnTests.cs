using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// A grade N engineering line costs every grade a Commander still has to roll to reach N, from what is fitted
/// in the slot now (#2749).
/// </summary>
public class APlanCostsEveryGradeUpToItsOwnTests
{
    private static readonly ChecklistScope Krait = ChecklistScope.Ship(12);

    private const string ElviraRankThree =
        """
        { "timestamp":"2026-08-16T09:00:00Z", "event":"EngineerProgress",
          "Engineers":[ {"Engineer":"Elvira Martuuk","EngineerID":300160,"Progress":"Unlocked","Rank":3} ] }
        """;

    private const string HoldNothing =
        """{ "timestamp":"2026-08-16T09:00:01Z", "event":"Materials", "Raw":[], "Manufactured":[], "Encoded":[] }""";

    private static string Fsd(string blueprint, int level, double quality) =>
        $$$"""
        { "timestamp":"2026-08-16T09:00:02Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
          "ShipName":"Bad Idea", "ShipIdent":"BI-01",
          "Modules":[ {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size5_class5","On":true,"Priority":0,"Health":1.0,
                       "Engineering":{"Engineer":"Elvira Martuuk","EngineerID":300160,"BlueprintID":1,
                                      "BlueprintName":"{{{blueprint}}}","Level":{{{level}}},"Quality":{{{quality.ToString(System.Globalization.CultureInfo.InvariantCulture)}}},
                                      "Modifiers":[]}} ] }
        """;

    private static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static IReadOnlyList<ChecklistItem> RangeToGradeFive(string? variant = null) =>
        EngineeringPlan.Items(Krait, "krait_mkii", [new BuildRequest("FrameShiftDrive", "Increased FSD Range", 5, Variant: variant)]);

    private static Dictionary<string, int> Needed(PlanCosting costing) =>
        costing.Ingredients.ToDictionary(ingredient => ingredient.Material.Symbol, ingredient => ingredient.Needed, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void EveryGradeOfEveryBlueprintIsCostedFromGradeOne()
    {
        var failures = new List<string>();

        foreach (var recipe in BlueprintCatalogue.All.Where(recipe => recipe.Kind == BlueprintKind.Modification && recipe.Grade is not null))
        {
            var grade = recipe.Grade!.Value;
            var items = EngineeringPlan.Items(Krait, null, [new BuildRequest("Slot", recipe.Name, grade, Module: recipe.Module)]);
            var costing = EngineeringPlan.Cost(items, null);
            var costed = costing.Ingredients.Select(ingredient => ingredient.Material.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var expected = BlueprintCatalogue.All
                .Where(other => other.Kind == BlueprintKind.Modification
                                && other.Grade <= grade
                                && string.Equals(other.Module, recipe.Module, StringComparison.Ordinal)
                                && string.Equals(other.Name, recipe.Name, StringComparison.Ordinal))
                .SelectMany(other => other.Ingredients.Select(ingredient => ingredient.Symbol))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var missing = expected.Where(symbol => !costed.Contains(symbol)).ToList();

            if (missing.Count > 0 || costing.Assumed.Count > 0 || costing.Uncovered.Count > 0)
            {
                failures.Add($"Grade {grade} {recipe.Name} on {recipe.Module}: missing {string.Join(", ", missing)}"
                             + $"{string.Concat(costing.Assumed.Concat(costing.Uncovered).Select(line => " · " + line))}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void AnUnengineeredModuleCostsEveryGradeAtTheCraftsTheRankGivesIt()
    {
        var costing = EngineeringPlan.Cost(RangeToGradeFive(), State(ElviraRankThree, HoldNothing));
        var needed = Needed(costing);

        // Grades 1 and 2 take 3 and 4 crafts at rank 3; grades 3 to 5 take 5 each, grades 4 and 5 because rank 3
        // cannot reach them yet.
        Assert.Equal(3 + 4, needed["disruptedwakeechoes"]);
        Assert.Equal(4 + 5, needed["chemicalprocessors"]);
        Assert.Equal(5, needed["phosphorus"]);
        Assert.Equal(5, needed["manganese"]);
        Assert.Equal(5, needed["arsenic"]);
        Assert.Contains("Grade 5", string.Join(" ", costing.Gates), StringComparison.Ordinal);
    }

    [Fact]
    public void AGradeTheFittedModuleHasFinishedIsNotCostedAgain()
    {
        var costing = EngineeringPlan.Cost(RangeToGradeFive(), State(ElviraRankThree, HoldNothing, Fsd("FSD_LongRange", 2, 0.85)));
        var needed = Needed(costing);

        Assert.DoesNotContain("disruptedwakeechoes", needed.Keys);
        Assert.Equal(5, needed["chemicalprocessors"]);
        Assert.Equal(5, needed["phosphorus"]);
        Assert.Equal(5, needed["wakesolutions"]);
        Assert.Equal(5, needed["arsenic"]);
    }

    [Fact]
    public void APartFinishedFittedGradeCostsOnlyTheCraftsLeftInIt()
    {
        // Grade 3 at rank 3 is five crafts of 0.2; at 0.4 three are left.
        var costing = EngineeringPlan.Cost(RangeToGradeFive(), State(ElviraRankThree, HoldNothing, Fsd("FSD_LongRange", 3, 0.4)));
        var needed = Needed(costing);

        Assert.DoesNotContain("disruptedwakeechoes", needed.Keys);
        Assert.Equal(3, needed["chemicalprocessors"]);
        Assert.Equal(3, needed["phosphorus"]);
        Assert.Equal(5, needed["manganese"]);
    }

    [Fact]
    public void AnotherBlueprintOnTheModuleStartsTheClimbAtGradeOne()
    {
        var costing = EngineeringPlan.Cost(RangeToGradeFive(), State(ElviraRankThree, HoldNothing, Fsd("FSD_FastBoot", 4, 1.0)));

        Assert.Equal(3 + 4, Needed(costing)["disruptedwakeechoes"]);
    }

    [Fact]
    public void AModuleThePlanReplacesLendsTheLineNoProgress()
    {
        var items = RangeToGradeFive(variant: "int_hyperdrive_size6_class5");

        var costing = EngineeringPlan.Cost(items, State(ElviraRankThree, HoldNothing, Fsd("FSD_LongRange", 4, 1.0)));

        Assert.Contains(items, item => item.Intent?.Kind == ChecklistIntentKind.Module);
        Assert.Equal(3 + 4, Needed(costing)["disruptedwakeechoes"]);
    }

    [Fact]
    public void AClimbStoppedAtGradeTwoGetsLinesForWhatGradeThreeNeeds()
    {
        var state = State(ElviraRankThree, HoldNothing, Fsd("FSD_LongRange", 2, 0.85));
        var wanted = RangeToGradeFive();

        var lines = PlanMaterials.With(ChecklistDocument.For("F1"), Krait, wanted, state)
            .Where(PlanMaterials.Is)
            .Select(line => line.Intent!.Subject)
            .ToList();

        Assert.Contains("phosphorus", lines, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("wakesolutions", lines, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("arsenic", lines, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("disruptedwakeechoes", lines, StringComparer.OrdinalIgnoreCase);
    }
}
