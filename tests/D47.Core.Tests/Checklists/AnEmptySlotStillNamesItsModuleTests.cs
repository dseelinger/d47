using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A line about a slot with nothing in it still says what it is about.</summary>
public class AnEmptySlotStillNamesItsModuleTests
{
    /// <summary>The Commander's Oxen, with two slots empty.</summary>
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

    /// <summary>A utility mount says what is going on it.</summary>
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

    /// <summary>What is fitted still wins.</summary>
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
    /// And where the plan does not say either — engineering asked for on a slot with nothing chosen for
    /// it — the mounting point is all there is, and it is still better than the journal's spelling.
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

    /// <summary>The module reaches the line from the plan the Commander actually wrote.</summary>
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

    /// <summary>And it stays out of the key.</summary>
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
