using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The slot layout, against the shipped table (remediation.md 12, items 1, 2, 3 and 6).
/// <para>
/// <b>The expectations here were measured, not recalled.</b> Every slot name asserted below was
/// read out of what Frontier actually wrote across 915 real journals — the union of every
/// <c>Loadout</c> event for that hull — and the derivation was accepted only once every one of
/// them was accounted for. That matters most for the irregular hulls: an Anaconda's compartments
/// stop at <c>Slot10</c> and resume at <c>Slot13</c>, a Type-9's begin at <c>Slot00</c>, a
/// Vulture has no <c>Slot04</c> and a Type-8 no <c>SmallHardpoint3</c>. Those gaps are Frontier's
/// own; nothing computes them from a list of sizes, and every rule that tries gets four hulls
/// wrong.
/// </para>
/// <para>
/// So these are the regression tests for a stale source. If EDSY changes shape, or a hull is
/// rebalanced and the table is rebuilt, this is what fails rather than a Commander finding a plan
/// keyed to a slot their ship does not have.
/// </para>
/// </summary>
public class SlotLayoutTests
{
    [Fact]
    public void EveryHullTheTableKnowsHasSlots()
    {
        foreach (var ship in EliteSpecifications.Ships)
        {
            var slots = EliteSpecifications.Slots(ship.Symbol);

            Assert.True(slots.Count > 0, $"{ship.Symbol} has no slots");

            // Eight core internals, always: armour and the seven a ship cannot fly without.
            Assert.Equal(8, slots.Count(slot => slot.Kind == ShipSlotKind.Core));
        }
    }

