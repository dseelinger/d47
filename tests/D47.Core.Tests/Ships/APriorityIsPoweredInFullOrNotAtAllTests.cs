using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>The power priorities against the 3a handoff prototype's 22 modules and 22.93 MW plant (#467).</summary>
public class APriorityIsPoweredInFullOrNotAtAllTests
{
    private const double Plant = 22.93;

    private static readonly PowerModule[] Prototype =
    [
        Module("pd", "Power Distributor", 0.97, 1, PowerRole.Core),
        Module("ls", "Life Support", 0.67, 1, PowerRole.Life),
        Module("fsd", "Frame Shift Drive", 0.75, 1, PowerRole.Core),
        Module("sen", "Sensors", 0.69, 1, PowerRole.Core),
        Module("thr", "Thrusters", 6.70, 2, PowerRole.Core),
        Module("sg", "Shield Generator", 4.30, 3, PowerRole.Keep),
        Module("scb", "Shield Cell Bank", 1.18, 3, PowerRole.Keep),
        Module("gfb", "Guardian FSD Booster", 0.75, 4, PowerRole.Depends),
        Module("fs", "Fuel Scoop", 0.52, 4, PowerRole.Shed),
        Module("afm", "Auto Field-Maintenance Unit", 0.94, 5, PowerRole.Shed),
        Module("ch", "Cargo Hatch", 0.60, 5, PowerRole.Shed),
        Module("sb1", "Shield Booster", 1.20, 4, PowerRole.Keep),
        Module("sb2", "Shield Booster", 1.20, 4, PowerRole.Keep),
        Module("sb3", "Shield Booster", 1.20, 4, PowerRole.Keep),
        Module("hs", "Heat Sink Launcher", 0.20, 5, PowerRole.Depends),
        Module("pdt", "Point Defence", 0.20, 5, PowerRole.Keep),
        Module("cf", "Chaff Launcher", 0.20, 5, PowerRole.Keep),
        Module("ws", "Wake Scanner", 0.20, 5, PowerRole.Keep),
        Module("bl1", "Beam Laser", 0.80, 4, PowerRole.Keep, hardpoint: true),
        Module("bl2", "Beam Laser", 0.85, 4, PowerRole.Keep, hardpoint: true),
        Module("mc1", "Multi-Cannon", 0.46, 4, PowerRole.Keep, hardpoint: true),
        Module("mc2", "Multi-Cannon", 0.46, 4, PowerRole.Keep, hardpoint: true),
    ];

    private static readonly string[] Order = Prototype.Select(module => module.Slot).ToArray();

    private static PowerModule Module(
        string slot, string name, double megawatts, int priority, PowerRole role, bool hardpoint = false) =>
        new(slot, name, megawatts, hardpoint, priority, role);

    private static PowerPriorities Deployed() => PowerPriorities.Of(Prototype, Plant, order: Order);

    [Fact]
    public void TheDeployedBuildIsOverAndTheRetractedOneFits()
    {
        var power = Deployed();

        Assert.False(power.Deployed.Fits);
        Assert.Equal(2.11, power.Deployed.Overage, 2);
        Assert.True(power.Retracted.Fits);
        Assert.Equal(0, power.Retracted.Overage);
    }

    [Fact]
    public void EachOutputLevelKeepsTheLeadingPrioritiesThatFit()
    {
        Assert.Equal(["P1–4", "P1–2", "P1", "P1"], Deployed().Lines.Select(line => line.Powered));
    }

    [Fact]
    public void TheFullOutputLineSelectsP5()
    {
        Assert.Equal(5, Deployed().DefaultSelected);
    }

    [Fact]
    public void TheCheckFlagsWhatAFightNeedsAndPassesWhatItDoesNot()
    {
        var power = Deployed();

        Assert.Equal("DEPLOYED · P5 UNPOWERED", power.CheckHead);

        var off = power.Checks.Where(row => row.Tag == CheckTag.Off).ToList();
        Assert.Equal(["Point Defence", "Chaff Launcher", "Wake Scanner"], off.Select(row => row.Module.Name));
        Assert.All(off, row => Assert.Equal(4, row.MoveTo));

        var check = Assert.Single(power.Checks, row => row.Tag == CheckTag.Check);
        Assert.Equal("Heat Sink Launcher", check.Module.Name);
        Assert.Null(check.MoveTo);

        Assert.Equal(
            ["Fuel Scoop", "Auto Field-Maintenance Unit", "Cargo Hatch"],
            power.Checks.Where(row => row.Tag == CheckTag.Ok).Select(row => row.Module.Name));

        Assert.DoesNotContain(power.Checks, row => row.Tag == CheckTag.AtRisk);

        // Problems first.
        Assert.Equal(
            power.Checks.OrderBy(row => row.IsProblem ? 0 : 1).Select(row => row.Module.Slot),
            power.Checks.Select(row => row.Module.Slot));
    }

