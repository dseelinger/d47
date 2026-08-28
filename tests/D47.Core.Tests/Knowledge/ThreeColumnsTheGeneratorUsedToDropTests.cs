using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The three columns Phase 38 asks the generator to stop dropping, asserted against the
/// shipped table.
/// <para>
/// Each one closes an arithmetic that had a hole in it: the purchase gate that says which modules
/// a Commander cannot buy, the plant output a power budget had no denominator without, and the
/// Guardian booster bonus a jump range misses by exactly.
/// </para>
/// </summary>
public class ThreeColumnsTheGeneratorUsedToDropTests
{
    [Fact]
    public void APrismaticShieldGeneratorIsBehindAPledgeAndAStockOneIsNot()
    {
        var prismatic = EliteSpecifications.Module("int_shieldgenerator_size5_class5_strong");
        var stock = EliteSpecifications.Module("int_shieldgenerator_size5_class5");

        Assert.NotNull(prismatic);
        Assert.NotNull(stock);

        Assert.True(prismatic.NeedsPledge);
        Assert.False(stock.NeedsPledge);

        // The id is carried whole rather than parsed down to a flag: it names *which* Power, and
        // a later release that finds a source for those names should not have to regenerate the
        // table to get at it.
        Assert.StartsWith("ELITE_SPECIFIC_V_POWER_", prismatic.Entitlement, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactlyNineteenModulesNeedAPledge()
    {
        // Named rather than counted alone, because the count is the part that can be right for the
        // wrong reason. This is the list Phase 38 records, and it comes out of
        // `outfitting.csv`'s own column rather than out of anybody's memory of Powerplay.
        var gated = EliteSpecifications.Modules
            .Where(module => module.NeedsPledge)
            .Select(module => module.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(19, EliteSpecifications.Modules.Count(module => module.NeedsPledge));

        Assert.Equal(
            [
                "Advanced Plasma Accelerator",
                "Concord Cannon",
                "Cytoscrambler Burst Laser",
                "Enforcer Cannon",
                "Imperial Hammer Rail Gun",
                "Mining Lance",
                "Pacifier Frag-Cannon",
                "Pack-Hound Missile Rack",
                "Prismatic Shield Generator",
                "Pulse Disruptor Laser",
                "Retributor Beam Laser",
                "Rocket Propelled FSD Disruptor",
            ],
            gated);
    }

    [Fact]
    public void EveryPowerPlantSaysWhatItMakes()
    {
        var plants = EliteSpecifications.Modules
            .Where(module => module.Type is "cpp")
            .ToList();

        Assert.NotEmpty(plants);
        Assert.All(plants, plant => Assert.NotNull(plant.PowerCapacity));

        // A plant's own draw stays empty, which is the reason this column had to exist: the
        // generator maps draw, and for a plant the meaningful figure is output.
        Assert.Equal(30, EliteSpecifications.Module("int_powerplant_size7_class5")?.PowerCapacity);
    }

    [Fact]
    public void TheFiveGuardianBoostersCarryTheirBonus()
    {
        var boosters = EliteSpecifications.Modules
            .Where(module => module.JumpBoost is not null)
            .OrderBy(module => module.Class)
            .ToList();

        Assert.Equal(5, boosters.Count);
        Assert.Equal([4, 6, 7.75, 9.25, 10.5], boosters.Select(booster => booster.JumpBoost));
        Assert.All(boosters, booster => Assert.Equal("Guardian FSD Booster", booster.Name));
    }

    [Fact]
    public void ADriveIsToldFromAThrusterByItsFuel()
    {
        // Thrusters and shield generators carry an optimal mass too — 39 and 55 of them — so the
        // older test of "has an optimal mass" was true of 94 modules that are not drives, and the
        // specification report offered every one of them a max fuel per jump with nothing after
        // it. Only a drive has the fuel figures.
        Assert.True(EliteSpecifications.Module("int_hyperdrive_size5_class5")?.IsDrive);
        Assert.True(EliteSpecifications.Module("int_hyperdrive_overcharge_size5_class5")?.IsDrive);

        Assert.False(EliteSpecifications.Module("int_engine_size5_class5")?.IsDrive);
        Assert.False(EliteSpecifications.Module("int_shieldgenerator_size5_class5")?.IsDrive);
    }

    [Fact]
    public void ARackAndATankSayWhatTheyHold()
    {
        // The jump gauge's laden and full-tank needles are computed from these on a build d47 has
        // to model rather than read — and they also tell a 5E Cargo Rack from a 6E, which was the
        // complaint the figures bag was built to answer and which these two kinds fell through.
        Assert.Equal(32, EliteSpecifications.Module("int_cargorack_size5_class1")?.Figure("capacity"));
        Assert.Equal(32, EliteSpecifications.Module("int_fueltank_size5_class3")?.Figure("capacity"));
    }
}
