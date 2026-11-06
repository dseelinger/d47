using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>Combat figures for a plan: resistances, boosts, damage and the hold.</summary>
public class PlannedCombatFiguresTests
{
    private static CombatGauge Read(params SlotPlan[] plans)
    {
        var build = new ShipBuild("F1", "ship-1", "python", 1, "Plan", plans);
        var combat = ShipGauges.Read(build, seen: null).Combat;

        Assert.NotNull(combat);

        return combat;
    }

    [Fact]
    public void APlannedShieldRollMovesEachResistanceInTheDamageStillTaken()
    {
        var combat = Read(new SlotPlan("Slot04_Size5", "Reinforced Shields", 5)
        {
            Variant = "int_shieldgenerator_size5_class5",
        });

        // Reinforced Shields grade 5 lists All Resistances +17% and Thermal Resistance -9%; the game applies
        // the first to each type, as the journals' engineered loadouts show.
        Assert.Equal(1 - 0.6 * 0.83, combat.ShieldResistance!.Kinetic, 6);
        Assert.Equal(1 - 1.2 * 0.83, combat.ShieldResistance.Thermal, 6);
        Assert.Equal(1 - 0.5 * 0.83, combat.ShieldResistance.Explosive, 6);
        Assert.Equal(FigureKind.Modelled, combat.Kind);
    }

    [Fact]
    public void APlannedBoosterAddsItsRollToOnePlusItsStockBoost()
    {
        var plain = Read(
            new SlotPlan("Slot04_Size5") { Variant = "int_shieldgenerator_size5_class5" },
            new SlotPlan("TinyHardpoint1") { Variant = "hpt_shieldbooster_size0_class5" });

        var heavy = Read(
            new SlotPlan("Slot04_Size5") { Variant = "int_shieldgenerator_size5_class5" },
            new SlotPlan("TinyHardpoint1", "Heavy Duty", 5) { Variant = "hpt_shieldbooster_size0_class5" });

        // 0.2 stock, +38% on 1.2.
        Assert.Equal(1.2 * 1.38 / 1.2, heavy.Shields!.Value / plain.Shields!.Value, 6);
    }

    [Fact]
    public void WeaponsThatDrawMoreThanTheDistributorRechargesAreScaledDown()
    {
        var combat = Read(
            new SlotPlan("LargeHardpoint1") { Variant = "hpt_pulselaser_fixed_large" },
            new SlotPlan("LargeHardpoint2") { Variant = "hpt_pulselaser_fixed_large" },
            new SlotPlan("LargeHardpoint3") { Variant = "hpt_pulselaser_fixed_large" },
            new SlotPlan("MediumHardpoint1") { Variant = "hpt_pulselaser_fixed_medium" },
            new SlotPlan("PowerDistributor") { Variant = "int_powerdistributor_size3_class5" });

        // Three large and one medium: draw per shot times shots per second (the reciprocal of the fire interval).
        var draw = 3 * (0.86 / 0.33) + 0.5 / 0.29;

        Assert.Equal(3 * 18.12 + 12.07, combat.RawDps, 6);
        Assert.Equal(combat.RawDps * 2.8 / draw, combat.SustainedDps, 6);
    }

    [Fact]
    public void TheHoldIsTheRacksFittedOrPlanned()
    {
        var combat = Read(new SlotPlan("Slot01_Size6") { Variant = "int_cargorack_size5_class1" });

        Assert.Equal(32, combat.Cargo);
    }
}
