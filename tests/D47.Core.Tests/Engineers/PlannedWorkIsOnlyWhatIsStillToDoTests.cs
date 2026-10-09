using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// Planned work leaves out a blueprint or experimental the remembered loadout already has, so the list,
/// the count and the unlock ranking agree (#659).
/// </summary>
public class PlannedWorkIsOnlyWhatIsStillToDoTests
{
    private const int ShipId = 7;

    private static readonly ShipBuild Build = new(
        "F1",
        "ship-1",
        "python",
        ShipId,
        "Test Ship",
        [
            new SlotPlan("PowerPlant", "Armoured", 5, "Thermal Spread", "Power Plant"),
            new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Drag Drives", "Thrusters"),
            new SlotPlan("Slot01_Size6", "Reinforced Shields", 5, "Hi-cap", "Shield Generator"),
        ]);

    [Fact]
    public void AnAppliedBlueprintAndExperimentalAreNotListed()
    {
        var report = UnlockPlanner.Of([Build], [], State(Loadout));

        Assert.DoesNotContain(report.Planned, work => work.What.Contains("Power Plant", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Planned, work => work.Wants is "Dirty Drive Tuning" or "Drag Drives");
    }

    [Fact]
    public void ASlotMissingOnlyItsExperimentalListsOnlyTheExperimental()
    {
        var report = UnlockPlanner.Of([Build], [], State(Loadout));

        var work = Assert.Single(report.Planned);
        Assert.Equal("Hi-cap", work.Wants);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AnEngineerWhoseWorkIsAllAppliedHasNoPlannedWorkAndNoCount()
    {
        var report = UnlockPlanner.Of([Build], [], State(Loadout));

        var palin = report.Directory.Single(entry => entry.Engineer.Name == "Professor Palin");

        Assert.Empty(palin.Planned);
        Assert.Equal(0, palin.Wanted);
    }

    [Fact]
    public void TheRankingValuesNoWorkThatIsAlreadyApplied()
    {
        var report = UnlockPlanner.Of([Build], [], State(Loadout));

        Assert.All(report.Route.SelectMany(candidate => candidate.Covers), work => Assert.Equal("Hi-cap", work.Wants));
        Assert.DoesNotContain(report.Route, candidate => candidate.Engineer.Name == "Professor Palin" && candidate.Value > 0);
    }

    [Fact]
    public void TheCountAndTheListAgreeOnWhoHasWork()
    {
        var report = UnlockPlanner.Of([Build], [], State(Loadout));

        Assert.All(report.Directory, entry => Assert.Equal(entry.Wanted > 0, entry.Planned.Count > 0));
    }

    [Fact]
    public void AShipWithNoRememberedLoadoutKeepsAllOfItsWork()
    {
        var report = UnlockPlanner.Of([Build], [], State());

        Assert.Equal(6, report.Planned.Count);
    }

    [Fact]
    public void AModificationOnTheWornSuitIsNotListed()
    {
        var suit = new OnFootBuild(
            "kit-1",
            "Maverick",
            OnFootKind.Suit,
            SuitId,
            [
                new KitPlan(OnFootBuild.ModSlot(1), Modification: "Quieter footsteps"),
                new KitPlan(OnFootBuild.ModSlot(2), Modification: "Night vision"),
            ]);

        var worn = UnlockPlanner.Of([], [suit], State(SuitLoadout));
        var elsewhere = UnlockPlanner.Of([], [suit with { ItemId = SuitId + 1 }], State(SuitLoadout));

        Assert.Equal(["Night vision"], worn.Planned.Select(work => work.Wants));
        Assert.Equal(2, elsewhere.Planned.Count);
    }

    private const long SuitId = 1845879835891144;

    private const string SuitLoadout =
        """
        {"timestamp":"2026-09-23T20:00:00Z","event":"SuitLoadout","SuitID":1845879835891144,
         "SuitName":"utilitysuit_class5","SuitMods":["suit_quieterfootsteps"],"LoadoutID":4293000001,
         "LoadoutName":"Ground","Modules":[]}
        """;

    private const string Loadout =
        """
        {"timestamp":"2026-09-23T20:00:00Z","event":"Loadout","Ship":"python","ShipID":7,"ShipName":"Test Ship",
         "Modules":[
          {"Slot":"PowerPlant","Item":"int_powerplant_size7_class5","On":true,"Priority":0,"Health":1.0,
           "Engineering":{"Engineer":"Hera Tani","EngineerID":300090,"BlueprintID":128673765,
           "BlueprintName":"PowerPlant_Armoured","Level":5,"Quality":1.0,
           "ExperimentalEffect":"special_powerplant_cooled","ExperimentalEffect_Localised":"Thermal Spread",
           "Modifiers":[]}},
          {"Slot":"MainEngines","Item":"int_engine_size6_class5","On":true,"Priority":0,"Health":1.0,
           "Engineering":{"Engineer":"Professor Palin","EngineerID":300220,"BlueprintID":128673659,
           "BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,
           "ExperimentalEffect":"special_engine_overloaded","ExperimentalEffect_Localised":"Drag Drives",
           "Modifiers":[]}},
          {"Slot":"Slot01_Size6","Item":"int_shieldgenerator_size6_class5","On":true,"Priority":0,"Health":1.0,
           "Engineering":{"Engineer":"Lei Cheung","EngineerID":300120,"BlueprintID":128673839,
           "BlueprintName":"ShieldGenerator_Reinforced","Level":5,"Quality":1.0,"Modifiers":[]}}]}
        """;

    /// <summary>A Commander in Sol who has met Liz Ryder only, so every other engineer is still locked.</summary>
    private static CommanderGameState State(params string[] extra)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-09-23T19:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-09-23T19:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-09-23T19:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Known"}]}""",
                 }.Concat(extra))
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
