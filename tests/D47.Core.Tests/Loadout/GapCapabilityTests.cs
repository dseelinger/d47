using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>The opening sentence of "what do my plans still need" (#303).</summary>
public class GapCapabilityTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static ChecklistService Checklists(TempInstall install) =>
        new(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static async Task<string> Say(TempInstall install, bool secondShip = false)
    {
        var shipStore = new ShipBuildStore(
            Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance);
        var state = State();
        var checklists = Checklists(install);
        var ships = new ShipPlanService(shipStore, checklists, () => state);

        var build = ships.BuildFor(12, "python", "Bad Idea");
        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));

        if (secondShip)
        {
            var second = ships.BuildFor(13, "python", "Second Idea");
            ships.Plan(second.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));
        }

        var capability = GapCapability.Create(ships, null, () => state);
        var tool = capability.Tools[0];

        var result = await tool.Handler(ToolArguments.Empty, CancellationToken.None);

        return result.Content;
    }

    /// <summary>One plan is singular throughout: "1 ship or suit you've planned."</summary>
    [Fact]
    public async Task ThePluralsDropAtOne()
    {
        using var install = new TempInstall();

        var said = await Say(install);

        Assert.StartsWith("You're short ", said, StringComparison.Ordinal);
        Assert.Contains("units of", said, StringComparison.Ordinal);
        Assert.Contains("materials, for 1 ship or suit you've planned.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("units still to find", said, StringComparison.Ordinal);
    }

    /// <summary>More than one plan reads "ships and suits you've planned."</summary>
    [Fact]
    public async Task MoreThanOnePlanIsPlural()
    {
        using var install = new TempInstall();

        var said = await Say(install, secondShip: true);

        Assert.Contains("for 2 ships and suits you've planned.", said, StringComparison.Ordinal);
    }
}
