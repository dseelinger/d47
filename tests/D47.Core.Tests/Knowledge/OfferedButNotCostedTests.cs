using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary> Modules Frontier engineers and d47 holds no recipe for. </summary>
public class OfferedButNotCostedTests
{
    private static string? TypeOf(string symbol) => EliteSpecifications.Module(symbol)?.Type;

    [Fact]
    public void TheGaussCannonIsOfferedEngineeringNobodyHasCosted()
    {
        var type = TypeOf("hpt_guardian_gausscannon_fixed_medium");

        Assert.Equal("hexgg", type);

        // Rapid Fire, and only Rapid Fire.
        Assert.Equal(["Weapon_RapidFire"], BlueprintCatalogue.OfferedTo(type));

        // And d47 holds no recipe for that one, which is the honest gap this file is named for: EDEngineer
        // carries no Guardian weapon recipes at all.
        Assert.Empty(BlueprintCatalogue.For(EliteSpecifications.Module("hpt_guardian_gausscannon_fixed_medium"))!);
    }

    [Fact]
    public void AFuelTankIsADifferentAnswerEntirely()
    {
        // Nothing is offered, so there is nothing d47 is failing to hold.
        var type = TypeOf("int_fueltank_size3_class3");

        Assert.Empty(BlueprintCatalogue.OfferedTo(type) ?? []);
        Assert.Empty(BlueprintCatalogue.For(EliteSpecifications.Module("int_fueltank_size3_class3"))!);
    }

    [Fact]
    public void TheSuperchargedDriveIsOfferedAndNowCosted()
    {
 // **The largest gap of the family, and it was never missing data**.
        var sco = EliteSpecifications.Module("int_hyperdrive_overcharge_size5_class5");

        Assert.NotNull(sco);
        Assert.Equal("cfsdo", sco.Type);

        var offered = BlueprintCatalogue.OfferedTo(sco.Type);

        Assert.NotNull(offered);
        Assert.Contains("FSD_LongRange", offered);

        var costed = BlueprintCatalogue.For(sco);

        Assert.NotNull(costed);
        Assert.Contains(costed, recipe => recipe.Name == "Increased FSD Range" && recipe.Grade == 5);
        Assert.All(
            costed.Where(recipe => recipe.Kind == BlueprintKind.Modification),
            recipe => Assert.NotEmpty(recipe.Ingredients));
    }

    [Fact]
    public void AnOrdinaryWeaponIsOfferedAndCosted()
    {
        // The control.
        var module = EliteSpecifications.Module("hpt_multicannon_fixed_medium");

        Assert.NotEmpty(BlueprintCatalogue.OfferedTo(module!.Type)!);
        Assert.NotEmpty(BlueprintCatalogue.For(module)!);
    }

    [Fact]
    public void EveryGuardianHardpointIsInTheSameState()
    {
        // One offer each and no recipe behind it, all three the same shape — the recipe belongs to the
        // blueprint rather than to the weapon, so anything true of one is true of them.
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
            Assert.DoesNotContain("GuardianModule_Sturdy", BlueprintCatalogue.OfferedTo(module.Type)!);
            Assert.Empty(BlueprintCatalogue.For(module)!);
        }
    }

    [Fact]
    public void AntiGuardianZoneResistanceIsOfferedNowhereAndCostedNowhere()
    {
        // Withheld at the offer table, so it is absent from both halves rather than present in one — an offer
        // with no recipe would still be a claim about a blueprint whose two describers disagree.
        var everywhere = new[] { "hexgg", "hexgp", "hexgs", "cpp", "cpd", "ifsdb", "ihrp", "imrp", "isrp" };

        foreach (var type in everywhere)
        {
            Assert.DoesNotContain("GuardianModule_Sturdy", BlueprintCatalogue.OfferedTo(type) ?? []);
        }

        Assert.DoesNotContain(
            BlueprintCatalogue.All,
            recipe => recipe.Symbols.Contains("GuardianModule_Sturdy", StringComparer.OrdinalIgnoreCase));

        // The Guardian FSD Booster's only offer was this one, so nothing is offered to it now.
        Assert.Empty(BlueprintCatalogue.OfferedTo("ifsdb")!);
    }

    /// <summary>The fourth state.</summary>
    [Fact]
    public void WhatIsWithheldIsStillNameableAsWithheld()
    {
        foreach (var type in new[] { "hexgg", "hexgp", "hexgs", "cpp", "cpd", "ifsdb", "ihrp", "imrp", "isrp" })
        {
            Assert.Equal(["Anti-Guardian Zone Resistance"], BlueprintCatalogue.DisputedFor(type));
        }

        // The name a Commander reads, not the symbol a journal writes.
        Assert.DoesNotContain("GuardianModule_Sturdy", BlueprintCatalogue.DisputedFor("ifsdb"));

        // Reached the way the panel reaches it — from a fitted module rather than from a type code — because
        // a lookup that works on the code and not on the module is a sentence nobody ever sees.
        var booster = EliteSpecifications.Module("int_guardianfsdbooster_size3");

        Assert.NotNull(booster);
        Assert.Empty(BlueprintCatalogue.OfferedTo(booster.Type)!);
        Assert.Equal(["Anti-Guardian Zone Resistance"], BlueprintCatalogue.DisputedFor(booster.Type));

        // And it is nobody else's business: a module with ordinary engineering says nothing.
        Assert.Empty(BlueprintCatalogue.DisputedFor("hmc"));
        Assert.Empty(BlueprintCatalogue.DisputedFor(null));

        // Emphatically not a recipe.
        Assert.DoesNotContain(
            BlueprintCatalogue.All,
            recipe => recipe.Name == "Anti-Guardian Zone Resistance");
    }

    [Fact]
    public void ItsMaterialsAreNameableAndCappedLikeAnyOther()
    {
        // The half that was blocked longest: a recipe whose ingredients cannot be keyed cannot be costed,
        // gathered or put on a checklist.
        foreach (var (symbol, name, grade) in new[]
                 {
                     ("tg_abrasion03", "Hardened Surface Fragments", 1),
                     ("tg_causticcrystal", "Caustic Crystal", 4),
                     ("unknowncorechip", "Tactical Core Chip", 5),
                 })
        {
            var material = MaterialCatalogue.Find(symbol);

            Assert.NotNull(material);
            Assert.Equal(name, material.Name);
            Assert.Equal(MaterialLedger.Material, material.Ledger);
            Assert.Equal("Manufactured", material.Category);

            // The grade is the capacity, which is the whole reason the row has to exist rather than the name
            // being aliased onto something near it.
            Assert.Equal(grade, material.Grade);

            // And a Commander who says the name gets the same row.
            Assert.Equal(symbol, MaterialCatalogue.Find(name)?.Symbol);
        }

        // Named and nothing more.
        Assert.DoesNotContain(
            BlueprintCatalogue.All.SelectMany(recipe => recipe.Ingredients),
            item => item.Symbol is "tg_abrasion03" or "tg_causticcrystal" or "unknowncorechip");
    }
}