    [Fact]
    public void WithP3SelectedTheHalfOutputLineFallsInsideTheShieldGenerator()
    {
        var drill = Deployed().Drill(3);

        var crossing = Assert.Single(drill.Crossings);
        Assert.Equal(0.5, crossing.Line.Level);
        Assert.Equal("Shield Generator", crossing.Module.Name);

        Assert.Equal(9.78, drill.Band.Start, 2);
        Assert.Equal(15.26, drill.Band.Cumulative, 2);
        Assert.Equal(drill.Band.Start, drill.Spans[0].Start, 6);
        Assert.Equal(drill.Band.Cumulative, drill.Spans[^1].End, 6);
    }

    [Fact]
    public void RetractedLeavesTheHardpointsOutOfTheStackButNotOutOfTheCheck()
    {
        var retracted = PowerPriorities.Of(Prototype, Plant, retracted: true, order: Order);

        Assert.Equal(["P1–5", "P1–2", "P1", "P1"], retracted.Lines.Select(line => line.Powered));
        Assert.Equal(5, retracted.DefaultSelected);
        Assert.Equal(4, retracted.Drill(4).Stowed.Count);
        Assert.Equal("DEPLOYED · P5 UNPOWERED", retracted.CheckHead);
    }

    [Fact]
    public void AMoveToThePriorityAModuleIsAlreadyInIsNotOffered()
    {
        // Everything in P1 and over the plant: nothing is powered, so the last powered priority is P1.
        var crowded = Prototype.Select(module => module with { Priority = 1 }).ToList();
        var power = PowerPriorities.Of(crowded, Plant);

        Assert.Equal("DEPLOYED · P1–5 UNPOWERED", power.CheckHead);

        var off = power.Checks.Where(row => row.Tag == CheckTag.Off).ToList();
        Assert.NotEmpty(off);
        Assert.All(off, row => Assert.Null(row.MoveTo));
    }

    [Fact]
    public void LifeSupportOutsideTheLowestDamageLevelIsAtRisk()
    {
        var moved = Prototype.Select(module => module.Slot == "ls" ? module with { Priority = 3 } : module).ToList();
        var power = PowerPriorities.Of(moved, Plant);

        var row = Assert.Single(power.Checks, row => row.Tag == CheckTag.AtRisk);
        Assert.Equal("Life Support", row.Module.Name);
        Assert.Equal(1, row.MoveTo);
        Assert.True(row.IsProblem);
    }

    [Fact]
    public void AnOrderWithinAPriorityMovesWhichModuleTheLineCrosses()
    {
        var reordered = PowerPriorities.Of(Prototype, Plant, order: ["scb", "sg"]);

        Assert.Equal(["Shield Cell Bank", "Shield Generator"], reordered.Drill(3).Spans.Select(span => span.Module.Name));
        Assert.Equal("Shield Generator", Assert.Single(reordered.Drill(3).Crossings).Module.Name);

        // 11.465 MW, and P3 starts at 9.78: the cell bank covers only up to 10.96.
        var bank = reordered.Drill(3).Spans[0];
        Assert.Equal(10.96, bank.End, 2);
    }

    [Fact]
    public void TheModuleListSumsToTheGauge()
    {
        Assert.True(JournalEvent.TryParse(
            """
            {"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":3,"UnladenMass":190.0,"MaxJumpRange":20.0,"CargoCapacity":0,"Modules":[
            {"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":0,"Health":1.0},
            {"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":0,"Health":1.0},
            {"Slot":"LifeSupport","Item":"int_lifesupport_size3_class2","On":true,"Priority":1,"Health":1.0},
            {"Slot":"MediumHardpoint1","Item":"hpt_multicannon_fixed_medium","On":true,"Priority":2,"Health":1.0},
            {"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":3,"Health":1.0}]}
            """,
            NullLogger.Instance,
            out var journalEvent));

        var loadout = ShipLoadout.Unknown.Apply(journalEvent!);
        var power = ShipGauges.Read(new ShipBuild("F1", "ship-1", loadout.Type!, loadout.ShipId), loadout).Power!;

        Assert.Equal(power.Deployed, power.Modules.Sum(module => module.Megawatts), 6);
        Assert.Equal(power.Retracted, power.Modules.Where(module => !module.IsHardpoint).Sum(module => module.Megawatts), 6);
        Assert.Equal(power.Draw.Keys, power.Modules.Select(module => module.Slot));

        var cannon = Assert.Single(power.Modules, module => module.Slot == "MediumHardpoint1");
        Assert.True(cannon.IsHardpoint);
        Assert.Equal(PowerRole.Keep, cannon.Role);
        Assert.Equal(3, cannon.Priority);

        var booster = Assert.Single(power.Modules, module => module.Slot == "TinyHardpoint1");
        Assert.False(booster.IsHardpoint);
        Assert.Equal(PowerRole.Keep, booster.Role);

        Assert.Equal(PowerRole.Core, power.Modules.Single(module => module.Slot == "MainEngines").Role);
        Assert.Equal(PowerRole.Life, power.Modules.Single(module => module.Slot == "LifeSupport").Role);
    }
}
