using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// What on-foot engineering costs (list.md Phase 20, "Know what on-foot engineering does").
/// <para>
/// <b>These numbers are not the ones the community sources publish, and that is the point.</b>
/// Every on-foot quantity in EDEngineer predates the patch that cut them, and
/// <c>tools/gen-blueprints.py</c> restates them to what the game charges. The figures asserted here
/// were measured against 16 real upgrade events and four <c>ShipLocker</c> deltas — see
/// <c>docs/spikes/journal-corpus-on-foot.md</c> §3 — so this file is the thing that fails if a
/// regeneration ever quietly puts the published figures back.
/// </para>
/// </summary>
public class OnFootEngineeringTests
{
    private static Blueprint Modification(string name) =>
        Assert.Single(BlueprintCatalogue.All, blueprint =>
            blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon && blueprint.Name == name);

    private static Blueprint Upgrade(string item, int grade) =>
        Assert.Single(BlueprintCatalogue.All, blueprint =>
            blueprint.Kind == BlueprintKind.Vendor && blueprint.Name == item && blueprint.Grade == grade);

    private static int SizeOf(Blueprint recipe, string symbol) =>
        Assert.Single(recipe.Ingredients, ingredient => ingredient.Symbol == symbol).Size;

    /// <summary>
    /// Quieter Footsteps, which the wiki independently prices at exactly these five quantities and
    /// which a locker delta in the corpus spent exactly these five of.
    /// </summary>
    [Fact]
    public void AModificationCostsWhatTheGameChargesRatherThanWhatEDEngineerPublishes()
    {
        var recipe = Modification("Quieter footsteps");

        Assert.Equal(3, SizeOf(recipe, "settlementassaultplans"));
        Assert.Equal(5, SizeOf(recipe, "tacticalplans"));
        Assert.Equal(5, SizeOf(recipe, "patrolroutes"));
        Assert.Equal(3, SizeOf(recipe, "microhydraulics"));
        Assert.Equal(8, SizeOf(recipe, "viscoelasticpolymer"));
    }

