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
/// <para>
/// <b>Half of the gap closed in Phase 38, and the two halves are different kinds of thing.</b>
/// Some offers were uncosted because EDSY <em>splits a module type EDEngineer does not</em> — the
/// Supercharged drive against the ordinary one — so the recipe was on disk all along, filed under
/// a sibling name. That is a join, it is settled by the corpus rather than by a resemblance, and
/// it is closed. The rest is genuine absence: EDEngineer carries no Guardian weapon recipes at
/// all, and FDevIDs — the naming authority — has no row for any of the three materials. So that
/// half stays uncosted and stays honest.
/// </para>
/// <para>
/// <b>The Guardian half is not uncosted any more — it is withheld</b>
/// (<a href="https://github.com/dseelinger/d47/issues/127">#127</a>, ruled 2026-09-01).
/// EDEngineer has no Guardian blueprint symbol at all and coriolis has none either, so the only
/// two sources that describe Anti-Guardian Zone Resistance are EDSY and ED Odyssey Materials
/// Helper. They agree that it exists, that it is Ram Tah's, that it has one grade, and on two of
/// its three ingredients. They disagree about the third — EDSY lists a Tactical Core Chip and
/// EDOMH does not — and EDSY is malformed in exactly that spot: <c>maxgrade:1</c> against three
/// <c>mats</c> groups, the only entry of the 65 carrying both fields where the counts disagree.
/// </para>
/// <para>
/// <b>The Commander's rule:</b> <i>"If the two trackers don't agree on an engineering item,
/// remove that from d47's offered engineering."</i> So the offer goes as well as the recipe.
/// A recipe missing an ingredient is the worst of the three states available — a Commander
/// gathers exactly what d47 asks for, flies to the workshop and cannot roll — and an offer with
/// no recipe is still a claim d47 is not in a position to make about a blueprint its two
/// describers describe differently.
/// </para>
/// <para>
/// <b>The three materials stay</b>, because both trackers agree on all three symbols and they
/// are real: a Commander who gathers one has it named, graded and counted. See
/// <c>tools/curated_materials.py</c> for the warrant per column.
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

        // Rapid Fire, and only Rapid Fire. Anti-Guardian Zone Resistance is withheld at the
        // offer table itself, so nothing downstream has to know it exists.
        Assert.Equal(["Weapon_RapidFire"], BlueprintCatalogue.OfferedTo(type));

        // And d47 holds no recipe for that one, which is the honest gap this file is named for:
        // EDEngineer carries no Guardian weapon recipes at all.
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
    public void TheSuperchargedDriveIsOfferedAndNowCosted()
    {
        // **The largest gap of the family, and it was never missing data** (Phase 38,
        // item 10). EDSY files the SCO drive as its own module type; EDEngineer has one "Frame
        // Shift Drive" — so all eight of the drive's blueprints were offered to a Commander with
        // no recipe behind any of them, on the drive almost everybody now flies.
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
        // The control. A multi-cannon has both halves, so neither refusal applies to it.
        var module = EliteSpecifications.Module("hpt_multicannon_fixed_medium");

        Assert.NotEmpty(BlueprintCatalogue.OfferedTo(module!.Type)!);
        Assert.NotEmpty(BlueprintCatalogue.For(module)!);
    }

    [Fact]
    public void EveryGuardianHardpointIsInTheSameState()
    {
        // One offer each and no recipe behind it, all three the same shape — the recipe belongs
        // to the blueprint rather than to the weapon, so anything true of one is true of them.
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
        // Withheld at the offer table, so it is absent from both halves rather than present in
        // one — an offer with no recipe would still be a claim about a blueprint whose two
        // describers disagree.
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

    /// <summary>
    /// <b>The fourth state.</b> Withholding a blueprint left the Guardian FSD Booster — whose
    /// only one it is — reading as a module Frontier does not engineer, which is a claim about
    /// Elite and the kind this file exists to keep d47 out of making. So the withholding says so
    /// itself: the name, and the fact that d47 will not describe it.
    /// </summary>
    [Fact]
    public void WhatIsWithheldIsStillNameableAsWithheld()
    {
        foreach (var type in new[] { "hexgg", "hexgp", "hexgs", "cpp", "cpd", "ifsdb", "ihrp", "imrp", "isrp" })
        {
            Assert.Equal(["Anti-Guardian Zone Resistance"], BlueprintCatalogue.DisputedFor(type));
        }

        // The name a Commander reads, not the symbol a journal writes.
        Assert.DoesNotContain("GuardianModule_Sturdy", BlueprintCatalogue.DisputedFor("ifsdb"));

        // Reached the way the panel reaches it — from a fitted module rather than from a type
        // code — because a lookup that works on the code and not on the module is a sentence
        // nobody ever sees.
        var booster = EliteSpecifications.Module("int_guardianfsdbooster_size3");

        Assert.NotNull(booster);
        Assert.Empty(BlueprintCatalogue.OfferedTo(booster.Type)!);
        Assert.Equal(["Anti-Guardian Zone Resistance"], BlueprintCatalogue.DisputedFor(booster.Type));

        // And it is nobody else's business: a module with ordinary engineering says nothing.
        Assert.Empty(BlueprintCatalogue.DisputedFor("hmc"));
        Assert.Empty(BlueprintCatalogue.DisputedFor(null));

        // Emphatically not a recipe. Nothing here can be costed, planned or gathered for — the
        // fourth state is a sentence, and a Blueprint would be a promise.
        Assert.DoesNotContain(
            BlueprintCatalogue.All,
            recipe => recipe.Name == "Anti-Guardian Zone Resistance");
    }

    [Fact]
    public void ItsMaterialsAreNameableAndCappedLikeAnyOther()
    {
        // The half that was blocked longest: a recipe whose ingredients cannot be keyed cannot
        // be costed, gathered or put on a checklist. FDevIDs still names none of these — they
        // are carried by tools/curated_materials.py, on two trackers agreeing (#127).
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

            // The grade is the capacity, which is the whole reason the row has to exist rather
            // than the name being aliased onto something near it.
            Assert.Equal(grade, material.Grade);

            // And a Commander who says the name gets the same row.
            Assert.Equal(symbol, MaterialCatalogue.Find(name)?.Symbol);
        }

        // Named and nothing more. No recipe anywhere asks for any of the three, because the one
        // that would is withheld — the materials are here so a Commander who gathers one is told
        // what it is and what it counts against, which is true whatever happens to the blueprint.
        Assert.DoesNotContain(
            BlueprintCatalogue.All.SelectMany(recipe => recipe.Ingredients),
            item => item.Symbol is "tg_abrasion03" or "tg_causticcrystal" or "unknowncorechip");
    }
}
