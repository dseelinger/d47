using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Modules Frontier engineers and d47 holds no recipe for (reported 2026-08-20 against a Guardian
/// Gauss Cannon: <i>"it does have 1 engineering option — Anti-Guardian Zone Resistance"</i>).
/// <para>
/// <b>Two shipped tables disagree, and the disagreement is the information.</b> The offer list is
/// EDSY's — which blueprints a module <em>type</em> may take — and the recipes are EDEngineer's,
/// which is what each costs. EDEngineer carries no Guardian weapon recipes at all, so every
/// Guardian hardpoint has offers and no rows.
/// </para>
/// <para>
/// Before this, the surface read that as <i>"I have no engineering for this module"</i>, which is
/// a claim about Elite. The true claim is about d47.
/// </para>
/// </summary>
public class OfferedButNotCostedTests
{
    private static string? TypeOf(string symbol) => EliteSpecifications.Module(symbol)?.Type;

    [Fact]
    public void TheGaussCannonIsOfferedEngineeringNobodyHasCosted()
    {
        var type = TypeOf("hpt_guardian_gausscannon_fixed_medium");

        Assert.Equal("hexgg", type);

        // EDSY says two blueprints are offered to it.
        var offered = BlueprintCatalogue.OfferedTo(type);

        Assert.NotNull(offered);
        Assert.NotEmpty(offered);
        Assert.Contains("GuardianModule_Sturdy", offered);

        // And d47 holds a recipe for neither, which is why the page had nothing to draw.
        Assert.Empty(BlueprintCatalogue.For(EliteSpecifications.Module("hpt_guardian_gausscannon_fixed_medium"))!);
    }

    [Fact]
    public void AFuelTankIsADifferentAnswerEntirely()
    {
        // The state the old sentence was written for, and it is still reachable: nothing is
        // offered, so there is nothing d47 is failing to hold.
        var type = TypeOf("int_fueltank_size3_class3");

        Assert.Empty(BlueprintCatalogue.OfferedTo(type) ?? []);
        Assert.Empty(BlueprintCatalogue.For(EliteSpecifications.Module("int_fueltank_size3_class3"))!);
    }

    [Fact]
    public void AnOrdinaryWeaponIsOfferedAndCosted()
    {
        // The control. A multi-cannon has both halves, so neither refusal applies to it.
        var module = EliteSpecifications.Module("hpt_multicannon_fixed_medium");

        Assert.NotEmpty(BlueprintCatalogue.OfferedTo(module!.Type)!);
        Assert.NotEmpty(BlueprintCatalogue.For(module)!);
    }

    [Fact]
    public void EveryGuardianHardpointIsInTheSameState()
    {
        // Named rather than counted, because this is the gap a regenerated Blueprints.tsv should
        // close — and when it does, this test is what says so.
        foreach (var symbol in new[]
                 {
                     "hpt_guardian_gausscannon_fixed_medium",
                     "hpt_guardian_plasmalauncher_fixed_medium",
                     "hpt_guardian_shardcannon_fixed_medium",
                 })
        {
            var module = EliteSpecifications.Module(symbol);

            Assert.NotNull(module);
            Assert.NotEmpty(BlueprintCatalogue.OfferedTo(module.Type)!);
            Assert.Empty(BlueprintCatalogue.For(module)!);
        }
    }
}
