using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A fleet card's planned line and dot follow the plans still outstanding (#1907).</summary>
public class ACardCountsOnlyPlansStillOutstandingTests
{
    private const string Thrusters = "int_engine_size5_class5";

    private static LoadoutRow Card(bool fitDrive)
    {
        var game = new GameStateStore();
        var lines = new List<string>
        {
            """{ "timestamp":"2026-08-18T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Ada Quill" }""",
            $$"""
            { "timestamp":"2026-08-18T09:00:05Z", "event":"Loadout", "Ship":"python", "ShipID":21, "ShipName":"Reaper",
              "Modules":[ { "Slot":"MainEngines", "Item":"{{Thrusters}}", "On":true }
                          {{(fitDrive ? ", { \"Slot\":\"FrameShiftDrive\", \"Item\":\"int_hyperdrive_size5_class5\", \"On\":true }" : string.Empty)}} ] }
            """,
        };

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            game.Apply(parsed!);
        }

        var files = new MemoryFileSystem();

        var checklists = new ChecklistService(
            new ChecklistStore("checklist.json", files, NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore("checklist-proposals.json", files, NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore("ships.json", files, NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = ships.BuildFor(21, "python", "Reaper");

        ships.Plan(build.Id, new SlotPlan("MainEngines", Module: "Thrusters") { Variant = Thrusters });
        ships.Plan(build.Id, new SlotPlan("FrameShiftDrive", Module: "Frame Shift Drive") { Variant = "int_hyperdrive_size5_class5" });

        return Assert.Single(new ShipsMode(ships, checklists, () => null).Items());
    }

    [Fact]
    public void OnePlanLeftReadsOnePlannedAndKeepsTheDot()
    {
        var card = Card(fitDrive: false);

        Assert.Contains("1 planned", card.Aside);
        Assert.True(card.Marked);
    }

    [Fact]
    public void NoPlansLeftHasNoPlannedLineAndNoDot()
    {
        var card = Card(fitDrive: true);

        Assert.DoesNotContain("planned", card.Aside ?? string.Empty);
        Assert.False(card.Marked);
    }
}
