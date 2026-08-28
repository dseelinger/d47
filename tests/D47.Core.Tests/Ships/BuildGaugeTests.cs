using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// The two gauges at the head of a ship's slot list (Phase 38, "A build you can watch").
/// <para>
/// <b>The loadout below is a real one.</b> Cobra Mk V "Predestinatio", out of the journal corpus on
/// 24 August 2025, with the cosmetics taken out — a paint job and a bobble have no row in the
/// specification table and weigh nothing, so removing them changes no figure here and makes the
/// fixture readable. Every other number, including <c>MaxJumpRange</c>, is exactly what Frontier
/// wrote, which is what makes the first test below a comparison against the game rather than
/// against the table d47 read.
/// </para>
/// <para>
/// <b>The modifiers are trimmed to the ones these gauges read, and one of them is not obvious.</b>
/// This drive carries Deep Charge, which raises <c>MaxFuelPerJump</c> — so a fixture cut down to
/// "mass, power and optimal mass" produces a jump range 4% short of Frontier's and looks like a
/// modelling error. It is not: it is a fixture that dropped a figure the arithmetic uses.
/// </para>
/// <para>
/// The whole corpus goes through this same code in <c>spike/BuildGaugeProbe</c> — 2,876 loadouts,
/// median error 0.000% — and CI has no corpus, so this is the case pinned here.
/// </para>
/// </summary>
public class BuildGaugeTests
{
    private const string Predestinatio =
        """
        {"timestamp":"2025-08-24T16:16:08Z","event":"Loadout","Ship":"cobramkv","ShipID":0,"ShipName":"Predestinatio","ShipIdent":"CAL-PD","HullValue":1989460,"ModulesValue":5003675,"HullHealth":1.0,"UnladenMass":190.399994,"CargoCapacity":0,"MaxJumpRange":53.397007,"FuelCapacity":{"Main":16.0,"Reserve":0.49},"Rebuy":349658,"Modules":[{"Slot":"Armour","Item":"cobramkv_armour_grade1","On":true,"Priority":1,"Health":1.0},{"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":1,"Health":1.0,"Value":1441233,"Engineering":{"Engineer":"Hera Tani","EngineerID":300090,"BlueprintID":128673774,"BlueprintName":"PowerPlant_Stealth","Level":5,"Quality":1.0,"ExperimentalEffect":"special_powerplant_cooled","ExperimentalEffect_Localised":"Thermal Spread","Modifiers":[{"Label":"Mass","Value":6.0,"OriginalValue":5.0,"LessIsGood":1},{"Label":"PowerCapacity","Value":13.26,"OriginalValue":15.6,"LessIsGood":0}]}},{"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":0,"Health":1.0,"Value":1610080,"Engineering":{"Engineer":"Professor Palin","EngineerID":300220,"BlueprintID":128673669,"BlueprintName":"Engine_Tuned","Level":5,"Quality":1.0,"ExperimentalEffect":"special_engine_lightweight","ExperimentalEffect_Localised":"Stripped Down","Modifiers":[{"Label":"Mass","Value":9.0,"OriginalValue":10.0,"LessIsGood":1},{"Label":"Integrity","Value":73.920006,"OriginalValue":88.0,"LessIsGood":0},{"Label":"PowerDraw","Value":5.7072,"OriginalValue":4.92,"LessIsGood":1}]}},{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_overcharge_size4_class5","On":true,"Priority":0,"Health":1.0,"Value":1932096,"Engineering":{"Engineer":"Felicity Farseer","EngineerID":300100,"BlueprintID":128673694,"BlueprintName":"FSD_LongRange","Level":5,"Quality":1.0,"ExperimentalEffect":"special_fsd_fuelcapacity","ExperimentalEffect_Localised":"Deep Charge","Modifiers":[{"Label":"Mass","Value":13.0,"OriginalValue":10.0,"LessIsGood":1},{"Label":"Integrity","Value":85.0,"OriginalValue":100.0,"LessIsGood":0},{"Label":"PowerDraw","Value":0.543375,"OriginalValue":0.45,"LessIsGood":1},{"Label":"FSDOptimalMass","Value":906.75,"OriginalValue":585.0,"LessIsGood":0},{"Label":"MaxFuelPerJump","Value":3.52,"OriginalValue":3.2,"LessIsGood":0}]}},{"Slot":"LifeSupport","Item":"int_lifesupport_size3_class2","On":true,"Priority":0,"Health":1.0,"Value":10133},{"Slot":"PowerDistributor","Item":"int_powerdistributor_size4_class5","On":true,"Priority":0,"Health":1.0,"Engineering":{"EngineerID":399999,"BlueprintID":129035483,"BlueprintName":"PowerDistributor_PrioritySystems","Level":5,"Quality":1.0,"Modifiers":[]}},{"Slot":"Radar","Item":"int_sensors_size3_class2","On":true,"Priority":0,"Health":1.0,"Value":10133,"Engineering":{"Engineer":"Juri Ishmaak","EngineerID":300250,"BlueprintID":128740673,"BlueprintName":"Sensor_LightWeight","Level":5,"Quality":1.0,"Modifiers":[{"Label":"Mass","Value":0.4,"OriginalValue":2.0,"LessIsGood":1},{"Label":"Integrity","Value":25.5,"OriginalValue":51.0,"LessIsGood":0}]}},{"Slot":"FuelTank","Item":"int_fueltank_size4_class3","On":true,"Priority":1,"Health":1.0}]}
        """;