    /// <summary>
    /// The whole modification table only ever uses these three sizes. A published 5, 10 or 15
    /// turning up here means the restatement has stopped being applied.
    /// </summary>
    [Fact]
    public void EveryModificationSizeIsOneOfTheThreeMeasuredOnes()
    {
        var sizes = BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon)
            .SelectMany(blueprint => blueprint.Ingredients)
            .Select(ingredient => ingredient.Size)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal([3, 5, 8], sizes);
    }

    /// <summary>
    /// A Maverick's grade 4, which the corpus shows the game charging 4/4/4/9/9 for while
    /// EDEngineer asks 10/10/10/10/25/25.
    /// </summary>
    [Fact]
    public void AGradeUpgradeCostsWhatTheGameChargesAndAsksForNoPowerRegulator()
    {
        var recipe = Upgrade("Maverick", 4);

        Assert.Equal(4, SizeOf(recipe, "suitschematic"));
        Assert.Equal(4, SizeOf(recipe, "healthmonitor"));
        Assert.Equal(4, SizeOf(recipe, "manufacturinginstructions"));
        Assert.Equal(9, SizeOf(recipe, "carbonfibreplating"));
        Assert.Equal(9, SizeOf(recipe, "graphene"));

        // Frontier removed them in 4.0.18.08 and EDEngineer still lists them in all four suit
        // recipes. Absent from all four UpgradeSuit events in the corpus.
        Assert.DoesNotContain(
            recipe.Ingredients,
            ingredient => ingredient.Symbol == "largecapacitypowerregulator");
    }

    /// <summary>
    /// The upgrade ladder uses six sizes and no others, across all 56 recipes. Same guard as the
    /// modification one, and the numbers are different because the two ladders were cut by
    /// different amounts.
    /// </summary>
    [Fact]
    public void EveryUpgradeSizeIsOneOfTheSixMeasuredOnes()
    {
        var sizes = BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind == BlueprintKind.Vendor && blueprint.Grade is not null)
            .SelectMany(blueprint => blueprint.Ingredients)
            .Select(ingredient => ingredient.Size)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal([1, 2, 4, 5, 9, 12], sizes);
    }

    /// <summary>
    /// EDEngineer spells one weapon two ways — "Oppressor" at grade 2 and "Opressor" above it.
    /// Left uncorrected it splits that weapon's ladder in half, so a Commander asking what grade 5
    /// costs is told there is no such recipe.
    /// </summary>
    [Fact]
    public void TheMisspeltWeaponHasOneLadderRatherThanTwo()
    {
        var ladder = BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind == BlueprintKind.Vendor
                                && blueprint.Name.Contains("Oppressor", StringComparison.Ordinal))
            .Select(blueprint => blueprint.Grade)
            .Order()
            .ToArray();

        Assert.Equal([2, 3, 4, 5], ladder);
        Assert.DoesNotContain(BlueprintCatalogue.All, blueprint => blueprint.Name.Contains("Opressor", StringComparison.Ordinal));
    }

    /// <summary>
    /// Per step and not cumulative, so grade 1 to 5 in full is 99 times the base. Measured on a
    /// Manticore Tormentor, whose 50,000 credit base paid 200,000 / 750,000 / 1,500,000 / 2,500,000.
    /// </summary>
    [Fact]
    public void UpgradeCreditsAreTheBasePriceTimesAFixedMultiplierPerStep()
    {
        Assert.Equal(200_000, OnFootRules.UpgradeCost(50_000, 1, 2));
        Assert.Equal(950_000, OnFootRules.UpgradeCost(50_000, 1, 3));
        Assert.Equal(4_950_000, OnFootRules.UpgradeCost(50_000, 1, 5));

        // From where the Commander actually is, which is a different number from "grade 5 costs X".
        Assert.Equal(2_500_000, OnFootRules.UpgradeCost(50_000, 4, 5));
    }

    /// <summary>
    /// Six of the eleven weapons have no published price. A total missing one step reads exactly
    /// like a total that is not, so there is no total at all.
    /// </summary>
    [Fact]
    public void AnUnpricedItemGivesNoCreditTotalRatherThanAShortOne()
    {
        Assert.Null(OnFootRules.UpgradeCost(null, 1, 5));
        Assert.Null(OnFootRules.UpgradeCost(50_000, 5, 5));
        Assert.Null(OnFootRules.UpgradeCost(50_000, 4, 3));
    }

    /// <summary>
    /// The Bartender's rate, which the ship material trader never had one of. Exact on 49 of 49
    /// real trades: Graphene at 13 barter value, Chemical Catalyst at 7 barter cost.
    /// </summary>
    [Fact]
    public void TheBarterRateIsArithmeticAndTheLeftoverIsLost()
    {
        var graphene = MaterialCatalogue.Find("graphene");
        var catalyst = MaterialCatalogue.Find("chemicalcatalyst");

        Assert.NotNull(graphene);
        Assert.NotNull(catalyst);
        Assert.True(graphene.IsBarterable);

        // The measured trade: four Graphene, at 13 each, bought seven Chemical Catalyst at 7.
        Assert.Equal(7, OnFootRules.BarterYield(4 * graphene.BarterValue!.Value, catalyst.BarterCost));

        // And back the other way, rounded up, because the shortfall is the Commander's to cover.
        Assert.Equal(4, OnFootRules.BarterCostOf(7, graphene.BarterValue, catalyst.BarterCost));
    }

    /// <summary>
    /// Only Components can be exchanged. Items and Data are sold for credits, and that is a
    /// different answer from "no rate is known".
    /// </summary>
    [Fact]
    public void OnlyComponentsCanBeBartered()
    {
        var barterable = MaterialCatalogue.All.Where(entry => entry.IsBarterable).ToArray();

        Assert.Equal(33, barterable.Length);
        Assert.All(barterable, entry => Assert.Equal("Component", entry.Category));

        Assert.False(MaterialCatalogue.Find("opinionpolls")?.IsBarterable);
    }

    /// <summary>
    /// The sourcing detail no ship material gets. "Planetary Settlement" is the best origin string
    /// an Odyssey ingredient has; the building code is what turns it into a place to walk to.
    /// </summary>
    [Fact]
    public void MicroResourcesCarryTheBuildingAndContainerAShipMaterialNeverHas()
    {
        var graphene = MaterialCatalogue.Find("graphene");

        Assert.NotNull(graphene);
        Assert.NotEmpty(graphene.Buildings);
        Assert.NotEmpty(graphene.Containers);

        // And a ship material has none of it, which is why the two are asked about differently.
        Assert.Empty(MaterialCatalogue.Find("iron")!.Buildings);
    }

    /// <summary>
    /// Per category and not per item — the sources disagreed and 28,148 locker snapshots settled
    /// it. Consumables carry their own, smaller, per-item cap.
    /// </summary>
    [Fact]
    public void TheLockerCapIsPerCategory()
    {
        Assert.Equal(1000, OnFootRules.LockerCapacityPerCategory);
        Assert.Equal(100, OnFootRules.ConsumableCapacityPerItem);
    }

    /// <summary>
    /// The four Colonia engineers are absent from EDEngineer and are not absent from the game. They
    /// carry the widest modification lists of the thirteen, so a table without them sends a Colonia
    /// Commander to the Bubble for something three of their neighbours offer.
    /// </summary>
    [Fact]
    public void TheColoniaEngineersCarryTheirModificationLists()
    {
        foreach (var name in (string[])["Baltanos", "Eleanor Bresa", "Rosa Dayette", "Yi Shen"])
        {
            var engineer = EngineerDirectory.ByName(name);

            Assert.NotNull(engineer);
            Assert.NotEmpty(engineer.Specialities);
        }
    }

    /// <summary>
    /// The two routing-critical modifications: one Bubble source each, so where they come from
    /// decides the trip rather than decorating it.
    /// </summary>
    [Fact]
    public void NightVisionAndQuieterFootstepsHaveOneBubbleSourceEach()
    {
        Assert.Equal(["Oden Geiger"], Modification("Night vision").Engineers);
        Assert.Equal(["Yarden Bond"], Modification("Quieter footsteps").Engineers);
    }
}
