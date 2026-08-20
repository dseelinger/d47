using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The groups Frontier allows only so many of per ship (asked for 2026-08-20).
/// <para>
/// <b>A group and a count, not a flag on a module.</b> The group is what is limited — a Standard
/// and an Advanced Docking Computer share one, and all three shield generator families share
/// another — and the limit is a number: sixteen groups allow one, and the AX and Guardian weapons
/// allow four.
/// </para>
/// <para>
/// <b>The corpus could not have found this on its own, and that is the point.</b> Measured over
/// 2,863 <c>Loadout</c> events, every limited group the corpus contains maxes out at exactly one —
/// which corroborates sixteen of the seventeen. It contains no AX or Guardian weapon at all, so
/// measuring alone would have shipped <c>hex</c> as one-per-ship and quietly hidden three quarters
/// of an anti-xeno loadout. EDSY is the authority; the corpus is the check.
/// </para>
/// </summary>
public class OneOfThatPerShipTests
{
    private static string? Group(string symbol) => EliteSpecifications.Module(symbol)?.Limit;

    [Theory]
    [InlineData("int_fuelscoop_size6_class5")]         // the reported example
    [InlineData("int_supercruiseassist")]              // and the other one
    [InlineData("int_guardianfsdbooster_size5")]       // and the third
    [InlineData("int_refinery_size4_class5")]
    [InlineData("int_fsdinterdictor_size4_class5")]
    [InlineData("int_detailedsurfacescanner_tiny")]
    public void OneOfTheseIsAllAShipMayCarry(string symbol)
    {
        var group = Group(symbol);

        Assert.NotNull(group);
        Assert.Equal(1, EliteSpecifications.MostOf(group));
    }

    [Fact]
    public void TheTwoDockingComputersShareOneLimitAndSoRuleEachOtherOut()
    {
        // The case a per-module check would miss: they are different modules with different
        // names, and fitting either means you may not fit the other.
        Assert.Equal(Group("int_dockingcomputer_standard"), Group("int_dockingcomputer_advanced"));
        Assert.Equal(1, EliteSpecifications.MostOf(Group("int_dockingcomputer_standard")));
    }

    [Fact]
    public void EveryShieldGeneratorFamilySharesOneLimit()
    {
        // Bi-Weave, Prismatic and stock are one shield generator between them.
        var stock = Group("int_shieldgenerator_size6_class5");

        Assert.NotNull(stock);
        Assert.Equal(stock, Group("int_shieldgenerator_size6_class3_fast"));
        Assert.Equal(stock, Group("int_shieldgenerator_size6_class5_strong"));
        Assert.Equal(1, EliteSpecifications.MostOf(stock));
    }

    [Fact]
    public void TheAntiXenoWeaponsAllowFourAndNotOne()
    {
        // The one the corpus could not have told us, and the reason this is a count.
        var group = Group("hpt_guardian_gausscannon_fixed_medium");

        Assert.NotNull(group);
        Assert.Equal(4, EliteSpecifications.MostOf(group));
    }

    [Fact]
    public void AnOrdinaryModuleIsNotLimitedAtAll()
    {
        // Most modules are not in a group, and a ship may carry as many as it has slots for.
        Assert.Null(Group("int_cargorack_size6_class1"));
        Assert.Null(Group("hpt_multicannon_fixed_medium"));
        Assert.Null(Group("int_hullreinforcement_size5_class2"));

        // Shield cell banks are deliberately not the shield generator's group — you may stack them.
        Assert.Null(Group("int_shieldcellbank_size5_class5"));
    }

    [Fact]
    public void AGroupNothingLimitsHasNoCount()
    {
        Assert.Null(EliteSpecifications.MostOf("not-a-group"));
        Assert.Null(EliteSpecifications.MostOf(""));
        Assert.Null(EliteSpecifications.MostOf(null));
    }
}
