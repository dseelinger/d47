using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A finished line says what was done, to which module, on which ship.</summary>
public class ALineNamesItsShipAndModuleTests
{
    private const string Flamebrand =
        """
        { "timestamp":"2026-08-21T10:00:00Z", "event":"Loadout", "Ship":"anaconda",
          "Ship_Localised":"Anaconda", "ShipID":51, "ShipName":"Flamebrand", "ShipIdent":"FB-01",
          "Modules":[
            { "Slot":"Slot01_Size7", "Item":"int_shieldgenerator_size7_class5", "On":true,
              "Engineering":{"BlueprintName":"ShieldGenerator_Reinforced","Level":5,"Quality":1.0,
                             "Engineer":"Didi Vatermann","EngineerID":300000} },
            { "Slot":"Slot02_Size6", "Item":"int_cargorack_size6_class1", "On":true } ]
        }
        """;

    private static CommanderGameState State(params string[] extra)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-21T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     Flamebrand,
                 }.Concat(extra))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistItem Item(ChecklistIntent intent, string text, int shipId = 51) => new()
    {
        Key = ChecklistKeys.For(intent),
        Scope = ChecklistScope.Ship(shipId),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = text,
        Intent = intent,
        Hull = "anaconda",
    };

    [Fact]
    public void ASlotResolvesToTheModuleSittingInIt()
    {
        var said = ChecklistWording.Said(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size7")
                {
                    Detail = "Reinforced Shields",
                    Grade = 5,
                },
                "Grade 5 Reinforced Shields on Slot01_Size7"),
            State());

        Assert.Equal("Grade 5 Reinforced Shields on 7A Shield Generator", said);
    }

    [Fact]
    public void TheWholeSentenceNamesTheShipTheCommanderNamed()
    {
        var line = ChecklistWording.Line(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size7")
                {
                    Detail = "Reinforced Shields",
                    Grade = 5,
                },
                "Grade 5 Reinforced Shields on Slot01_Size7"),
            State());

        Assert.Equal(
            "Grade 5 Reinforced Shields on 7A Shield Generator on Flamebrand (Anaconda)", line);
    }

    /// <summary>The caption under the line.</summary>
    [Fact]
    public void TheCaptionNamesTheShipRatherThanItsId()
    {
        var item = Item(
            new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size7") { Grade = 5 },
            "Grade 5 engineering on Slot01_Size7");

        Assert.Equal("ship 51", item.Scope.ToString());
        Assert.Equal("Flamebrand (Anaconda)", ChecklistWording.Where(item, State()));
    }

    [Fact]
    public void TheVerdictSpellsTheModuleTheWayTheLineDoes()
    {
        var item = Item(
            new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size7")
            {
                Detail = "Reinforced Shields",
                Grade = 5,
            },
            "Grade 5 Reinforced Shields on Slot01_Size7");

        var verdict = ChecklistEvaluator.Evaluate(item, State());

        Assert.Equal("7A Shield Generator is at grade 5 and finished.", verdict?.Reason);
        Assert.Contains("7A Shield Generator", ChecklistWording.Said(item, State()), StringComparison.Ordinal);
    }

    /// <summary>
    /// The engineer's own access is not about a slot, so nothing is substituted into it — and the ship
    /// still gets named, because the rank is wanted for a roll on that ship.
    /// </summary>
    [Fact]
    public void AnItemThatIsNotAboutASlotKeepsItsWording()
    {
        var line = ChecklistWording.Line(
            Item(
                new ChecklistIntent(ChecklistIntentKind.EngineerAccess, "Didi Vatermann") { Grade = 5 },
                "Rank 5 with Didi Vatermann"),
            State());

        Assert.Equal("Rank 5 with Didi Vatermann on Flamebrand (Anaconda)", line);
    }

    /// <summary>A slot with nothing in it is still not called <c>Slot02_Size6</c> to a Commander.</summary>
    [Fact]
    public void AnEmptySlotFallsBackToTheLayoutsNameForIt()
    {
        var said = ChecklistWording.Said(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot03_Size6") { Grade = 3 },
                "Grade 3 engineering on Slot03_Size6"),
            State());

        Assert.Equal("Grade 3 engineering on Compartment 3 (size 6)", said);
    }

    /// <summary>Nothing is invented where nothing is known.</summary>
    [Fact]
    public void AShipNeverSeenKeepsTheStoredWordingAndSaysSo()
    {
        var item = Item(
            new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size7") { Grade = 5 },
            "Grade 5 engineering on Slot01_Size7",
            shipId: 53);

        Assert.Equal("Grade 5 engineering on Slot01_Size7", ChecklistWording.Said(item, State()));
        Assert.Equal("Anaconda (ship 53)", ChecklistWording.Where(item, State()));
    }

    /// <summary>
    /// A parked ship is named from the fleet snapshot, which carries the Commander's name for one they
    /// are not sitting in.
    /// </summary>
    [Fact]
    public void AParkedShipIsNamedFromTheFleetSnapshot()
    {
        var state = State(
            """
            { "timestamp":"2026-08-21T11:00:00Z", "event":"StoredShips", "StarSystem":"Deciat",
              "StationName":"Garay Terminal",
              "ShipsHere":[ {"ShipID":53,"ShipType":"cutter","ShipType_Localised":"Imperial Cutter",
                             "Name":"Oxen Free","Value":700000000} ],
              "ShipsRemote":[] }
            """);

        var item = Item(
            new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size8") { Grade = 5 },
            "Grade 5 engineering on Slot01_Size8",
            shipId: 53);

        Assert.Equal("Oxen Free (Imperial Cutter)", ChecklistWording.Where(item, state));
    }

    /// <summary>
    /// The Commander's own line is their sentence, and no ship is bolted onto a list that is about no
    /// ship.
    /// </summary>
    [Fact]
    public void ACustomLineIsAboutNoShipAndSaysNothingAboutOne()
    {
        var note = new ChecklistItem
        {
            Key = "note",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = "buy limpets",
        };

        Assert.Null(ChecklistWording.Ship(note.Scope, null, State()));
        Assert.Equal("buy limpets", ChecklistWording.Line(note, State()));
        Assert.Equal("custom", ChecklistWording.Where(note, State()));
    }

    /// <summary>
    /// Nothing here reads a clock or a journal of its own, so a page drawn before the first
    /// <c>Loadout</c> lands is the stored wording rather than an exception.
    /// </summary>
    [Fact]
    public void WithNoGameStateAtAllTheStoredWordingStands()
    {
        var item = Item(
            new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size7") { Grade = 5 },
            "Grade 5 engineering on Slot01_Size7");

        Assert.Equal("Grade 5 engineering on Slot01_Size7", ChecklistWording.Said(item, null));
        Assert.Equal("Anaconda (ship 51)", ChecklistWording.Where(item, null));
    }
}