    private static ShipLoadout Flown(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));

        return ShipLoadout.Unknown.Apply(journalEvent!);
    }

    private static ShipBuild Build(ShipLoadout loadout, params SlotPlan[] plans) =>
        new("F1", "ship-1", loadout.Type ?? "unknown", loadout.ShipId, loadout.Name, plans);

    [Fact]
    public void TheBestNeedleIsTheFigureEliteReports()
    {
        var loadout = Flown(Predestinatio);
        var gauges = ShipGauges.Read(Build(loadout), loadout);

        Assert.NotNull(gauges.Jump);

        // **The anchor for the whole gauge.** A range that quietly disagrees with the number on
        // the outfitting screen looks broken, and this is that number: unladen mass plus one
        // jump's fuel, which is what MaxJumpRange turns out to mean.
        Assert.Equal(loadout.MaxJumpRange!.Value, gauges.Jump.Best, 2);

        // Measured, because nothing is planned: every figure came out of the event.
        Assert.Equal(FigureKind.Measured, gauges.Jump.Kind);
    }

    [Fact]
    public void TheThreeNeedlesDescendAsTheShipFillsUp()
    {
        var loadout = Flown(Predestinatio);
        var jump = ShipGauges.Read(Build(loadout), loadout).Jump;

        Assert.NotNull(jump);

        // Best is one jump's fuel, middle is the full 16 tonne tank, worst adds the hold — which
        // on this ship is empty, so the last two are the same and that is the honest answer rather
        // than an invented spread.
        Assert.True(jump.Best > jump.Middle);
        Assert.Equal(jump.Middle, jump.Worst, 6);
        Assert.Equal(0, loadout.CargoCapacity);
    }

    [Fact]
    public void ThePowerGaugeWeighsTheDrawAgainstThePlant()
    {
        var loadout = Flown(Predestinatio);
        var power = ShipGauges.Read(Build(loadout), loadout).Power;

        Assert.NotNull(power);

        // The plant's own roll moved its output down: 15.6 MW stock, 13.26 after a grade 5 Low
        // Emissions. Elite reported that figure and the gauge uses it rather than the table's.
        Assert.Equal(13.26, power.Capacity!.Value, 3);

        // Nothing on this ship is a hardpoint, so the two totals are the same — and saying so is
        // the test that the split is about what is fitted rather than about arithmetic.
        Assert.Equal(power.Retracted, power.Deployed, 6);
        Assert.True(power.Fits);
        Assert.Null(power.Overage);
    }

    [Fact]
    public void AShieldBoosterDrawsAllTheTimeAndAMultiCannonDoesNot()
    {
        // **The distinction hardest to keep in your head by hand.** A shield booster sits in a
        // TinyHardpoint slot, so it looks like a hardpoint; its module type is `usb`, a utility,
        // and it draws whether or not the guns are out.
        var loadout = Flown(
            """
            {"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":3,"UnladenMass":190.0,"MaxJumpRange":20.0,"CargoCapacity":0,"Modules":[
            {"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":1,"Health":1.0},
            {"Slot":"MediumHardpoint1","Item":"hpt_multicannon_fixed_medium","On":true,"Priority":1,"Health":1.0},
            {"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":1,"Health":1.0}]}
            """);

        var power = ShipGauges.Read(Build(loadout), loadout).Power;

        Assert.NotNull(power);

        var cannon = EliteSpecifications.Module("hpt_multicannon_fixed_medium")!.Power!.Value;
        var booster = EliteSpecifications.Module("hpt_shieldbooster_size0_class5")!.Power!.Value;

        Assert.Equal(booster, power.Retracted, 3);
        Assert.Equal(booster + cannon, power.Deployed, 3);
    }

    [Fact]
    public void AShipD47HasNeverBeenInsideSaysSoRatherThanDrawingAFigure()
    {
        var gauges = ShipGauges.Read(new ShipBuild("F1", "ship-2", "anaconda", 9), seen: null);

        Assert.Null(gauges.Power);
        Assert.Null(gauges.Jump);
        Assert.Equal(ShipGauges.Unseen, gauges.Silent);
    }

    [Fact]
    public void PlanningARollMovesTheGaugeAndMarksItModelled()
    {
        var loadout = Flown(Predestinatio);
        var measured = ShipGauges.Read(Build(loadout), loadout);

        // The same ship with a *planned* Armoured roll on the plant, which raises its output.
        var planned = ShipGauges.Read(
            Build(loadout, new SlotPlan("PowerPlant", "Armoured", 5)),
            loadout);

        Assert.NotNull(measured.Power);
        Assert.NotNull(planned.Power);

        Assert.Equal(FigureKind.Measured, measured.Power.Kind);
        Assert.Equal(FigureKind.Modelled, planned.Power.Kind);

        // Modelled off the module's stock output rather than off the roll already on it: a plan
        // replaces the engineering rather than stacking on top of it. 15.6 MW at +12%.
        Assert.Equal(15.6 * 1.12, planned.Power.Capacity!.Value, 3);
        Assert.True(planned.Power.Capacity > measured.Power.Capacity);
    }

    [Fact]
    public void APlanNamingNoSizeIsCountedRatherThanIgnored()
    {
        var loadout = Flown(Predestinatio);

        // "A shield generator, I do not mind which" is a real plan and has no figures at all.
        var gauges = ShipGauges.Read(
            Build(loadout, new SlotPlan("PowerDistributor", Module: "Power Distributor")),
            loadout);

        Assert.Equal(1, gauges.Unmodelled);

        // And the slot still weighs what is fitted, so the gauge is a complete total of a build
        // that exists rather than a partial total of one that does not.
        Assert.NotNull(gauges.Power);
        Assert.True(gauges.Power.Deployed > 0);
    }

    [Fact]
    public void ElitesOwnPerModuleFigureWinsOverTheTable()
    {
        var loadout = Flown(Predestinatio);

        // ModulesInfo.json, for the ship being flown. The drive's draw is deliberately not the
        // table's and not the roll's, so a gauge reading it can be told from one that is not.
        var measured = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["FrameShiftDrive"] = 9.99,
        };

        var plain = ShipGauges.Read(Build(loadout), loadout).Power!;
        var live = ShipGauges.Read(Build(loadout), loadout, measured).Power!;

        Assert.Equal(plain.Deployed - 0.543375 + 9.99, live.Deployed, 3);
    }

    [Fact]
    public void APlannedSlotIgnoresTheMeasuredFileBecauseItDescribesWhatIsThereNow()
    {
        var loadout = Flown(Predestinatio);

        var measured = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["FrameShiftDrive"] = 9.99,
        };

        var planned = ShipGauges.Read(
            Build(loadout, new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5)),
            loadout,
            measured).Power!;

        // The file says what is on the hull; a plan is by definition not that yet, so the modelled
        // figure is what the gauge shows for that slot.
        Assert.NotEqual(9.99, planned.Deployed, 3);
        Assert.Equal(FigureKind.Modelled, planned.Kind);
    }

    [Fact]
    public void AFileDescribingAnotherShipIsNotUsed()
    {
        var loadout = Flown(Predestinatio);

        var elsewhere = new ModulePower
        {
            ReadAt = DateTimeOffset.UnixEpoch,
            Items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FrameShiftDrive"] = "int_hyperdrive_size7_class5",
            },
        };

        // A Commander who swaps ships without re-outfitting leaves the previous ship's figures on
        // disk, and using those would put one hull's draw under another hull's plant.
        Assert.False(elsewhere.Describes(loadout));

        var mine = new ModulePower
        {
            ReadAt = DateTimeOffset.UnixEpoch,
            Items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FrameShiftDrive"] = "int_hyperdrive_overcharge_size4_class5",
            },
        };

        Assert.True(mine.Describes(loadout));
    }

    [Fact]
    public void TheReaderTakesBothFiguresAndSurvivesAModuleWithNeither()
    {
        var directory = Directory.CreateTempSubdirectory("d47-modulesinfo");

        try
        {
            // Real shape, including the entries that carry no Priority at all — the cockpit and the
            // cosmetics — which is a state and not a parse failure.
            File.WriteAllText(
                Path.Combine(directory.FullName, ModulePowerReader.FileName),
                """
                { "timestamp":"2026-08-21T01:36:30Z", "event":"ModuleInfo", "Modules":[
                { "Slot":"MainEngines", "Item":"int_engine_size7_class5", "Power":9.120000, "Priority":0 },
                { "Slot":"ShipCockpit", "Item":"corsair_cockpit", "Power":0.000000 },
                { "Slot":"PowerPlant", "Item":"int_powerplant_size7_class5", "Power":0.000000, "Priority":1 } ] }
                """);

            var reader = new ModulePowerReader(directory.FullName, NullLogger.Instance);

            Assert.True(reader.Poll());
            Assert.Equal(9.12, reader.Current.Draw["MainEngines"], 3);
            Assert.Equal(0, reader.Current.Priority["MainEngines"]);
            Assert.False(reader.Current.Priority.ContainsKey("ShipCockpit"));
            Assert.Equal("int_powerplant_size7_class5", reader.Current.Items["PowerPlant"]);

            // Read only when the write time moves, like every other reader in that namespace.
            Assert.False(reader.Poll());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ABuildThatDoesNotFitSaysByHowMuch()
    {
        // A 2E plant under a 4A thruster, drive, distributor and a Plasma Accelerator: over budget
        // once the guns come out, which is the case the split exists to catch. Both halves are what a Commander acts on — the percentage
        // says there is a problem and the megawatts say how big a plant fixes it.
        var loadout = Flown(
            """
            {"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":4,"UnladenMass":190.0,"MaxJumpRange":20.0,"CargoCapacity":0,"Modules":[
            {"Slot":"PowerPlant","Item":"int_powerplant_size2_class1","On":true,"Priority":1,"Health":1.0},
            {"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":1,"Health":1.0},
            {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_overcharge_size4_class5","On":true,"Priority":1,"Health":1.0},
            {"Slot":"PowerDistributor","Item":"int_powerdistributor_size4_class5","On":true,"Priority":1,"Health":1.0},
            {"Slot":"MediumHardpoint1","Item":"hpt_plasmaaccelerator_fixed_medium","On":true,"Priority":1,"Health":1.0}]}
            """);

        var power = ShipGauges.Read(Build(loadout), loadout).Power;

        Assert.NotNull(power);
        Assert.False(power.Fits);
        Assert.NotNull(power.Overage);
        Assert.Equal(power.Deployed - power.Capacity!.Value, power.Overage.Value, 6);
        Assert.True(power.DeployedShare > 1);
    }

    [Fact]
    public void ABuildWithNoPlantSaysThatRatherThanZero()
    {
        var loadout = Flown(
            """
            {"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":5,"UnladenMass":190.0,"MaxJumpRange":20.0,"CargoCapacity":0,"Modules":[
            {"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":1,"Health":1.0}]}
            """);

        var power = ShipGauges.Read(Build(loadout), loadout).Power;

        Assert.NotNull(power);
        Assert.Null(power.Capacity);
        Assert.Null(power.RetractedShare);

        // Not fitting is a comparison, and there is nothing to compare against — so it does not
        // claim the build fails.
        Assert.True(power.Fits);
        Assert.Null(power.Overage);
    }
}
