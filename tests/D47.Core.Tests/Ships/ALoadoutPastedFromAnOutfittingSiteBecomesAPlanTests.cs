using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>A pasted SLEF loadout is read into slot plans, and imported onto a build of the same hull.</summary>
public class ALoadoutPastedFromAnOutfittingSiteBecomesAPlanTests
{
    private const int ShipId = 41;

    private const string Data = """
        {"Ship":"python","ShipName":"Bad Idea","Modules":[
          {"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":2,
           "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"ExperimentalEffect":"special_engine_haulage","Quality":1.0}},
          {"Slot":"PowerPlant","Item":"int_powerplant_size6_class5","On":true,"Priority":0},
          {"Slot":"PaintJob","Item":"paintjob_python_default_1","On":true},
          {"Slot":"Decal1","Item":"decal_explorer_elite","On":true},
          {"Slot":"CargoHatch","Item":"modularcargobaydoor","On":true}
        ]}
        """;

    private static string Slef(string data) => $$"""[{"header":{"appName":"Coriolis"},"data":{{data}}}]""";

    private static string AsEvent(string data) =>
        data.Replace("{\"Ship\"", "{\"timestamp\":\"2026-09-10T09:00:00Z\",\"event\":\"Loadout\",\"Ship\"", StringComparison.Ordinal);

    [Fact]
    public void ASlefArrayGivesOnePlanPerPlannableSlot()
    {
        var read = LoadoutImport.Read(Slef(Data));

        Assert.False(read.IsRefused);
        Assert.Equal("python", read.Hull);
        Assert.Equal(["MainEngines", "PowerPlant"], read.Slots.Select(slot => slot.Slot));
        Assert.Empty(read.Unread);

        var engines = read.Slots[0];

        Assert.Equal("int_engine_size5_class5", engines.Variant);
        Assert.Equal(5, engines.Grade);
        Assert.Equal(3, engines.Priority);
        Assert.False(string.IsNullOrEmpty(engines.Blueprint));
        Assert.NotEqual("Engine_Dirty", engines.Blueprint);
        Assert.False(string.IsNullOrEmpty(engines.Experimental));
        Assert.False(string.IsNullOrEmpty(engines.Module));

        var plant = read.Slots[1];

        Assert.Null(plant.Blueprint);
        Assert.Equal(0, plant.Grade);
        Assert.Equal(1, plant.Priority);
    }

    [Fact]
    public void ABareObjectAndABareEventGiveTheSamePlans()
    {
        var expected = LoadoutImport.Read(Slef(Data)).Slots;

        Assert.Equal(expected, LoadoutImport.Read($$"""{"header":{},"data":{{Data}}}""").Slots);
        Assert.Equal(expected, LoadoutImport.Read(AsEvent(Data)).Slots);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"header":{},"data":{"Ship":"python"}}""")]
    [InlineData("[]")]
    [InlineData("")]
    public void TextThatIsNotALoadoutIsRefusedWithASentence(string text)
    {
        var read = LoadoutImport.Read(text);

        Assert.Equal("That text is not a loadout.", read.Refusal);
        Assert.Empty(read.Slots);
    }

    [Fact]
    public void TwoLoadoutsAreRefusedAndTheSentenceSaysOneAtATime()
    {
        var read = LoadoutImport.Read($$"""[{"data":{{Data}}},{"data":{{Data}}}]""");

        Assert.Contains("one build at a time", read.Refusal);
    }

    [Fact]
    public void AnUnknownHullRefusesTheWholeImport()
    {
        var read = LoadoutImport.Read(Slef("""{"Ship":"notahull","Modules":[]}"""));

        Assert.True(read.IsRefused);
    }

    [Fact]
    public void UnknownPartsAreReportedAndTheRestIsKept()
    {
        const string data = """
            {"Ship":"python","Modules":[
              {"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,
               "Engineering":{"BlueprintName":"Engine_Nonsense","Level":3,"ExperimentalEffect":"special_nonsense"}},
              {"Slot":"PowerPlant","Item":"int_notamodule","On":true}
            ]}
            """;

        var read = LoadoutImport.Read(Slef(data));

        var engines = Assert.Single(read.Slots);

        Assert.Equal("MainEngines", engines.Slot);
        Assert.Null(engines.Blueprint);
        Assert.Equal(0, engines.Grade);
        Assert.Null(engines.Experimental);
        Assert.Contains(read.Unread, line => line.Contains("Engine_Nonsense", StringComparison.Ordinal));
        Assert.Contains(read.Unread, line => line.Contains("special_nonsense", StringComparison.Ordinal));
        Assert.Contains(read.Unread, line => line.Contains("int_notamodule", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportingOntoAnotherHullChangesNothing()
    {
        var (_, ships) = Set(fit: false);
        var other = ships.Intend("anaconda")!;

        var said = ships.Import(other.Id, LoadoutImport.Read(Slef(Data)), dropMet: false);

        Assert.Contains("Nothing was changed", said);
        Assert.Empty(ships.Store.Find(other.Id)!.Slots);
    }

    [Fact]
    public void ImportKeepsTheBuildsIdentityAndEmptiesItsPriorityMoves()
    {
        var (_, ships) = Set(fit: false);
        var build = ships.BuildFor(ShipId, "python", "Bad Idea");

        ships.Plan(build.Id, new SlotPlan("Armour", Module: "Lightweight Alloy"));
        ships.Move(build.Id, "MainEngines", 3);

        var said = ships.Import(build.Id, LoadoutImport.Read(Slef(Data)), dropMet: false);
        var after = ships.Store.Find(build.Id)!;

        Assert.Equal(ShipId, after.ShipId);
        Assert.Equal("Bad Idea", after.Name);
        Assert.Equal(build.Id, after.Id);
        Assert.Empty(after.PriorityMoves);
        Assert.Equal(["MainEngines", "PowerPlant"], after.Slots.Select(slot => slot.Slot));
        Assert.Equal("Planned 2 slots from the loadout.", said);
    }

    [Fact]
    public void WithDropMetASlotAlreadyFittedIsLeftOutAndCounted()
    {
        var (_, ships) = Set(fit: true);
        var build = ships.BuildFor(ShipId, "python");

        var said = ships.Import(build.Id, LoadoutImport.Read(Slef(Data)), dropMet: true);

        Assert.Equal(["MainEngines"], ships.Store.Find(build.Id)!.Slots.Select(slot => slot.Slot));
        Assert.Contains("1 slot already matched what is fitted and was left out", said);
    }

    private static (GameStateStore Game, ShipPlanService Ships) Set(bool fit)
    {
        var game = new GameStateStore();
        var fitted = fit ? """{"Slot":"PowerPlant","Item":"int_powerplant_size6_class5","On":true}""" : string.Empty;

        game.Apply(Event("""{"timestamp":"2026-09-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Event(
            $$"""
            {"timestamp":"2026-09-10T09:05:00Z","event":"Loadout","Ship":"python","ShipID":{{ShipId}},
             "Modules":[{{fitted}}]}
            """));

        var files = new MemoryFileSystem();

        var checklists = new ChecklistService(
            new ChecklistStore("checklist.json", files, NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore("checklist-proposals.json", files, NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore("ships.json", files, NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        return (game, ships);
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
