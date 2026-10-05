using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The 19 small common journal events read into rows that answer a question (#815).</summary>
public partial class TheSmallCommonEventsAreGroupedByQuestionTests
{
    private static readonly string[] Events =
    [
        """{ "timestamp":"2025-07-04T02:32:03Z", "event":"StartJump", "JumpType":"Hyperspace", "Taxi":false, "StarSystem":"Abibitoa", "SystemAddress":7505354691298, "StarClass":"K" }""",
        """{ "timestamp":"2025-07-04T10:25:07Z", "event":"FSDTarget", "Name":"Col 285 Sector GS-J c9-8", "SystemAddress":2282674459354, "StarClass":"M", "RemainingJumpsInRoute":8 }""",
        """{ "timestamp":"2025-07-04T10:28:57Z", "event":"FuelScoop", "Scooped":4.739725, "Total":32.000000 }""",
        """{ "timestamp":"2025-07-05T03:41:22Z", "event":"SupercruiseExit", "Taxi":false, "Multicrew":false, "StarSystem":"Amariba", "SystemAddress":5856087282402, "Body":"Newcomen Gateway", "BodyID":20, "BodyType":"Station" }""",
        """{ "timestamp":"2025-07-04T01:51:24Z", "event":"SupercruiseEntry", "Taxi":false, "Multicrew":false, "StarSystem":"Hyades Sector DR-V c2-23", "SystemAddress":6405910172338 }""",
        """{ "timestamp":"2025-07-04T19:06:44Z", "event":"SupercruiseDestinationDrop", "Type":"Newcomen Gateway", "Threat":0, "MarketID":3227334144 }""",
        """{ "timestamp":"2025-07-05T03:41:35Z", "event":"DockingRequested", "MarketID":3227334144, "StationName":"Newcomen Gateway", "StationType":"Coriolis", "LandingPads":{ "Small":17, "Medium":18, "Large":9 } }""",
        """{ "timestamp":"2025-07-04T02:35:24Z", "event":"DockingGranted", "LandingPad":35, "MarketID":3227322880, "StationName":"Fidalgo Dock", "StationType":"Coriolis" }""",
        """{ "timestamp":"2025-07-04T23:26:14Z", "event":"Undocked", "StationName":"Wait Colburn Dock", "StationType":"Coriolis", "MarketID":3227193600, "Taxi":false, "Multicrew":false }""",
        """{ "timestamp":"2025-07-04T02:36:48Z", "event":"RefuelAll", "Cost":180, "Amount":3.565503 }""",
        """{ "timestamp":"2025-07-05T11:15:22Z", "event":"LaunchDrone", "Type":"Collection" }""",
        """{ "timestamp":"2025-07-04T10:29:43Z", "event":"FSSDiscoveryScan", "Progress":1.000000, "BodyCount":1, "NonBodyCount":3, "SystemName":"Pegasi Sector OD-R b5-7", "SystemAddress":16062506608041 }""",
        """{ "timestamp":"2025-07-05T11:15:32Z", "event":"MaterialCollected", "Category":"Manufactured", "Name":"fedproprietarycomposites", "Name_Localised":"Proprietary Composites", "Count":3 }""",
        """{ "timestamp":"2025-07-05T02:18:54Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"adder", "ScanStage":3, "PilotName":"$npc_name_decorate:#name=Test Pilot;", "PilotName_Localised":"Test Pilot", "PilotRank":"Master", "ShieldHealth":100.000000, "HullHealth":100.000000, "Faction":"The Prime Spade", "LegalStatus":"Wanted", "Bounty":92454 }""",
        """{ "timestamp":"2025-07-10T10:50:59Z", "event":"MiningRefined", "Type":"$platinum_name;", "Type_Localised":"Platinum" }""",
        """{ "timestamp":"2025-07-07T00:01:23Z", "event":"UnderAttack", "Target":"You" }""",
        """{ "timestamp":"2025-07-07T17:38:35Z", "event":"BackpackChange", "Added":[ { "Name":"insightdatabank", "Name_Localised":"Insight Data Bank", "OwnerID":0, "MissionID":1021817590, "Count":1, "Type":"Item" } ] }""",
        """{ "timestamp":"2025-07-05T11:43:11Z", "event":"PowerplayMerits", "Power":"Edmund Mahon", "MeritsGained":55, "TotalMerits":6876 }""",
        """{ "timestamp":"2025-07-07T17:38:35Z", "event":"CollectItems", "Name":"insightdatabank", "Name_Localised":"Insight Data Bank", "Type":"Item", "OwnerID":0, "Count":1, "Stolen":false }""",
    ];

    private const string ScanStageZero =
        """{ "timestamp":"2025-07-04T11:13:36Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"corsair", "ScanStage":0 }""";

    private static EventReading Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var kind = document.RootElement.GetProperty("event").GetString()!;
        var log = new JournalLog();

        log.Add([new JournalEvent(DateTimeOffset.Parse("2025-07-04T00:00:00Z"), kind, document.RootElement.Clone())]);

        return EventReadings.For(Assert.Single(log.Read(noise: true)));
    }

    private static ReadingRow Row(EventReading reading, string label) =>
        Assert.Single(reading.Rows, row => row.Label == label);

    private static string Joined(EventReading reading, string label) =>
        string.Join(" · ", Row(reading, label).Values.Select(value => value.Text));

    [Fact]
    public void EachOfTheNineteenKindsHasRowsAndOneEntryPerField()
    {
        Assert.Equal(19, Events.Length);

        foreach (var json in Events)
        {
            var reading = Read(json);

            using var document = JsonDocument.Parse(json);

            Assert.NotEmpty(reading.Rows);
            Assert.Equal(document.RootElement.EnumerateObject().Count(), reading.Plumbing.Count);
            Assert.All(reading.Rows, row => Assert.DoesNotMatch(FieldName(), row.Label));
        }
    }

    [GeneratedRegex(@"[a-z][A-Z]|_")]
    private static partial Regex FieldName();

    [Fact]
    public void AJumpReadsAsKindDestinationAndStar()
    {
        var jump = Read(Events[0]);
        var target = Read(Events[1]);

        Assert.Equal("Hyperspace", Joined(jump, "Jump"));
        Assert.Equal(ReadingTone.System, Assert.Single(Row(jump, "To").Values).Tone);
        Assert.Equal("K", Joined(jump, "Star class"));
        Assert.Equal("Col 285 Sector GS-J c9-8", Joined(target, "Next jump"));
        Assert.Equal(ReadingTone.System, Assert.Single(Row(target, "Next jump").Values).Tone);
        Assert.Equal([new ReadingRun("8", true)], Assert.Single(Row(target, "Jumps left").Values).Runs);
    }

    [Fact]
    public void SupercruiseAndDockingEventsNameTheStationAndTheSystem()
    {
        Assert.Equal("Newcomen Gateway", Joined(Read(Events[3]), "Arrived at"));
        Assert.Equal("Station", Joined(Read(Events[3]), "Body type"));
        Assert.Equal("Amariba", Joined(Read(Events[3]), "System"));
        Assert.Equal("Hyades Sector DR-V c2-23", Joined(Read(Events[4]), "System"));
        Assert.Equal("Newcomen Gateway", Joined(Read(Events[5]), "Dropped at"));
        Assert.Equal("0", Joined(Read(Events[5]), "Threat level"));
        Assert.Equal("Newcomen Gateway · Coriolis", Joined(Read(Events[6]), "Station"));
        Assert.Equal("17 small · 18 medium · 9 large", Joined(Read(Events[6]), "Landing pads"));
        Assert.Equal("35", Joined(Read(Events[7]), "Pad"));
        Assert.Equal("Fidalgo Dock · Coriolis", Joined(Read(Events[7]), "Station"));
        Assert.Equal("Wait Colburn Dock · Coriolis", Joined(Read(Events[8]), "Station"));
    }

    [Fact]
    public void ARefuelReadsItsAmountInTonnesAndItsCostInCredits()
    {
        var refuel = Read(Events[9]);
        var scoop = Read(Events[2]);

        Assert.Equal("3.57 t", Joined(refuel, "Fuel bought"));
        Assert.Equal("180 Cr", Joined(refuel, "Cost"));
        Assert.Equal("4.74 t", Joined(scoop, "Scooped"));
        Assert.Equal("32 t", Joined(scoop, "Fuel now"));
    }

    [Fact]
    public void AScanOfTheSystemReadsItsProgressAsAPercentage()
    {
        var scan = Read(Events[11]);

        Assert.Equal("Pegasi Sector OD-R b5-7", Joined(scan, "System"));
        Assert.Equal("100%", Joined(scan, "Scanned"));
        Assert.Equal("1", Joined(scan, "Bodies found"));
        Assert.Equal("3", Joined(scan, "Other objects"));
        Assert.Equal("Collection", Joined(Read(Events[10]), "Drone"));
    }

    [Fact]
    public void AMaterialIsDecodedThroughTheCatalogue()
    {
        var material = Read(Events[12]);
        var item = Read(Events[18]);

        Assert.Equal("Proprietary Composites", Joined(material, "Material"));
        Assert.Equal("Manufactured", Joined(material, "Category"));
        Assert.Equal("3", Joined(material, "Collected"));
        Assert.Equal("Insight Data Bank", Joined(item, "Item"));
        Assert.Equal("1", Joined(item, "Taken"));
        Assert.DoesNotContain(item.Rows, row => row.Label == "Stolen" || row.Values.Any(value => value.Tone == ReadingTone.Warning));
    }

    [Fact]
    public void AStolenItemIsAWarning()
    {
        var reading = Read(Events[18].Replace("\"Stolen\":false", "\"Stolen\":true"));

        Assert.Equal(ReadingTone.Warning, Assert.Single(reading.Rows, row => row.Label.Length == 0).Values[0].Tone);
    }

    [Fact]
    public void AScanAtStageZeroHasNoShieldsHullOrBountyAndStageThreeHasAll()
    {
        var early = Read(ScanStageZero);
        var late = Read(Events[13]);

        Assert.Equal(["Ship", "Scan stage"], early.Rows.Select(row => row.Label));
        Assert.Equal("100%", Joined(late, "Shields"));
        Assert.Equal("100%", Joined(late, "Hull"));
        Assert.Equal("92,454 Cr", Joined(late, "Bounty"));
    }

    [Fact]
    public void AShipTargetedNamesItsPilotFactionAndStanding()
    {
        var reading = Read(Events[13]);

        var pilot = Assert.Single(Row(reading, "Pilot").Values);
        var legal = Assert.Single(Row(reading, "Legal status").Values);

        Assert.Equal("Test Pilot", pilot.Text);
        Assert.Equal(ReadingTone.Name, pilot.Tone);
        Assert.False(pilot.Typed);
        Assert.Equal(ReadingTone.Name, Assert.Single(Row(reading, "Faction").Values).Tone);
        Assert.Equal(ReadingTone.Warning, legal.Tone);
        Assert.Equal(ReadingTone.Value, Assert.Single(Row(Read(Events[13].Replace("Wanted", "Clean")), "Legal status").Values).Tone);
    }

    [Fact]
    public void APilotWithNoLocalisedNameIsUndecoratedOrTypedAsAPlayerWroteIt()
    {
        var bare = Events[13].Replace("\"PilotName_Localised\":\"Test Pilot\", ", string.Empty);
        var player = bare.Replace("$npc_name_decorate:#name=Test Pilot;", "Player One");

        var decorated = Assert.Single(Row(Read(bare), "Pilot").Values);
        var typed = Assert.Single(Row(Read(player), "Pilot").Values);

        Assert.Equal("Test Pilot", decorated.Text);
        Assert.False(decorated.Typed);
        Assert.Equal("Player One", typed.Text);
        Assert.True(typed.Typed);
    }

    [Fact]
    public void AnAimedSubsystemShowsItsHealthAndASquadronIsAName()
    {
        var reading = Read(Events[13].Replace(
            "\"Bounty\":92454",
            "\"Bounty\":92454, \"SquadronID\":\"TSTX\", \"Subsystem\":\"$int_fsdinterdictor_size1_class3_name;\", \"Subsystem_Localised\":\"FSD Interdictor\", \"SubsystemHealth\":64.5"));

        Assert.Equal("FSD Interdictor · 64.5%", Joined(reading, "Aimed at"));
        Assert.Equal("TSTX", Joined(reading, "Squadron"));
        Assert.Equal(ReadingTone.Name, Assert.Single(Row(reading, "Squadron").Values).Tone);
    }

    [Fact]
    public void ARefinedOreAndAnAttackReadPlainly()
    {
        var ore = Assert.Single(Row(Read(Events[14]), "Refined").Values);

        Assert.Equal("Platinum", ore.Text);
        Assert.Equal("$platinum_name;", ore.Symbol);
        Assert.Equal("You", Joined(Read(Events[15]), "Attacked"));
    }

    [Fact]
    public void ABackpackChangeNamesEachItemWithItsCount()
    {
        var added = Read(Events[16]);
        var removed = Read("""{ "timestamp":"2026-09-26T21:56:40Z", "event":"BackpackChange", "Removed":[ { "Name":"energycell", "Name_Localised":"Energy Cell", "OwnerID":0, "Count":2, "Type":"Consumable" } ] }""");

        var item = Assert.Single(Row(added, "Into the backpack").Values);

        Assert.Equal("Insight Data Bank ×1", item.Text);
        Assert.Equal([new ReadingRun("Insight Data Bank ×", false), new ReadingRun("1", true)], item.Runs);
        Assert.Equal("Energy Cell ×2", Joined(removed, "Out of the backpack"));
        Assert.DoesNotContain(removed.Rows, row => row.Label == "Into the backpack");
    }

    [Fact]
    public void PowerplayMeritsNameThePowerAndCountTheMerits()
    {
        var merits = Read(Events[17]);

        Assert.Equal(ReadingTone.Name, Assert.Single(Row(merits, "Power").Values).Tone);
        Assert.Equal("55", Joined(merits, "Merits gained"));
        Assert.Equal("6,876", Joined(merits, "Total merits"));
    }
}