    [Fact]
    public void ATinyHardpointIsAUtilityMount()
    {
        // The journal's name for it says hardpoint and the outfitting screen's does not, and the
        // screen's is the one the Commander is holding in their head (remediation.md 12, item 6).
        var tiny = EliteSpecifications.Slots("anaconda")
            .Where(slot => slot.Name.StartsWith("TinyHardpoint", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(8, tiny.Count);
        Assert.All(tiny, slot => Assert.Equal(ShipSlotKind.Utility, slot.Kind));
        Assert.All(tiny, slot => Assert.Equal(0, slot.Size));
    }

    [Fact]
    public void NothingCosmeticIsInTheTable()
    {
        // The exclusion is the table's shape rather than a filter anybody maintains: a paint job
        // is not a thing you outfit, so it is not a slot here (remediation.md 12, item 2).
        string[] absent =
        [
            "PaintJob", "Decal1", "Decal2", "Decal3", "VesselVoice", "ShipCockpit", "CargoHatch",
            "StringLights", "WeaponColour", "EngineColour", "Bobble01", "ShipKitWings",
            "ShipKitBumper", "ShipKitSpoiler", "ShipKitTail", "ShipName0", "ShipID0",
            "PlanetaryApproachSuite",
        ];

        foreach (var ship in EliteSpecifications.Ships)
        {
            var names = EliteSpecifications.Slots(ship.Symbol).Select(slot => slot.Name).ToHashSet();

            foreach (var cosmetic in absent)
            {
                Assert.DoesNotContain(cosmetic, names);
            }
        }
    }

    [Theory]

    // The four hulls whose numbering no rule derives. Each pair is a slot the journal writes and
    // a number beside it that a naive sequence would have produced instead.
    [InlineData("anaconda", "Slot13_Size2", "Slot11_Size2")]
    [InlineData("anaconda", "Slot14_Size1", "Slot12_Size1")]
    [InlineData("type9", "Slot00_Size8", "Slot12_Size8")]
    [InlineData("vulture", "Slot08_Size1", "Slot04_Size1")]
    [InlineData("type8", "SmallHardpoint6", "SmallHardpoint3")]
    public void TheIrregularHullsAreSpeltTheWayFrontierSpellsThem(
        string hull, string real, string invented)
    {
        var names = EliteSpecifications.Slots(hull).Select(slot => slot.Name).ToHashSet();

        Assert.Contains(real, names);
        Assert.DoesNotContain(invented, names);
    }

    [Fact]
    public void AnacondasCompartmentsAreTheTwelveTheGameGivesIt()
    {
        var compartments = EliteSpecifications.Slots("anaconda")
            .Where(slot => slot.Kind == ShipSlotKind.Optional && slot.Restrict.Count == 0)
            .Select(slot => slot.Name)
            .ToList();

        Assert.Equal(
            [
                "Slot01_Size7", "Slot02_Size6", "Slot03_Size6", "Slot04_Size6", "Slot05_Size5",
                "Slot06_Size5", "Slot07_Size5", "Slot08_Size4", "Slot09_Size4", "Slot10_Size4",
                "Slot13_Size2", "Slot14_Size1",
            ],
            compartments);
    }

    [Fact]
    public void ARestrictedCompartmentSaysWhatItTakes()
    {
        // A Panther's two cargo holds, a Prospector's limpet bay and hangar, and the military
        // compartment every combat hull carries. All four are optional internals in the
        // outfitting screen, which is why they are that kind here with a restriction rather than
        // a kind of their own.
        var cargo = EliteSpecifications.Slot("panthermkii", "Cargo01");
        var limpets = EliteSpecifications.Slot("lakonminer", "LimpetController01");
        var hangar = EliteSpecifications.Slot("lakonminer", "FighterBay01");
        var military = EliteSpecifications.Slot("anaconda", "Military01");

        Assert.NotNull(cargo);
        Assert.NotNull(limpets);
        Assert.NotNull(hangar);
        Assert.NotNull(military);

        Assert.All(
            new[] { cargo, limpets, hangar, military },
            slot =>
            {
                Assert.Equal(ShipSlotKind.Optional, slot.Kind);
                Assert.NotEmpty(slot.Restrict);
            });

        Assert.Contains("Cargo Rack", EliteSpecifications.ModulesFor(cargo).Select(m => m.Name));
        Assert.DoesNotContain(
            "Shield Generator", EliteSpecifications.ModulesFor(cargo).Select(m => m.Name));

        Assert.Contains(
            "Fighter Hangar", EliteSpecifications.ModulesFor(hangar).Select(m => m.Name));
    }

    [Fact]
    public void AMilitaryCompartmentTakesReinforcementAndNothingElse()
    {
        var military = EliteSpecifications.Slot("anaconda", "Military01");

        Assert.NotNull(military);

        var offered = EliteSpecifications.ModulesFor(military).Select(module => module.Name).ToList();

        Assert.Contains("Hull Reinforcement Package", offered);
        Assert.Contains("Shield Cell Bank", offered);
        Assert.DoesNotContain("Cargo Rack", offered);
        Assert.DoesNotContain("Fuel Scoop", offered);
    }

    [Fact]
    public void AModuleTooBigForTheSlotIsNotOffered()
    {
        var small = EliteSpecifications.Slot("anaconda", "SmallHardpoint1");

        Assert.NotNull(small);
        Assert.All(EliteSpecifications.ModulesFor(small), module => Assert.True(module.Class <= 1));

        // And the slot above it is genuinely wider, or the rule is untested.
        var huge = EliteSpecifications.Slot("anaconda", "HugeHardpoint1");

        Assert.NotNull(huge);
        Assert.Contains(EliteSpecifications.ModulesFor(huge), module => module.Class == 4);
    }

    [Fact]
    public void LifeSupportAndSensorsHaveToFillTheirSlotExactly()
    {
        // The two core slots that refuse an undersized module. Getting this wrong offers a build
        // the outfitting screen will not sell.
        foreach (var name in new[] { "LifeSupport", "Radar" })
        {
            var slot = EliteSpecifications.Slot("anaconda", name);

            Assert.NotNull(slot);
            Assert.NotEmpty(EliteSpecifications.ModulesFor(slot));
            Assert.All(
                EliteSpecifications.ModulesFor(slot),
                module => Assert.Equal(slot.Size, module.Class));
        }

        // Where a power plant, in the same ship, is free to be smaller than its socket.
        var plant = EliteSpecifications.Slot("anaconda", "PowerPlant");

        Assert.NotNull(plant);
        Assert.Contains(EliteSpecifications.ModulesFor(plant), module => module.Class < plant.Size);
    }

    [Fact]
    public void AnScoDriveWillNotSitInACompartmentLargerThanItself()
    {
        var drive = EliteSpecifications.Slot("anaconda", "FrameShiftDrive");

        Assert.NotNull(drive);

        var offered = EliteSpecifications.ModulesFor(drive).ToList();

        // The ordinary drive may be undersized; the supercruise-assist one may not, and it says
        // so on the module rather than in a rule about drives.
        Assert.Contains(offered, module => module.Class < drive.Size && !module.MustFillSlot);
        Assert.DoesNotContain(offered, module => module.Class < drive.Size && module.MustFillSlot);
    }

    [Fact]
    public void ArmourIsOfferedForTheHullItBelongsTo()
    {
        var anaconda = EliteSpecifications.Slot("anaconda", "Armour");
        var vulture = EliteSpecifications.Slot("vulture", "Armour");

        Assert.NotNull(anaconda);
        Assert.NotNull(vulture);

        var offered = EliteSpecifications.ModulesFor(anaconda).Select(module => module.Symbol).ToList();

        Assert.Contains("anaconda_armour_grade1", offered);
        Assert.DoesNotContain("vulture_armour_grade1", offered);

        // Five grades each, and the Vulture gets its own five rather than none.
        Assert.Equal(5, offered.Count);
        Assert.Equal(5, EliteSpecifications.ModulesFor(vulture).Count);
    }

    [Fact]
    public void AModuleRestrictedToSomeHullsIsNotOfferedOnTheOthers()
    {
        // A Sidewinder has compartments and cannot carry a fighter; the Prospector's hangar bay
        // can. Without the restriction the picker would offer one on every ship with a size 5.
        var sidewinder = EliteSpecifications.Slots("sidewinder")
            .Where(slot => slot.Kind == ShipSlotKind.Optional)
            .SelectMany(EliteSpecifications.ModulesFor)
            .Select(module => module.Name)
            .ToHashSet();

        Assert.DoesNotContain("Fighter Hangar", sidewinder);
        Assert.Contains("Cargo Rack", sidewinder);
    }

    [Fact]
    public void ASlotSaysItselfInWordsRatherThanInTheJournalsSpelling()
    {
        Assert.Equal("Main Engines", EliteSpecifications.Slot("anaconda", "MainEngines")?.Describe());
        Assert.Equal(
            "Large Hardpoint 1", EliteSpecifications.Slot("anaconda", "LargeHardpoint1")?.Describe());
        Assert.Equal(
            "Utility Mount 3", EliteSpecifications.Slot("anaconda", "TinyHardpoint3")?.Describe());
        Assert.Equal(
            "Compartment 1 (size 7)", EliteSpecifications.Slot("anaconda", "Slot01_Size7")?.Describe());
        Assert.Equal(
            "Military 1 (size 5)", EliteSpecifications.Slot("anaconda", "Military01")?.Describe());
    }
}
