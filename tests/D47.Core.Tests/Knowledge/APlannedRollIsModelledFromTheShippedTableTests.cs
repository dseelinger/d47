using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Applying a blueprint's own grade row to a stock figure lands on what Elite actually wrote
/// (Phase 38, "Planned rolls modelled from the effects already shipped").
/// <para>
/// <b>The figures below are real rolls out of the journal corpus</b>, not invented ones: the
/// before-and-after of each is what Elite reported in a <c>Loadout</c>'s <c>Modifiers</c>, so a
/// test that passes here is a test that agrees with the game rather than with the table it is
/// reading. The whole corpus is replayed through this same code by
/// <c>spike/BuildGaugeProbe</c> — 374 comparisons, 97.6% exact — and these are the cases worth
/// pinning in CI, which has no corpus.
/// </para>
/// </summary>
public class APlannedRollIsModelledFromTheShippedTableTests
{
    private const string Drive = "int_hyperdrive_size5_class5";

    private const string Plant = "int_powerplant_size6_class5";

    private static double? Apply(
        string module, double stock, string attribute, string? blueprint, int grade, string? experimental = null) =>
        RollModel.Apply(stock, attribute, EliteSpecifications.Module(module), blueprint, grade, experimental);

    [Fact]
    public void AGradeFiveIncreasedRangeLandsWhereEliteReportedIt()
    {
        // 1,050 t of optimal mass on a 5A drive, +55% at grade 5. Elite writes 1,627.5.
        Assert.Equal(1627.5, Apply(Drive, 1050, "Optimal Mass", "FSD_LongRange", 5)!.Value, 3);

        // And the cost of it, on the same row: power draw up 15%.
        Assert.Equal(0.69, Apply(Drive, 0.6, "Power Draw", "FSD_LongRange", 5)!.Value, 3);
    }

    [Fact]
    public void EachGradeIsItsOwnRowRatherThanAFractionOfTheTop()
    {
        // The percentages are per grade in the table, so a grade 3 is not three fifths of a grade
        // 5 and must not be modelled as one.
        Assert.Equal(1417.5, Apply(Drive, 1050, "Optimal Mass", "FSD_LongRange", 3)!.Value, 3);
        Assert.Equal(1207.5, Apply(Drive, 1050, "Optimal Mass", "FSD_LongRange", 1)!.Value, 3);
    }

    [Fact]
    public void ABlueprintAndAnExperimentalCompoundRatherThanAdd()
    {
        // **This is what the table does not state and the corpus settles.** Mass Manager on a
        // grade 5 Increased Range: the blueprint puts optimal mass up 55% and the experimental
        // puts it up a further 4%. Compounded that is 1,692.6; added it would be 1,669.5, and
        // every one of the 27 corpus modules carrying both agrees with the first.
        var both = Apply(Drive, 1050, "Optimal Mass", "FSD_LongRange", 5, "special_fsd_heavy");

        Assert.NotNull(both);
        Assert.Equal(1050 * 1.55 * 1.04, both.Value, 3);
        Assert.NotEqual(1050 * (1 + 0.55 + 0.04), both.Value, 3);
    }

    [Fact]
    public void AnExperimentalOnItsOwnStillApplies()
    {
        // A plan can name an effect and no blueprint, and the effect is a real change on its own.
        Assert.Equal(1092, Apply(Drive, 1050, "Optimal Mass", null, 0, "special_fsd_heavy")!.Value, 3);
    }

    [Fact]
    public void APlantsOutputIsModelledFromItsOwnRow()
    {
        // The other half of the power gauge: a planned Overcharged roll moves the denominator as
        // well as the numerators. Overcharged at grade 5 is +40% generation.
        Assert.Equal(28, Apply(Plant, 20, "Power Generation", "PowerPlant_Boosted", 5)!.Value, 3);
    }

    [Fact]
    public void ARecipeTheModuleCannotTakeChangesNothing()
    {
        // Narrowed by module, so a name that exists on fifteen kinds cannot model an armour roll
        // onto a drive. Unchanged rather than refused: the module is still there and still weighs
        // what it weighs.
        Assert.Equal(1050, Apply(Drive, 1050, "Optimal Mass", "Armour_Advanced", 5)!.Value, 3);
    }

    [Fact]
    public void AnAttributeTheRecipeDoesNotTouchIsLeftAlone()
    {
        // Increased Range says nothing about a distributor draw, and "says nothing" means no
        // change rather than an unknown.
        Assert.Equal(0.6, Apply(Drive, 0.6, "Distributor Draw", "FSD_LongRange", 5)!.Value, 3);
    }

    [Fact]
    public void NothingIsModelledFromAFigureThatIsMissing()
    {
        Assert.Null(RollModel.Apply(
            null, "Optimal Mass", EliteSpecifications.Module(Drive), "FSD_LongRange", 5, null));
    }

    [Fact]
    public void TheOverchargedDriveIsCostedFromTheOrdinaryDrivesRecipe()
    {
        // **The gap that mattered most** (Phase 38, item 10). EDSY files the SCO drive
        // under its own module type and EDEngineer has one "Frame Shift Drive", so every drive
        // recipe landed on the ordinary drive and an SCO drive — which is what almost every
        // Commander now flies — was offered eight blueprints d47 held no recipe for.
        //
        // The corpus is what settles that they are the same recipe rather than similar ones: SCO
        // drives carrying FSD_LongRange report Modifiers the ordinary drive's grade rows reproduce
        // exactly. 1,175 t of optimal mass, +55%.
        var sco = EliteSpecifications.Module("int_hyperdrive_overcharge_size5_class5");

        Assert.NotNull(sco);
        Assert.NotEmpty(BlueprintCatalogue.For(sco)!);

        Assert.Equal(
            1821.25,
            RollModel.Apply(1175, "Optimal Mass", sco, "FSD_LongRange", 5, null)!.Value,
            3);
    }
}
