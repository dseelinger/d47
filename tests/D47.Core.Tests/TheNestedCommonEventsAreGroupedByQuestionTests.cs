using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The seven common journal events that carry nested lists, read into rows that answer a question (#816).</summary>
public class TheNestedCommonEventsAreGroupedByQuestionTests
{
    private const string FsdJump =
        """{ "timestamp":"2026-09-28T14:05:35Z", "event":"FSDJump", "Taxi":false, "Multicrew":false, "StarSystem":"Kokoimudji", "SystemAddress":2870783124945, "StarPos":[69.31250,18.12500,98.40625], "SystemAllegiance":"Independent", "SystemEconomy":"$economy_Industrial;", "SystemEconomy_Localised":"Industrial", "SystemSecondEconomy":"$economy_Extraction;", "SystemSecondEconomy_Localised":"Extraction", "SystemGovernment":"$government_Democracy;", "SystemGovernment_Localised":"Democracy", "SystemSecurity":"$SYSTEM_SECURITY_high;", "SystemSecurity_Localised":"High Security", "Population":19812755, "Body":"Kokoimudji A", "BodyID":2, "BodyType":"Star", "ControllingPower":"Edmund Mahon", "Powers":[ "Aisling Duval", "Edmund Mahon", "Nakato Kaine" ], "PowerplayState":"Fortified", "PowerplayStateControlProgress":0.254882, "PowerplayStateReinforcement":603, "PowerplayStateUndermining":529, "JumpDist":77.985, "FuelUsed":6.645944, "FuelLevel":121.354057, "Factions":[ { "Name":"CD-44 10336 Industries", "FactionState":"None", "Government":"Corporate", "Influence":0.170489, "Allegiance":"Federation", "Happiness":"$Faction_HappinessBand2;", "Happiness_Localised":"Happy", "MyReputation":0.000000 }, { "Name":"Social Kokoimudji Independents", "FactionState":"None", "Government":"Democracy", "Influence":0.565304, "Allegiance":"Independent", "Happiness":"$Faction_HappinessBand2;", "Happiness_Localised":"Happy", "MyReputation":0.000000 } ], "SystemFaction":{ "Name":"Social Kokoimudji Independents" } }""";

    private const string StarScan =
        """{ "timestamp":"2026-09-26T23:44:41Z", "event":"Scan", "ScanType":"AutoScan", "BodyName":"Col 285 Sector YB-G b26-5 A", "BodyID":2, "Parents":[ {"Null":1}, {"Null":0} ], "StarSystem":"Col 285 Sector YB-G b26-5", "SystemAddress":11667950085625, "DistanceFromArrivalLS":0.000000, "StarType":"M", "Subclass":7, "StellarMass":0.261719, "Radius":295196928.000000, "AbsoluteMagnitude":10.524307, "Age_MY":2424, "SurfaceTemperature":2391.000000, "Luminosity":"Va", "SemiMajorAxis":6364106178.283691, "Eccentricity":0.690549, "OrbitalInclination":18.690855, "Periapsis":16.159620, "OrbitalPeriod":2845336.139202, "AscendingNode":-86.390688, "MeanAnomaly":342.786037, "RotationPeriod":191858.452547, "AxialTilt":0.000000, "WasDiscovered":true, "WasMapped":false, "WasFootfalled":false }""";

    private const string PlanetScan =
        """{ "timestamp":"2026-09-26T23:51:32Z", "event":"Scan", "ScanType":"AutoScan", "BodyName":"Col 285 Sector QO-G c11-7 2", "BodyID":9, "Parents":[ {"Star":0} ], "StarSystem":"Col 285 Sector QO-G c11-7", "SystemAddress":2008199271146, "DistanceFromArrivalLS":19.050939, "TidalLock":true, "TerraformState":"", "PlanetClass":"High metal content body", "Atmosphere":"", "AtmosphereType":"None", "Volcanism":"", "MassEM":1.076557, "Radius":6209090.500000, "SurfaceGravity":11.129879, "SurfaceTemperature":689.080200, "SurfacePressure":0.000000, "Landable":true, "Materials":[ { "Name":"iron", "Percent":23.620602 }, { "Name":"nickel", "Percent":17.865635 } ], "Composition":{ "Ice":0.000000, "Rock":0.666132, "Metal":0.333868 }, "SemiMajorAxis":5711399078.369141, "Eccentricity":0.000013, "OrbitalInclination":0.000789, "Periapsis":223.681834, "OrbitalPeriod":309083.896875, "AscendingNode":44.405054, "MeanAnomaly":14.928479, "RotationPeriod":310637.444556, "AxialTilt":0.407097, "WasDiscovered":true, "WasMapped":true, "WasFootfalled":true }""";

    private const string Loadout =
        """{ "timestamp":"2026-10-05T01:01:57Z", "event":"Loadout", "Ship":"sidewinder", "ShipID":0, "ShipName":"Test Ship", "ShipIdent":"TS-01", "HullHealth":1.000000, "UnladenMass":53.399998, "CargoCapacity":2, "MaxJumpRange":7.745967, "FuelCapacity":{ "Main":2.000000, "Reserve":0.300000 }, "Rebuy":0, "Modules":[ { "Slot":"SmallHardpoint1", "Item":"hpt_pulselaser_gimbal_small", "On":true, "Priority":2, "Health":1.000000 }, { "Slot":"PowerPlant", "Item":"int_powerplant_size2_class1", "On":true, "Priority":1, "Health":1.000000 }, { "Slot":"MainEngines", "Item":"int_engine_size2_class1", "On":true, "Priority":0, "Health":1.000000 }, { "Slot":"FrameShiftDrive", "Item":"int_hyperdrive_size2_class2", "On":true, "Priority":2, "Health":1.000000 }, { "Slot":"Slot04_Size1", "Item":"int_cargorack_size1_class1", "On":true, "Priority":1, "Health":1.000000 } ] }""";

    private const string Cargo =
        """{ "timestamp":"2026-09-23T20:21:04Z", "event":"Cargo", "Vessel":"Ship", "Count":1, "Inventory":[ { "Name":"metaalloys", "Name_Localised":"Meta-Alloys", "Count":1, "Stolen":0 } ] }""";

    private const string EngineerCraft =
        """{ "timestamp":"2026-09-20T00:38:02Z", "event":"EngineerCraft", "Slot":"MainEngines", "Module":"int_engine_size4_class5", "ApplyExperimentalEffect":"special_engine_overloaded", "Ingredients":[ { "Name":"iron", "Count":5 }, { "Name":"hybridcapacitors", "Name_Localised":"Hybrid Capacitors", "Count":3 }, { "Name":"securityfirmware", "Name_Localised":"Security Firmware Patch", "Count":1 } ], "Engineer":"Professor Palin", "EngineerID":300220, "BlueprintID":128673659, "BlueprintName":"Engine_Dirty", "Level":5, "Quality":1.000000, "ExperimentalEffect":"special_engine_overloaded", "ExperimentalEffect_Localised":"Drag Drives", "Modifiers":[ { "Label":"Integrity", "Value":74.800003, "OriginalValue":88.000000, "LessIsGood":0 }, { "Label":"PowerDraw", "Value":5.510400, "OriginalValue":4.920000, "LessIsGood":1 } ] }""";

    private const string CommunityGoal =
        """{ "timestamp":"2026-09-30T14:06:02Z", "event":"CommunityGoal", "CurrentGoals":[ { "CGID":859, "Title":"Wreaken Calls for Sourced Materials for Output Quality Comparison Tests", "SystemName":"Ega", "MarketName":"Metz Enterprise", "Expiry":"2026-09-17T11:00:00Z", "IsComplete":true, "CurrentTotal":140456277, "PlayerContribution":0, "NumContributors":11031, "TopTier":{ "Name":"Tier 5", "Bonus":"" }, "TopRankSize":10, "PlayerInTopRank":false, "TierReached":"Tier 3", "PlayerPercentileBand":100, "Bonus":45000000 } ] }""";

    private const string ConstructionDepot =
        """{ "timestamp":"2025-12-23T22:39:41Z", "event":"ColonisationConstructionDepot", "MarketID":3959687426, "ConstructionProgress":0.375000, "ConstructionComplete":false, "ConstructionFailed":false, "ResourcesRequired":[ { "Name":"$ceramiccomposites_name;", "Name_Localised":"Ceramic Composites", "RequiredAmount":497, "ProvidedAmount":497, "Payment":724 }, { "Name":"$cmmcomposite_name;", "Name_Localised":"CMM Composite", "RequiredAmount":3912, "ProvidedAmount":12, "Payment":6788 }, { "Name":"$copper_name;", "Name_Localised":"Copper", "RequiredAmount":218, "ProvidedAmount":0, "Payment":1050 } ] }""";

    private static readonly string[] Events =
        [FsdJump, StarScan, PlanetScan, Loadout, Cargo, EngineerCraft, CommunityGoal, ConstructionDepot];

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
    public void EachOfTheSevenKindsHasRowsAndOneEntryPerField()
    {
        foreach (var json in Events)
        {
            var reading = Read(json);

            using var document = JsonDocument.Parse(json);

            Assert.NotEmpty(reading.Rows);
            Assert.Equal(document.RootElement.EnumerateObject().Count(), reading.Plumbing.Count);
        }
    }

    [Fact]
    public void AJumpReadsAsTheSystemTheDistanceAndWhoRunsIt()
    {
        var jump = Read(FsdJump);

        Assert.Equal(
            ["System", "Jump distance", "Fuel used", "Fuel left", "Controlled by", "Government", "Security", "Economy", "Population", "Controlling power"],
            jump.Rows.Select(row => row.Label));
        Assert.Equal("Kokoimudji", Joined(jump, "System"));
        Assert.Equal(ReadingTone.System, Assert.Single(Row(jump, "System").Values).Tone);
        Assert.Equal("77.99 ly", Joined(jump, "Jump distance"));
        Assert.Equal("6.65 t", Joined(jump, "Fuel used"));
        Assert.Equal("121.35 t", Joined(jump, "Fuel left"));
        Assert.Equal("Social Kokoimudji Independents", Joined(jump, "Controlled by"));
        Assert.Equal(ReadingTone.Name, Assert.Single(Row(jump, "Controlled by").Values).Tone);
        Assert.Equal("Democracy", Joined(jump, "Government"));
        Assert.Equal("High Security", Joined(jump, "Security"));
        Assert.Equal("Industrial", Joined(jump, "Economy"));
        Assert.Equal("19,812,755", Joined(jump, "Population"));
        Assert.Equal("Edmund Mahon", Joined(jump, "Controlling power"));
        Assert.Equal(ReadingTone.Name, Assert.Single(Row(jump, "Controlling power").Values).Tone);
    }

    [Fact]
    public void AStarScanReadsAsTheBodyItsClassItsAgeAndItsMass()
    {
        var star = Read(StarScan);

        Assert.Equal(
            ["Body", "Distance from arrival", "Discovered", "Mapped", "Star class", "Age", "Mass"],
            star.Rows.Select(row => row.Label));
        Assert.Equal("0 ls", Joined(star, "Distance from arrival"));
        Assert.Equal("Already discovered", Joined(star, "Discovered"));
        Assert.Equal("Not yet mapped", Joined(star, "Mapped"));
        Assert.Equal("M7", Joined(star, "Star class"));
        Assert.Equal([new ReadingRun("M", false), new ReadingRun("7", true)], Assert.Single(Row(star, "Star class").Values).Runs);
        Assert.Equal("2,424 million years", Joined(star, "Age"));
        Assert.Equal("0.26 solar masses", Joined(star, "Mass"));
    }

    [Fact]
    public void AnOrbitalElementIsNeverARow()
    {
        string[] orbital = ["Semi major axis", "Eccentricity", "Orbital inclination", "Periapsis", "Ascending node", "Mean anomaly", "Parents"];

        foreach (var json in new[] { StarScan, PlanetScan })
        {
            var labels = Read(json).Rows.Select(row => row.Label).ToList();

            Assert.All(orbital, name => Assert.DoesNotContain(name, labels));
        }
    }

    [Fact]
    public void APlanetScanReadsAsTheBodyItsClassItsGravityAndItsAir()
    {
        var planet = Read(PlanetScan);

        Assert.Equal(
            ["Body", "Distance from arrival", "Discovered", "Mapped", "Planet class", "Landing", "Gravity", "Atmosphere", "Volcanism", "Terraform state"],
            planet.Rows.Select(row => row.Label));
        Assert.Equal("19.05 ls", Joined(planet, "Distance from arrival"));
        Assert.Equal("Already mapped", Joined(planet, "Mapped"));
        Assert.Equal("High metal content body", Joined(planet, "Planet class"));
        Assert.Equal("Landable", Joined(planet, "Landing"));
        Assert.Equal("1.13 g", Joined(planet, "Gravity"));
        Assert.Equal("None", Joined(planet, "Atmosphere"));
        Assert.Equal("None", Joined(planet, "Volcanism"));
        Assert.Equal("Not terraformable", Joined(planet, "Terraform state"));
    }

    [Fact]
    public void ALoadoutNamesItsHullAndLinksTheShipByItsId()
    {
        var loadout = Read(Loadout);
        var hull = Assert.Single(Row(loadout, "Ship").Values);

        Assert.Equal("Sidewinder", hull.Text);
        Assert.Equal(new ReadingLink(ReadingLinkKind.Ship, "0"), hull.Link);
        Assert.Equal("Test Ship", Joined(loadout, "Name"));
        Assert.True(Assert.Single(Row(loadout, "Name").Values).Typed);
        Assert.Equal("TS-01", Joined(loadout, "Ident"));
        Assert.Equal("7.75 ly", Joined(loadout, "Jump range"));
        Assert.Equal("2 t", Joined(loadout, "Cargo"));
        Assert.StartsWith("0", Joined(loadout, "Rebuy"));
    }

    [Fact]
    public void ALoadoutListsEveryModuleInOneRowWithItsSizeAsANumber()
    {
        var modules = Row(Read(Loadout), "Modules").Values;

        Assert.Equal(5, modules.Count);
        Assert.Contains(modules, module => module.Runs.Any(run => run.Number));
        Assert.All(modules, module => Assert.Equal(module.Text, string.Concat(module.Runs.Select(run => run.Text))));
        Assert.DoesNotContain(modules, module => module.Text.Contains('_'));
    }

    [Fact]
    public void ACargoReadingNamesTheVesselTheTotalAndEachItem()
    {
        var cargo = Read(Cargo);

        Assert.Equal("Ship", Joined(cargo, "Vessel"));
        Assert.Equal("1", Joined(cargo, "Total"));
        Assert.Equal("Meta-Alloys ×1", Joined(cargo, "Items"));
        Assert.Equal([new ReadingRun("Meta-Alloys ×", false), new ReadingRun("1", true)], Assert.Single(Row(cargo, "Items").Values).Runs);
    }

    [Fact]
    public void AnEngineerCraftNamesTheEngineerAndLinksThemById()
    {
        var craft = Read(EngineerCraft);
        var engineer = Assert.Single(Row(craft, "Engineer").Values);

        Assert.Equal("Professor Palin", engineer.Text);
        Assert.Equal(new ReadingLink(ReadingLinkKind.Engineer, "300220"), engineer.Link);
        Assert.Equal(ReadingTone.Name, engineer.Tone);
        Assert.DoesNotContain('_', Joined(craft, "Module"));
        Assert.Equal("Engine Dirty, grade 5", Joined(craft, "Blueprint"));
        Assert.Equal("Drag Drives", Joined(craft, "Effect"));
        Assert.Equal("Iron ×5 · Hybrid Capacitors ×3 · Security Firmware Patch ×1", Joined(craft, "Ingredients"));
    }

    [Fact]
    public void AnEngineerCraftWithoutAnExperimentalEffectHasNoEffectRow()
    {
        var plain = EngineerCraft.Replace("\"ExperimentalEffect\":\"special_engine_overloaded\", \"ExperimentalEffect_Localised\":\"Drag Drives\", ", string.Empty);

        Assert.DoesNotContain(Read(plain).Rows, row => row.Label == "Effect");
    }

    [Fact]
    public void ACommunityGoalReadsAsItsTitleAndTheTierReached()
    {
        var goal = Read(CommunityGoal);
        var value = Assert.Single(Row(goal, "Goals").Values);

        Assert.Equal("Wreaken Calls for Sourced Materials for Output Quality Comparison Tests — Tier 3", value.Text);
        Assert.Equal(new ReadingRun("3", true), value.Runs[^1]);
    }

    [Fact]
    public void ACommunityGoalMentionsTheContributionOnlyAboveZero()
    {
        var gave = CommunityGoal.Replace("\"PlayerContribution\":0", "\"PlayerContribution\":1250");

        Assert.EndsWith("Tier 3 — you gave 1,250", Joined(Read(gave), "Goals"));
        Assert.DoesNotContain("you gave", Joined(Read(CommunityGoal), "Goals"));
    }

    [Fact]
    public void AConstructionDepotReadsAsProgressStatusAndWhatIsStillShort()
    {
        var depot = Read(ConstructionDepot);

        Assert.Equal("37.5%", Joined(depot, "Progress"));
        Assert.Equal("In progress", Joined(depot, "Status"));
        Assert.Equal("2 of 3 resources", Joined(depot, "Resources short"));
        Assert.Equal(
            [new ReadingRun("2", true), new ReadingRun(" of ", false), new ReadingRun("3", true), new ReadingRun(" resources", false)],
            Assert.Single(Row(depot, "Resources short").Values).Runs);
    }

    [Fact]
    public void AConstructionDepotSaysWhenItIsCompleteOrFailed()
    {
        var complete = ConstructionDepot.Replace("\"ConstructionComplete\":false", "\"ConstructionComplete\":true");
        var failed = ConstructionDepot.Replace("\"ConstructionFailed\":false", "\"ConstructionFailed\":true");

        Assert.Equal("Complete", Joined(Read(complete), "Status"));
        Assert.Equal("Failed", Joined(Read(failed), "Status"));
        Assert.Equal(ReadingTone.Warning, Assert.Single(Row(Read(failed), "Status").Values).Tone);
    }
}
