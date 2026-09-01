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
/// <b>Re-measured 2026-09-01 against all four sources, and it is still blocked</b>
/// (<a href="https://github.com/dseelinger/d47/issues/127">#127</a>). Two things this file used to
/// say were wrong, and both mattered to whether the block could be lifted.
/// </para>
/// <para>
/// <b>EDSY does not mark the materials as guesses.</b> Its rows read
/// <c>hasufr : { name:'Hardened Surface Fragments', … fdname:'TG_Abrasion03' }, // TODO:
/// matgrp,fdid</c> — the marker is on the material <em>group</em> and the numeric id, not on the
/// symbol. So EDSY does assert a Frontier symbol for each of the three, and the block is that
/// <b>nothing corroborates it</b>: FDevIDs has no such row by name or by symbol, EDEngineer's
/// <c>entryData.json</c> has never heard of them, coriolis has no <c>GuardianModule_Sturdy</c> at
/// all (its <c>Weapon_Sturdy</c> is Sturdy Mount, a different blueprint), and 941 journals across
/// three Commanders contain no occurrence of any spelling. One source saying it is a lead.
/// </para>
/// <para>
/// <b>And EDSY's quantities cannot be read even if the symbols were taken on trust.</b> Its entry
/// is <c>misc_agzr : { name:'Anti-Guardian Zone Resistance', maxgrade:1, mats:[ {hasufr:2},
/// {cacr:1}, {tacoch:1} ], fdname:'GuardianModule_Sturdy' }, // TODO: fdname</c>. <c>mats</c> is
/// one group per grade everywhere in that file — of the 65 entries carrying both fields,
/// <b>this is the only one where the counts disagree</b>, and of the four ungraded ones the other
/// three have exactly one group. So either it is three grades and <c>maxgrade</c> is wrong, or it
/// is one grade costing all three materials and the shape is wrong, and the difference is what a
/// Commander would go and gather.
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

    [Fact]
    public void AntiGuardianZoneResistanceIsNamedNowhereD47Reads()
    {
        // **Why the other half of item 10 did not ship, asserted rather than written down.**
        // Every module EDSY offers `GuardianModule_Sturdy` to has no recipe for it, and the
        // reason is that the recipe exists in none of d47's three sources — so shipping one
        // would mean writing Frontier's game data out of somebody's memory of it, which is the
        // thing the guardrails exist to prevent.
        var offered = new[] { "hexgg", "hexgp", "hexgs", "cpp", "cpd", "ifsdb", "ihrp", "imrp", "isrp" };

        foreach (var type in offered)
        {
            Assert.Contains("GuardianModule_Sturdy", BlueprintCatalogue.OfferedTo(type) ?? []);
        }

        Assert.DoesNotContain(
            BlueprintCatalogue.All,
            recipe => recipe.Symbols.Contains("GuardianModule_Sturdy", StringComparer.OrdinalIgnoreCase));

        // And the materials EDSY names for it are in no naming source either, which is the second
        // half of the same problem: a recipe whose ingredients cannot be keyed cannot be costed,
        // gathered or put on a checklist.
        foreach (var material in new[] { "Hardened Surface Fragments", "Caustic Crystal", "Tactical Core Chip" })
        {
            Assert.Empty(MaterialCatalogue.Near(material));
        }
    }

    /// <summary>
    /// <b>The tripwire</b> (#127, 2026-09-01). Everything above asserts that d47 is honest about a
    /// gap; this asserts the gap is still <em>there</em>, by the symbols EDSY names rather than by
    /// the display names above — because those are what a regenerated <c>Materials.tsv</c> would
    /// carry the day FDevIDs gains the rows.
    /// <para>
    /// Written to fail on good news. The block is upstream and nobody here can lift it, so the
    /// alternative to this test is somebody remembering to go and look; a suite that runs on every
    /// push does not forget. When it fails, the recipe can be built and the issue can close.
    /// </para>
    /// </summary>
    [Fact]
    public void WhenTheseThreeSymbolsResolveThisIssueCanBeBuilt()
    {
        foreach (var symbol in new[] { "TG_Abrasion03", "TG_CausticCrystal", "UnknownCoreChip" })
        {
            Assert.True(
                MaterialCatalogue.Find(symbol) is null,
                $"Materials.tsv now names {symbol}. That was the block on "
                + "https://github.com/dseelinger/d47/issues/127: Anti-Guardian Zone Resistance's "
                + "ingredients can be keyed now, so the recipe can be costed and this test's whole "
                + "family is ready to be inverted. EDSY's quantities still need settling first — "
                + "its maxgrade and its mats count disagree, and it is the only entry in that file "
                + "where they do.");
        }
    }
}
