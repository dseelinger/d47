using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// A line about a slot with nothing in it still says what it is about (asked for 2026-08-24).
/// <para>
/// Reported against the Commander's own Type-10, two lines of it:
/// <em>"Grade 5 Heavy Duty on Utility Mount 8"</em> and <em>"Grade 5 Heavy Duty Hull Reinforcement
/// on Compartment 4 (size 5)"</em> — <em>"Utility Mount 8 and Compartment 4 don't tell me the
/// module type. It should always be Module Type, not location within the group type."</em>
/// </para>
/// <para>
/// <b>d47 knew, and the sentence never asked.</b> The ship plan stores the module beside the
/// blueprint — <c>"module": "Shield Booster"</c>, <c>"module": "Hull Reinforcement Package"</c> —
/// and <see cref="ChecklistWording"/> resolved a slot by asking the <em>ship</em> what was fitted,
/// falling back to the mounting point when the answer was nothing. Both of those slots were empty,
/// so both fell back.
/// </para>
/// </summary>
public class AnEmptySlotStillNamesItsModuleTests
{
    /// <summary>
    /// The Commander's Oxen, with the two slots from the report empty — which is the whole point:
    /// there is nothing in them for the old path to have found.
    /// </summary>
    private const string Oxen =
        """
        { "timestamp":"2026-08-24T10:00:00Z", "event":"Loadout", "Ship":"type9_military",
          "Ship_Localised":"Type-10 Defender", "ShipID":77, "ShipName":"Oxen", "ShipIdent":"OX-01",
          "Modules":[
            { "Slot":"PowerPlant", "Item":"int_powerplant_size8_class5", "On":true } ]
        }
        """;

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-24T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     Oxen,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistItem Item(ChecklistIntent intent, string text) => new()
    {
        Key = ChecklistKeys.For(intent),
        Scope = ChecklistScope.Ship(77),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = text,
        Intent = intent,
        Hull = "type9_military",
    };

    /// <summary>The first line from the report, as it now reads.</summary>
    [Fact]
    public void AUtilityMountSaysWhatIsGoingOnIt()
    {
        var said = ChecklistWording.Said(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "TinyHardpoint8")
                {
                    Detail = "Heavy Duty",
                    Grade = 5,
                    Module = "Shield Booster",
                },
                "Grade 5 Heavy Duty on TinyHardpoint8"),
            State());

        Assert.Equal("Grade 5 Heavy Duty on Shield Booster", said);
        Assert.DoesNotContain("Utility Mount", said, StringComparison.Ordinal);
    }

    /// <summary>And the second.</summary>
    [Fact]
    public void ACompartmentSaysWhatIsGoingInIt()
    {
        var said = ChecklistWording.Said(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot04_Size5")
                {
                    Detail = "Heavy Duty Hull Reinforcement",
                    Grade = 5,
                    Module = "Hull Reinforcement Package",
                },
                "Grade 5 Heavy Duty Hull Reinforcement on Slot04_Size5"),
            State());

        Assert.Equal("Grade 5 Heavy Duty Hull Reinforcement on Hull Reinforcement Package", said);
        Assert.DoesNotContain("Compartment", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>What is fitted still wins.</b> A real module is more exact than a plan's name for one —
    /// "7A Shield Generator" says the size and the rating, which is what the Commander is actually
    /// looking at — so the plan's word is the fallback rather than the answer.
    /// </summary>
    [Fact]
    public void AFittedModuleBeatsThePlansNameForIt()
    {
        var said = ChecklistWording.Said(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "PowerPlant")
                {
                    Detail = "Overcharged",
                    Grade = 5,
                    Module = "Power Plant",
                },
                "Grade 5 Overcharged on PowerPlant"),
            State());

        Assert.Contains("8A Power Plant", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And where the plan does not say either — engineering asked for on a slot with nothing chosen
    /// for it — the mounting point is all there is, and it is still better than the journal's
    /// spelling.
    /// </summary>
    [Fact]
    public void WithNoModuleChosenTheMountingPointIsAllThereIs()
    {
        var said = ChecklistWording.Said(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "TinyHardpoint8")
                {
                    Detail = "Heavy Duty",
                    Grade = 5,
                },
                "Grade 5 Heavy Duty on TinyHardpoint8"),
            State());

        Assert.Equal("Grade 5 Heavy Duty on Utility Mount 8", said);
    }

    /// <summary>
    /// <b>The module reaches the line from the plan the Commander actually wrote.</b> Asserted
    /// through <see cref="SlotPlan.ToRequest"/> and <see cref="EngineeringPlan.Items"/>, because
    /// the field existed on the plan all along and the bug was every step between there and the
    /// sentence.
    /// </summary>
    [Fact]
    public void ThePlansModuleSurvivesTheTripToTheItem()
    {
        var plan = new SlotPlan(
            "TinyHardpoint8",
            Blueprint: "Heavy Duty",
            Grade: 5,
            Experimental: "Super Capacitor",
            Module: "Shield Booster");

        var items = EngineeringPlan.Items(
            ChecklistScope.Ship(77), "type9_military", [plan.ToRequest()]);

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal("Shield Booster", item.Intent?.Module));
    }

    /// <summary>
    /// <b>And it stays out of the key.</b> The slot alone is the identity of a slot-shaped intent
    /// (Phase 26), so changing which module is meant for a slot is the Commander changing
    /// their mind — not the item being abandoned and a new one appearing, which is how a fortnight
    /// of progress would read as thrown away.
    /// </summary>
    [Fact]
    public void ChangingTheModuleIsNotANewItem()
    {
        var booster = new ChecklistIntent(ChecklistIntentKind.Blueprint, "TinyHardpoint8")
        {
            Detail = "Heavy Duty",
            Grade = 5,
            Module = "Shield Booster",
        };

        var other = booster with { Module = "Chaff Launcher" };

        Assert.Equal(ChecklistKeys.For(booster), ChecklistKeys.For(other));
    }
}
