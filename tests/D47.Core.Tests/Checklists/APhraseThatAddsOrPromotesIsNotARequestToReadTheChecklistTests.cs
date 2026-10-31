using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>Phrases that add to or promote onto the checklist reach the tool they name, not get_checklist (#1958).</summary>
public sealed class APhraseThatAddsOrPromotesIsNotARequestToReadTheChecklistTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static KeywordRouter Router(bool shipBuild)
    {
        var install = new MemoryInstall();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            store.Apply(Event(line));
        }

        var state = store.Active!;
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => state);

        if (shipBuild)
        {
            var build = ships.BuildFor(12, "python", "Bad Idea");
            ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        }

        return new KeywordRouter(CapabilityRegistry.Build(
        [
            OnFootCapability.Create(() => state, null, ships),
            ChecklistCapability.Create(checklists, ships, null),
            ShipsCapability.Create(ships, null),
        ]));
    }

    [Theory]
    [InlineData("add buy a fuel scoop to my checklist")]
    [InlineData("put this build on my checklist")]
    [InlineData("put that on my checklist")]
    public void NoReadingOfTheChecklistIsMatched(string said)
    {
        var router = Router(shipBuild: true);

        Assert.NotEqual("get_checklist", router.Match(said)?.ToolName);
    }

    [Fact]
    public void ReadingTheChecklistStillIs()
    {
        Assert.Equal("get_checklist", Router(shipBuild: true).Match("my checklist")?.ToolName);
    }

    [Fact]
    public void PuttingThisBuildOnTheChecklistPromotesTheShipBuild()
    {
        Assert.Equal("promote_ship_plan", Router(shipBuild: true).MatchToolCommand("put this build on my checklist")?.ToolName);
    }

    [Fact]
    public void PuttingThatOnTheChecklistPromotesTheShipBuildWhenTheFlownShipHasOne()
    {
        Assert.Equal("promote_ship_plan", Router(shipBuild: true).MatchToolCommand("put that on my checklist")?.ToolName);
    }

    [Fact]
    public void PuttingThatOnTheChecklistPromotesTheSuitPlanWhenTheFlownShipHasNoBuild()
    {
        Assert.Equal("promote_on_foot_plan", Router(shipBuild: false).MatchToolCommand("put that on my checklist")?.ToolName);
    }
}
