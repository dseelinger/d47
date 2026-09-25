using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>The route ranks by tier first, then by value per jump, taking one unlock at a time (#476).</summary>
public class AnUnlockIsRankedByEffortThenValueTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState State(string starPos, double range, string engineers, params string[] extra)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     $$"""{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Somewhere","StarPos":[{{starPos}}],"Docked":true,"StationName":"Anywhere"}""",
                     $$"""{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":{{range.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"Modules":[]}""",
                     $$"""{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{{engineers}}]}""",
                 }.Concat(extra))
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private const string Nobody = """{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Known"}""";

    private static string Unlocked(string name, int id) =>
        $$"""{"Engineer":"{{name}}","EngineerID":{{id}},"Progress":"Unlocked","RankProgress":0,"Rank":1}""";

    private static ShipBuild Build(string id, params SlotPlan[] slots) =>
        new("F1", id, "python", 12, "Bad Idea", slots);

    private static SlotPlan[] Multicannons(int count) =>
        [.. Enumerable.Range(1, count).Select(index =>
            new SlotPlan($"Hardpoint{index}", "Long Range Weapon", 5, Module: "Multi-cannon"))];

    private static UnlockCandidate Named(EngineerReport report, string name) =>
        report.Route.Single(candidate => candidate.Engineer.Name == name);

    /// <summary>
    /// Felicity Farseer, already unlocked, is a rank climb and nothing more; Elvira Martuuk covers more
    /// but her invitation cannot be read as met.
    /// </summary>
    [Fact]
    public void ATierZeroUnlockRanksAboveATierTwoOneThatCoversMore()
    {
        var report = UnlockPlanner.Of(
            [
                Build("ship-1",
                    new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5, Module: "Frame Shift Drive"),
                    new SlotPlan("ShieldGenerator", "Reinforced", 3, Module: "Shield Generator")),
                Build("ship-2", new SlotPlan("ShieldGenerator", "Kinetic Resistant", 3, Module: "Shield Generator")),
            ],
            [],
            State("0,0,0", 30, Unlocked("Felicity Farseer", 300100)));

        var farseer = Named(report, "Felicity Farseer");
        var martuuk = Named(report, "Elvira Martuuk");

        Assert.Equal(0, farseer.Tier);
        Assert.Equal(2, martuuk.Tier);
        Assert.True(martuuk.Covers.Count > farseer.Covers.Count);
        Assert.True(martuuk.Value > farseer.Value);
        Assert.Equal("Felicity Farseer", report.Route[0].Engineer.Name);
    }

    /// <summary>Exploration rank Pathfinder meets Farseer's invitation; the Meta-alloys are not aboard.</summary>
    [Fact]
    public void AMetMeetingIsNotTierTwo()
    {
        var report = UnlockPlanner.Of(
            [Build("ship-1", new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5, Module: "Frame Shift Drive"))],
            [],
            State(
                "0,0,0",
                30,
                Nobody,
                """{"timestamp":"2026-08-18T09:00:00Z","event":"Rank","Combat":0,"Trade":0,"Explore":5,"Soldier":0,"Exobiologist":0,"Empire":0,"Federation":0,"CQC":0}"""));

        var farseer = Named(report, "Felicity Farseer");

        Assert.Contains(farseer.Criteria, criterion => criterion.Text == farseer.Engineer.Meeting && criterion.Met == true);
        Assert.Equal(1, farseer.Tier);
    }

    [Fact]
    public void AHandOverAboardIsHeld()
    {
        var farseer = EngineerDirectory.ByName("Felicity Farseer")!;
        var evidence = D47.Core.Engineers.UnlockEvidence.From(null);

        Assert.False(EngineerAccess.HandOverHeld(farseer, evidence, CargoHold.Empty, null));
        Assert.True(EngineerAccess.HandOverHeld(
            farseer, evidence, new CargoHold { Items = [new CargoItem("metaalloys", 1)] }, null));
    }

    [Fact]
    public void ABountyHandOverIsNeverHeld()
    {
        var tod = EngineerDirectory.ByName("Tod 'The Blaster' McQuinn")!;

        Assert.False(EngineerAccess.HandOverHeld(tod, D47.Core.Engineers.UnlockEvidence.From(null), CargoHold.Empty, null));
    }

    /// <summary>Twenty multi-cannons wanting the same roll are one job over twenty slots.</summary>
    [Fact]
    public void TwentySlotsOfOneRollAreOneJob()
    {
        var report = UnlockPlanner.Of(
            [Build("ship-1", Multicannons(20))],
            [],
            State("0,0,0", 30, Unlocked("Tod 'The Blaster' McQuinn", 300260)));

        var tod = Named(report, "Tod 'The Blaster' McQuinn");

        Assert.Single(tod.Jobs);
        Assert.Equal(20, tod.Covers.Count);
        Assert.Equal(1 + Math.Log2(21) + 1, tod.Value, 6);

        Assert.Contains("1 planned job covered", tod.Summary(), StringComparison.Ordinal);
        Assert.Equal(
            ["    covers: grade 5 Long Range Weapon ×20"],
            tod.Working().Where(line => line.StartsWith("    covers:", StringComparison.Ordinal)));
    }

    /// <summary>Marsha Hicks rolls the same multi-cannons Tod McQuinn does, and gets no credit for them after him.</summary>
    [Fact]
    public void AJobCoveredByABetterUnlockIsNotCountedAgain()
    {
        var report = UnlockPlanner.Of(
            [Build("ship-1", Multicannons(3))],
            [],
            State("0,0,0", 30, Unlocked("Tod 'The Blaster' McQuinn", 300260)));

        var tod = Named(report, "Tod 'The Blaster' McQuinn");
        var hicks = Named(report, "Marsha Hicks");

        Assert.Equal(3, tod.Covers.Count);
        Assert.Empty(hicks.Covers);
        Assert.Equal(0, hicks.Value);
        Assert.True(report.Route.ToList().IndexOf(tod) < report.Route.ToList().IndexOf(hicks));
    }

    /// <summary>
    /// A: Tod McQuinn, 4 jumps, grade 5 Long Range on 20 multi-cannons in a build with 5 blocked jobs.
    /// B: Lori Jameson, 2 jumps, grade 4 Lightweight Life Support in 3 builds, the only blocked job in
    /// each. V(A) = 5.59, score 1.12; V(B) = 6, score 2.0.
    /// </summary>
    [Fact]
    public void TheWorkedExampleRanksLifeSupportFirst()
    {
        SlotPlan LifeSupport() => new("LifeSupport", "Lightweight", 4, Module: "Life Support");

        var report = UnlockPlanner.Of(
            [
                Build("ship-a",
                [
                    .. Multicannons(20),
                    new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5, Module: "Frame Shift Drive"),
                    new SlotPlan("Armour", "Heavy Duty", 5, Module: "Armour"),
                    new SlotPlan("PowerPlant", "Overcharged", 5, Module: "Power Plant"),
                    new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, Module: "Thrusters"),
                ]),
                Build("ship-b1", LifeSupport()),
                Build("ship-b2", LifeSupport()),
                Build("ship-b3", LifeSupport()),
            ],
            [],
            State(
                "70.616,-40.809,62.755",
                37,
                Unlocked("Tod 'The Blaster' McQuinn", 300260) + "," + Unlocked("Lori Jameson", 300230)));

        var tod = Named(report, "Tod 'The Blaster' McQuinn");
        var lori = Named(report, "Lori Jameson");

        Assert.Equal(4, tod.Chain.Jumps);
        Assert.Equal(2, lori.Chain.Jumps);
        Assert.Equal(1 + Math.Log2(21) + 0.2, tod.Value, 6);
        Assert.Equal(6, lori.Value, 6);
        Assert.Equal(2.0, lori.Score, 6);

        Assert.Equal("Lori Jameson", report.Route[0].Engineer.Name);
        Assert.Equal("Tod 'The Blaster' McQuinn", report.Route[1].Engineer.Name);
    }
}
