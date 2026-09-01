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
/// <b>Closed 2026-09-01, and the file keeps its name because the shape it tests is still the
/// disagreement between an offer list and a recipe list.</b> Every ordinary road was checked
/// again first and every one was empty: FDevIDs has no row for any of the three materials by
/// name or by symbol, EDEngineer has no Guardian blueprint symbol at all, coriolis has no
/// <c>GuardianModule_Sturdy</c> (its <c>Weapon_Sturdy</c> is Sturdy Mount, a different
/// blueprint), and 941 journals across three Commanders contain no occurrence of any spelling.
/// </para>
/// <para>
/// <b>What broke the deadlock was a second tracker.</b> ED Odyssey Materials Helper's
/// <c>locale/material/horizons/manufactured.csv</c> (MIT) names all three symbols exactly as
/// EDSY does, independently, and it is the key EDOMH counts a journal inventory by — so a wrong
/// one would show a permanent zero to every user who gathered one. On the Commander's ruling —
/// <i>"go with what EDSY and EDOMH agree on"</i> — the intersection ships and the disagreement
/// does not: both name two Hardened Surface Fragments and one Caustic Crystal, EDSY alone adds a
/// Tactical Core Chip, and that third ingredient is left out. The warrant per column lives in
/// <c>tools/curated_materials.py</c>; the recipe's lives in <c>tools/gen-blueprints.py</c>.
/// </para>
/// <para>
/// <b>The risk it takes is understating</b>, and it is written down rather than buried: if EDSY
/// is right about the third ingredient, a Commander gathers what d47 asks for and cannot roll.
/// That is the first thing to check if the blueprint ever refuses.
/// </para>
/// </summary>
public class OfferedButNotCostedTests
{
    private static string? TypeOf(string symbol) => EliteSpecifications.Module(symbol)?.Type;

    [Fact]
    public void TheGaussCannonIsOfferedEngineeringAndOneOfItIsNowCosted()
    {
        var type = TypeOf("hpt_guardian_gausscannon_fixed_medium");

        Assert.Equal("hexgg", type);

        // EDSY says two blueprints are offered to it.
        var offered = BlueprintCatalogue.OfferedTo(type);

        Assert.NotNull(offered);
        Assert.Contains("GuardianModule_Sturdy", offered);
        Assert.Contains("Weapon_RapidFire", offered);

        // The reported line, and it draws now: the page said "no engineering" about a module
        // whose one blueprint the Commander was standing in front of.
        var costed = Assert.Single(
            BlueprintCatalogue.For(EliteSpecifications.Module("hpt_guardian_gausscannon_fixed_medium"))!);

        Assert.Equal("Anti-Guardian Zone Resistance", costed.Name);
        Assert.Equal("Ram Tah", Assert.Single(costed.Engineers));

        Assert.Equal(
            [("tg_abrasion03", 2), ("tg_causticcrystal", 1)],
            costed.Ingredients.Select(item => (item.Symbol, item.Size)));

        // And Rapid Fire is still uncosted, which is the honest half staying honest: EDEngineer
        // has no Guardian weapon recipes and nothing has changed about that.
        Assert.DoesNotContain(
            BlueprintCatalogue.For(EliteSpecifications.Module("hpt_guardian_gausscannon_fixed_medium"))!,
            recipe => recipe.Symbols.Contains("Weapon_RapidFire", StringComparer.OrdinalIgnoreCase));
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
        // All three, because the recipe belongs to the blueprint rather than to the weapon: a
        // fix that reached the Gauss Cannon alone would be a fix keyed on the wrong thing.
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

            var costed = Assert.Single(BlueprintCatalogue.For(module)!);

            Assert.Equal("Anti-Guardian Zone Resistance", costed.Name);
            Assert.NotEmpty(costed.Ingredients);
        }
    }

    [Fact]
    public void AntiGuardianZoneResistanceIsCostedEverywhereItIsOffered()
    {
        // Nine module types, and the recipe reaches all of them: three Guardian weapons, the
        // power plant and distributor, the FSD booster and the three reinforcement packages.
        var offered = new[] { "hexgg", "hexgp", "hexgs", "cpp", "cpd", "ifsdb", "ihrp", "imrp", "isrp" };

        var recipe = Assert.Single(
            BlueprintCatalogue.All,
            entry => entry.Symbols.Contains("GuardianModule_Sturdy", StringComparer.OrdinalIgnoreCase));

        foreach (var type in offered)
        {
            Assert.Contains("GuardianModule_Sturdy", BlueprintCatalogue.OfferedTo(type) ?? []);
            Assert.Contains(type, recipe.ModuleTypes);
        }

        // **A one-off cost, not a per-application one**, which is a claim about the arithmetic
        // rather than about what the game's menus call it. A modification is multiplied by
        // EngineeringRules.RollsFor — five crafts at rank 1 — and both sources report two
        // fragments where all 786 modification rows in this table cost exactly one of each per
        // application. A source reporting two is reporting a total, so filing it as a
        // modification would send a Commander after ten.
        Assert.Equal(BlueprintKind.Experimental, recipe.Kind);
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

        // Tactical Core Chip is nameable and is deliberately not in the recipe: EDSY lists it as
        // a third ingredient and EDOMH does not, so it falls outside what the two agree on.
        Assert.DoesNotContain(
            BlueprintCatalogue.All
                .Single(entry => entry.Symbols.Contains("GuardianModule_Sturdy", StringComparer.OrdinalIgnoreCase))
                .Ingredients,
            item => item.Symbol == "unknowncorechip");
    }
}
