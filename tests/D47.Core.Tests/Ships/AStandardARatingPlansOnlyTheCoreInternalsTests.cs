using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// The Standard-A rating plans power plant, thrusters, drive and distributor at A and life support and sensors at D,
/// at the slot's size and in the family already there, and leaves every other slot alone (#2289).
/// </summary>
public class AStandardARatingPlansOnlyTheCoreInternalsTests
{
    private const string Commander =
        """{ "timestamp":"2026-11-10T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Ada Quill" }""";

    private static string Loadout(string hull, params (string Slot, string Item)[] modules) =>
        $$"""
        { "timestamp":"2026-11-10T09:00:05Z", "event":"Loadout", "Ship":"{{hull}}", "ShipID":21, "ShipName":"Reaper",
          "Modules":[ {{string.Join(",", modules.Select(module => $$"""{ "Slot":"{{module.Slot}}", "Item":"{{module.Item}}", "On":true }"""))}} ] }
        """;

    private static ShipPlanService Service(string? loadout)
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

        return new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), install.Files, NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);
    }

    private static string? Planned(ShipPlanService ships, string buildId, string slot) =>
        ships.Store.Find(buildId)!.For(slot)?.Variant;

    [Fact]
    public void AMandalayAlreadyAtAWithAnScoDriveGetsOnlyItsSensorsLowered()
    {
        var ships = Service(Loadout(
            "mandalay",
            ("Armour", "mandalay_armour_grade1"),
            ("PowerPlant", "int_powerplant_size5_class5"),
            ("MainEngines", "int_engine_size5_class5"),
            ("FrameShiftDrive", "int_hyperdrive_overcharge_size5_class5"),
            ("LifeSupport", "int_lifesupport_size4_class2"),
            ("PowerDistributor", "int_powerdistributor_size5_class5"),
            ("Radar", "int_sensors_size5_class5"),
            ("FuelTank", "int_fueltank_size5_class3")));

        var build = ships.BuildFor(21, "mandalay", "Reaper");

        Assert.Equal("Planned 1 core internal; 5 were already there.", ships.StandardA(build.Id));

        var slot = Assert.Single(ships.Store.Find(build.Id)!.Slots);

        Assert.Equal("Radar", slot.Slot);
        Assert.Equal("int_sensors_size5_class2", slot.Variant);
        Assert.Equal("Sensors", slot.Module);
    }

    [Fact]
    public void AnIntendedHullWithNothingPlannedGetsAllSixAtTheSlotsSize()
    {
        var ships = Service(null);
        var build = ships.Intend("python")!;

        Assert.Equal("Planned 6 core internals.", ships.StandardA(build.Id));

        Assert.Equal("int_powerplant_size7_class5", Planned(ships, build.Id, "PowerPlant"));
        Assert.Equal("int_engine_size6_class5", Planned(ships, build.Id, "MainEngines"));
        Assert.Equal("int_hyperdrive_size5_class5", Planned(ships, build.Id, "FrameShiftDrive"));
        Assert.Equal("int_lifesupport_size4_class2", Planned(ships, build.Id, "LifeSupport"));
        Assert.Equal("int_powerdistributor_size7_class5", Planned(ships, build.Id, "PowerDistributor"));
        Assert.Equal("int_sensors_size6_class2", Planned(ships, build.Id, "Radar"));
    }

    [Fact]
    public void ArmourFuelAndEveryOtherSlotPlanAreUnchanged()
    {
        var ships = Service(null);
        var build = ships.Intend("python")!;

        SlotPlan[] others =
        [
            new("Armour", Module: "Python Military Grade Composite"),
            new("FuelTank", Module: "Fuel Tank") { Variant = "int_fueltank_size5_class3" },
            new("LargeHardpoint1", Module: "Beam Laser"),
            new("TinyHardpoint1", Module: "Heat Sink Launcher"),
            new("Slot01_Size6", Module: "Cargo Rack"),
        ];

        foreach (var plan in others)
        {
            ships.Plan(build.Id, plan);
        }

        ships.StandardA(build.Id);

        foreach (var plan in others)
        {
            Assert.Equal(plan, ships.Store.Find(build.Id)!.For(plan.Slot));
        }
    }

    [Fact]
    public void PlannedEngineeringAndPriorityAreKept()
    {
        var ships = Service(null);
        var build = ships.Intend("python")!;

        ships.Plan(build.Id, new SlotPlan("FrameShiftDrive", "Increased Range", 5, "Mass Manager", "Frame Shift Drive (SCO)")
        {
            Variant = "int_hyperdrive_overcharge_size4_class3",
            Priority = 2,
        });

        ships.StandardA(build.Id);

        var drive = ships.Store.Find(build.Id)!.For("FrameShiftDrive")!;

        Assert.Equal("int_hyperdrive_overcharge_size5_class5", drive.Variant);
        Assert.Equal("Frame Shift Drive (SCO)", drive.Module);
        Assert.Equal("Increased Range", drive.Blueprint);
        Assert.Equal(5, drive.Grade);
        Assert.Equal("Mass Manager", drive.Experimental);
        Assert.Equal(2, drive.Priority);
    }

    [Fact]
    public void AFittedScoDriveStaysAnScoDriveAtA()
    {
        var ships = Service(Loadout("python", ("FrameShiftDrive", "int_hyperdrive_overcharge_size5_class2")));
        var build = ships.BuildFor(21, "python", "Reaper");

        ships.StandardA(build.Id);

        Assert.Equal("int_hyperdrive_overcharge_size5_class5", Planned(ships, build.Id, "FrameShiftDrive"));
    }

    [Fact]
    public void AFittedGuardianHybridPowerPlantIsLeftAlone()
    {
        var ships = Service(Loadout("python", ("PowerPlant", "int_guardianpowerplant_size7")));
        var build = ships.BuildFor(21, "python", "Reaper");

        Assert.Equal("Planned 5 core internals; 1 was already there.", ships.StandardA(build.Id));
        Assert.Null(ships.Store.Find(build.Id)!.For("PowerPlant"));
    }

    [Fact]
    public void EnhancedPerformanceThrustersInALargerSlotBecomePlainThrustersAtTheSlotsSize()
    {
        var ships = Service(Loadout("python", ("MainEngines", "int_engine_size3_class5_fast")));
        var build = ships.BuildFor(21, "python", "Reaper");

        ships.StandardA(build.Id);

        Assert.Equal("int_engine_size6_class5", Planned(ships, build.Id, "MainEngines"));
    }

    [Fact]
    public void ASecondPressPlansNothing()
    {
        var ships = Service(null);
        var build = ships.Intend("python")!;

        ships.StandardA(build.Id);

        Assert.Equal("Every core internal is already at Standard-A.", ships.StandardA(build.Id));
    }

    [Fact]
    public void ASpokenPhraseReachesTheProtectedTool()
    {
        var ships = Service(null);
        var capability = ShipsCapability.Create(ships);
        var router = new KeywordRouter(CapabilityRegistry.Build([capability]));

        var match = router.MatchToolCommand("standard-A rate this ship");

        Assert.Equal("standard_a_core_internals", match?.ToolName);
        Assert.True(capability.Tools.Single(tool => tool.Name == "standard_a_core_internals").Protected);
    }
}
