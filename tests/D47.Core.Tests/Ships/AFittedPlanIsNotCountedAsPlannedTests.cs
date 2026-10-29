using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>The fleet counts only the slot plans still outstanding, on the card and in the voice answer (#1907).</summary>
public class AFittedPlanIsNotCountedAsPlannedTests
{
    private const string Thrusters = "int_engine_size5_class5";

    private const string Drive = "int_hyperdrive_size5_class5";

    private const string Commander =
        """{ "timestamp":"2026-08-18T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Ada Quill" }""";

    private static string Loadout(params string[] items) =>
        $$"""
        { "timestamp":"2026-08-18T09:00:05Z", "event":"Loadout", "Ship":"python", "ShipID":21, "ShipName":"Reaper",
          "Modules":[ {{string.Join(",", items)}} ] }
        """;

    private static string Fitted(string slot, string item) => $$"""{ "Slot":"{{slot}}", "Item":"{{item}}", "On":true }""";

    private static (ShipPlanService Ships, ShipBuild Build) Rig(string? loadout)
    {
        var install = new MemoryInstall();
        var game = new GameStateStore();

        void Play(string line)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            game.Apply(parsed!);
        }

        Play(Commander);

        if (loadout is not null)
        {
            Play(loadout);
        }

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), install.Files, NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = loadout is null ? ships.Intend("python")! : ships.BuildFor(21, "python", "Reaper");

        ships.Plan(build.Id, new SlotPlan("MainEngines", Module: "Thrusters") { Variant = Thrusters });
        ships.Plan(build.Id, new SlotPlan("FrameShiftDrive", Module: "Frame Shift Drive") { Variant = Drive });

        return (ships, build);
    }

    [Fact]
    public void ABuildWithBothPlansFittedCountsNone()
    {
        var (ships, _) = Rig(Loadout(Fitted("MainEngines", Thrusters), Fitted("FrameShiftDrive", Drive)));

        var entry = Assert.Single(ships.Fleet());

        Assert.Equal(0, entry.Planned);
        Assert.DoesNotContain("planned", ShipsCapability.Fleet(ships), StringComparison.Ordinal);
    }

    [Fact]
    public void ABuildWithOnePlanFittedCountsTheOtherAndSaysOneSlot()
    {
        var (ships, _) = Rig(Loadout(Fitted("MainEngines", Thrusters)));

        var entry = Assert.Single(ships.Fleet());

        Assert.Equal(1, entry.Planned);
        Assert.EndsWith(", 1 slot planned", ShipsCapability.Fleet(ships), StringComparison.Ordinal);
    }

    [Fact]
    public void AHullNotBoughtYetCountsEveryPlan()
    {
        var (ships, _) = Rig(null);

        var entry = Assert.Single(ships.Fleet());

        Assert.Equal(2, entry.Planned);
        Assert.EndsWith(", 2 slots planned", ShipsCapability.Fleet(ships), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyPlanIsNotCounted()
    {
        var (ships, build) = Rig(null);

        ships.Plan(build.Id, new SlotPlan("Hardpoint1"));

        Assert.Equal(2, Assert.Single(ships.Fleet()).Planned);
    }

    [Fact]
    public void ThePlannedCountOfShipsStaysOnTheBuildsPresent()
    {
        var (ships, _) = Rig(Loadout(Fitted("MainEngines", Thrusters), Fitted("FrameShiftDrive", Drive)));

        Assert.Contains("1 with a build planned", ShipsCapability.Live(ships), StringComparison.Ordinal);
    }
}
