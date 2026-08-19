using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// What a module can be engineered with, asserted against the shipped tables.
/// <para>
/// Remediation 15 items 6 and 10. A Type-10's Lightweight Alloy was offered every blueprint in the
/// game — Dirty Drive Tuning, Ammo Capacity, Efficient Weapon — and a fuel tank, which cannot be
/// engineered at all, was offered the same forty. Both came from one fallback: a module whose name
/// failed to match and a module that genuinely takes nothing produced the identical empty list, so
/// the panel could not tell them apart and guessed the same way for both.
/// </para>
/// <para>
/// The join is now EDSY's <c>mtype</c>, which <see cref="EliteSpecifications"/> already carried
/// against every module, and Frontier's own blueprint names. Every hop is an id.
/// </para>
/// </summary>
public class BlueprintOfferTests
{
    private static IReadOnlyList<Blueprint>? For(string symbol) =>
        BlueprintCatalogue.For(EliteSpecifications.Module(symbol));

    private static IReadOnlyList<string> Names(string symbol) =>
        [.. (For(symbol) ?? []).Select(recipe => recipe.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void ArmourIsOfferedTheFiveArmourBlueprintsAndNotTheOtherThirtyFive()
    {
        // The reported case. Bulkhead names carry the hull — forty-eight of them have a Lightweight
        // Alloy — so `Type-10 Defender Lightweight Alloy` could never match a table keyed on plain
        // "Armour", and the miss was absorbed rather than reported.
        var offered = Names("type9_military_armour_grade1");

        Assert.Equal(
            [
                "Angled Plating", "Blast Resistant", "Deep Plating", "Heavy Duty",
                "Kinetic Resistant", "Layered Plating", "Lightweight", "Reflective Plating",
                "Thermal Resistant",
            ],
            offered);

        // Named rather than merely counted: these are the ones it used to be offered.
        Assert.DoesNotContain("Dirty Drive Tuning", offered);
        Assert.DoesNotContain("Ammo Capacity", offered);
    }

    [Fact]
    public void AFuelTankCannotBeEngineeredAtAll()
    {
        // "Fuel tanks can't be engineered" — and d47's data always agreed. Zero blueprint rows of
        // any kind mention a fuel tank; it was the panel that offered forty. Empty is the answer,
        // and it is a different answer from null.
        var offered = For("int_fueltank_size3_class3");

        Assert.NotNull(offered);
        Assert.Empty(offered);
    }

    [Fact]
    public void AModuleTypeNobodyKnowsSaysSoRatherThanSayingNone()
    {
        // The third state, and the reason the other two can be trusted. A module carrying no type
        // is a hole in the table, which is not the same claim as "this takes no engineering".
        Assert.Null(BlueprintCatalogue.OfferedTo("not-a-module-type"));
        Assert.Null(BlueprintCatalogue.OfferedTo(null));
    }

    [Fact]
    public void TheScannersKeepTheirOwnBlueprints()
    {
        // These defeated every attempt to join through coriolis: a Cargo Scanner kept coming out as
        // a Chaff Launcher, because the two share the generic Lightweight and Reinforced rolls and
        // nothing else distinguished them. They are real modules with real engineering.
        Assert.Equal(
            ["Fast Scanner", "Lightweight", "Long Range Scanner", "Reinforced", "Shielded", "Wide Angle Scanner"],
            Names("hpt_cargoscanner_size0_class1"));

        Assert.Contains("Fast Scanner", Names("hpt_cloudscanner_size0_class1"));
        Assert.Contains("Wide Angle Scanner", Names("hpt_crimescanner_size0_class1"));

        // One blueprint, and the two sources share no word for it: EDSY calls it "Expanded Radius"
        // and EDEngineer "Expanded Probe Scanning Radius".
        Assert.Equal(["Expanded Probe Scanning Radius"], Names("int_detailedsurfacescanner_tiny"));
    }

    [Fact]
    public void AVariantIsOfferedWhatItsFamilyIsOffered()
    {
        // Bi-Weave and Prismatic are shield generators with their own names, so the name join
        // missed them exactly as it missed armour. Their module type is the same.
        var plain = Names("int_shieldgenerator_size4_class5");

        Assert.NotEmpty(plain);
        Assert.Equal(plain, Names("int_shieldgenerator_size4_class3_fast"));
        Assert.Contains("Thermal Resistant Shields", plain);
    }

    [Fact]
    public void ACollectorLimpetControllerIsNotOfferedTheArmourRecipe()
    {
        // coriolis files one recipe under several names, so the armour Lightweight roll answered to
        // seven of them — six belonging to limpet controllers and weapons. Unnarrowed, this module
        // matched the armour row, which is this item's own defect arriving by a side door.
        var offered = For("int_dronecontrol_collection_size1_class1");

        Assert.NotNull(offered);
        Assert.All(offered, recipe => Assert.Equal("Collector Limpet Controller", recipe.Module));
    }

    [Fact]
    public void TheJournalsOwnBlueprintNameIsReadBackAsAName()
    {
        // Remediation 15 item 10, and the string in the reported screenshot. `CannotConfirm` told
        // the Commander outright that nothing d47 shipped joined the two spellings; it does now.
        Assert.Equal("System Focused", BlueprintCatalogue.NameOf("PowerDistributor_PrioritySystems"));
        Assert.Equal("Dirty Drive Tuning", BlueprintCatalogue.NameOf("Engine_Dirty"));
        Assert.Equal("Lightweight", BlueprintCatalogue.NameOf("Armour_Advanced"));

        // A symbol nothing knows answers null rather than a prettified guess.
        Assert.Null(BlueprintCatalogue.NameOf("Not_A_Blueprint"));
        Assert.Null(BlueprintCatalogue.NameOf(null));
    }

    [Fact]
    public void EveryRecipeCarriesFrontiersNameForIt()
    {
        // The column item 10 rests on. A modification or experimental without one is a roll the
        // journal can name and d47 cannot, which is where the whole thread started.
        var nameless = BlueprintCatalogue.All
            .Where(recipe => recipe.Kind is BlueprintKind.Modification or BlueprintKind.Experimental)
            .Where(recipe => recipe.Symbols.Count == 0)
            .Select(recipe => $"{recipe.Module} / {recipe.Name}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(nameless);
    }
}
