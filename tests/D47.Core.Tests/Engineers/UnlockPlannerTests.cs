using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>The engineer solver.</summary>
public class UnlockPlannerTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A Commander sitting in Sol in a ship that jumps 30 light years, having met nobody.</summary>
    private static CommanderGameState State(params string[] extra)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Known"}]}""",
                 }.Concat(extra))
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    /// <summary>Dirty drives at grade 5, which only three people in the game roll.</summary>
    private static ShipBuild Thrusters() =>
        new("F1", "ship-1", "python", 12, "Bad Idea",
            [new SlotPlan("MainEngines", "Dirty Drive Tuning", 5)]);

    /// <summary>A second plan, on a module with a different and overlapping set of engineers.</summary>
    private static ShipBuild Drive() =>
        new("F1", "ship-2", "krait_mkii", 13, "Long Way",
            [new SlotPlan("FrameShiftDrive", "Increased FSD Range", 3)]);

    /// <summary>
    /// Dirty drives at grade 3, which Felicity Farseer does roll — where grade 5 is three other people
    /// entirely, which is the per-grade point made twice over.
    /// </summary>
    private static ShipBuild SofterThrusters() =>
        new("F1", "ship-3", "python", 12, "Bad Idea",
            [new SlotPlan("MainEngines", "Dirty Drive Tuning", 3)]);

    /// <summary>Armour, which Liz Ryder rolls and Felicity Farseer does not.</summary>
    private static ShipBuild Hull() =>
        new("F1", "ship-4", "cobramkiii", 14, "Tin Can",
            [new SlotPlan("Armour", "Heavy Duty", 1, Module: "Armour")]);

    /// <summary>
    /// "Heavy Duty" with no module named, on a slot that (once the loadout is read) turns out to be a
    /// Shield Booster — which Selene Jean cannot grade at all, though she grades Armour of the same name
    /// (#137).
    /// </summary>
    private static ShipBuild ShieldBoosterHeavyDuty() =>
        new("F1", "ship-5", "python", 12, "Bad Idea",
            [new SlotPlan("TinyHardpoint1", "Heavy Duty", 1)]);

    /// <summary>Dirty drives at grade 5, plus the "Double Braced" experimental on the same slot.</summary>
    private static ShipBuild ThrustersWithExperimental() =>
        new("F1", "ship-6", "python", 12, "Bad Idea",
            [new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, Experimental: "Double Braced")]);

    /// <summary>Increased FSD Range at grade 3, plus the "Mass Manager" experimental on the same slot.</summary>
    private static ShipBuild DriveWithExperimental() =>
        new("F1", "ship-7", "krait_mkii", 13, "Long Way",
            [new SlotPlan("FrameShiftDrive", "Increased FSD Range", 3, Experimental: "Mass Manager")]);

    /// <summary>The remembered loadout of "Bad Idea", with grade 5 Dirty Drive Tuning already rolled.</summary>
    private static string EngineeredThrusters() =>
        """{"timestamp":"2026-08-18T09:05:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size2_class1","On":true,"Health":1.0,"Engineering":{"BlueprintName":"Engine_Dirty","Level":5}}]}""";

    /// <summary>The remembered loadout of "Bad Idea", with a Shield Booster in the tiny hardpoint.</summary>
    private static string ShieldBoosterFitted() =>
        """{"timestamp":"2026-08-18T09:05:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[{"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class1","On":true,"Health":1.0}]}""";

    /// <summary>
    /// The count is modules still to engineer, not rolls — a blueprint the loadout already carries at
    /// the planned grade contributes to nobody's count (#137).
    /// </summary>
    [Fact]
    public void AnAppliedBlueprintCountsForNobody()
    {
        var report = UnlockPlanner.Of([Thrusters()], [], State(EngineeredThrusters()));

        Assert.All(report.Directory, entry => Assert.Equal(0, entry.Wanted));
    }

    /// <summary>
    /// A blueprint name shared by several module types is matched against the module actually sitting
    /// in the slot, not against every module that shares the name (#137).
    /// </summary>
    [Fact]
    public void AnEngineerWhoCannotGradeTheFittedModuleIsNotCounted()
    {
        var report = UnlockPlanner.Of([ShieldBoosterHeavyDuty()], [], State(ShieldBoosterFitted()));

        var selene = report.Directory.Single(entry => entry.Engineer.Name == "Selene Jean");
        var brandon = report.Directory.Single(entry => entry.Engineer.Name == "Mel Brandon");

        Assert.Equal(0, selene.Wanted);
        Assert.Equal(1, brandon.Wanted);
    }

    /// <summary>
    /// One slot with an outstanding blueprint and an outstanding experimental effect still contributes
    /// at most 1 to an engineer's count, even where they could do both (#137).
    /// </summary>
    [Fact]
    public void AModuleContributesAtMostOneToACount()
    {
        var report = UnlockPlanner.Of([DriveWithExperimental()], [], State());

        var farseer = report.Directory.Single(entry => entry.Engineer.Name == "Felicity Farseer");

        Assert.Equal(1, farseer.Wanted);
    }

    /// <summary>
    /// An engineer who can only apply the experimental effect, and not roll the outstanding blueprint on
    /// the same slot, is not the answer to that module and is not counted for it (#137).
    /// </summary>
    [Fact]
    public void AnEngineerWhoCanOnlyHalfFinishAModuleIsNotCounted()
    {
        var report = UnlockPlanner.Of([ThrustersWithExperimental()], [], State());

        var farseer = report.Directory.Single(entry => entry.Engineer.Name == "Felicity Farseer");
        var brandon = report.Directory.Single(entry => entry.Engineer.Name == "Mel Brandon");

        Assert.Equal(0, farseer.Wanted);
        Assert.Equal(1, brandon.Wanted);
    }

    /// <summary>
    /// Once the blueprint is applied there is no half left: an engineer who offers only the remaining
    /// experimental effect is counted even though they could never have rolled the blueprint (#137).
    /// </summary>
    [Fact]
    public void AnEngineerOfferingOnlyTheRemainingEffectIsCountedOnceTheBlueprintIsApplied()
    {
        var report = UnlockPlanner.Of([ThrustersWithExperimental()], [], State(EngineeredThrusters()));

        var farseer = report.Directory.Single(entry => entry.Engineer.Name == "Felicity Farseer");

        Assert.Equal(1, farseer.Wanted);
    }

    /// <summary>
    /// The plans are read for who could roll them, per grade — because the blueprint table states it
    /// per grade.
    /// </summary>
    [Fact]
    public void WhoCanRollIsReadPerGrade()
    {
        Assert.Equal(
            ["Chloe Sedesi", "Mel Brandon", "Professor Palin"],
            PlannedNeeds.Rollers("Dirty Drive Tuning", null, 5, null));

        Assert.Contains("Felicity Farseer", PlannedNeeds.Rollers("Dirty Drive Tuning", null, 1, null));
    }

    /// <summary>A slot naming an engineer is that engineer and nobody else.</summary>
    [Fact]
    public void AnEngineerTheCommanderNamedIsTheAnswer()
    {
        Assert.Equal(
            ["Felicity Farseer"],
            PlannedNeeds.Rollers("Dirty Drive Tuning", null, 5, "Farseer"));
    }

    /// <summary>
    /// The top line counts the game's three states against the whole directory, and nothing about
    /// what the solver judges reachable remains in it (#133).
    /// </summary>
    [Fact]
    public void TheSummaryCountsTheGamesThreeStates()
    {
        var report = UnlockPlanner.Of([], [], State());

        var total = report.Directory.Count;
        var unlocked = report.Directory.Count(entry => entry.Standing?.IsUnlocked == true);
        var inProgress = report.Directory.Count(entry => entry.Standing is { IsUnlocked: false });
        var notStarted = report.Directory.Count(entry => entry.Standing is null);

        Assert.Equal(total, unlocked + inProgress + notStarted);
        Assert.Equal(
            $"{unlocked} of {total} unlocked. {inProgress} in progress. {notStarted} not started.",
            report.Summary());
    }

    /// <summary>
    /// The count that belongs to the other tab: how many planned things are waiting on somebody the
    /// Commander has not unlocked.
    /// </summary>
    [Fact]
    public void PlansWaitingOnAStrangerAreCounted()
    {
        var report = UnlockPlanner.Of([Thrusters(), Drive()], [], State());

        Assert.Equal(2, report.Planned.Count);
        Assert.Equal(2, report.Waiting);

        var unlocked = UnlockPlanner.Of([Thrusters(), Drive()], [], State(
            """{"timestamp":"2026-08-18T09:10:00Z","event":"EngineerProgress","Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}"""));

        // Farseer rolls Increased FSD Range to grade 5 and dirty drives only to grade 3, so she clears one of
        // the two and the other is still waiting on a stranger.
        Assert.Equal(1, unlocked.Waiting);
    }

    /// <summary>
    /// The directory sorts by what can be acted on today — within reach, then unlocked, then locked —
    /// rather than alphabetically or by speciality.
    /// </summary>
    [Fact]
    public void TheDirectoryLeadsWithWhatIsReachable()
    {
        var report = UnlockPlanner.Of([], [], State(
            """{"timestamp":"2026-08-18T09:10:00Z","event":"EngineerProgress","Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}"""));

        Assert.Equal(38, report.Directory.Count);
        Assert.Equal(
            report.Directory.Select(entry => entry.Reach).OrderBy(reach => reach),
            report.Directory.Select(entry => entry.Reach));

        var farseer = report.Directory.Single(entry => entry.Engineer.Name == "Felicity Farseer");

        Assert.Equal(EngineerReach.Unlocked, farseer.Reach);
        Assert.Equal(5, farseer.Jumps);
        Assert.Contains("131 ly", farseer.Aside);
    }

    /// <summary>
    /// The headline: the best next unlock is the one that satisfies the most of what is planned, not
    /// the nearest one.
    /// </summary>
    [Fact]
    public void TheBestUnlockIsTheOneThatCoversTheMost()
    {
        var report = UnlockPlanner.Of([SofterThrusters(), Drive(), Hull()], [], State());

        var farseer = report.Route.Single(candidate => candidate.Engineer.Name == "Felicity Farseer");
        var ryder = report.Route.Single(candidate => candidate.Engineer.Name == "Liz Ryder");

        Assert.Equal(2, farseer.Covers.Count);
        Assert.Single(ryder.Covers);
        Assert.True(ryder.Chain.Jumps < farseer.Chain.Jumps, "Liz Ryder is the nearer of the two");

        Assert.Equal("Felicity Farseer", report.Route[0].Engineer.Name);
    }

    /// <summary>
    /// An unlock that covers something the Commander has planned beats one that covers nothing, however
    /// near it is.
    /// </summary>
    [Fact]
    public void CoveringSomethingComesFirst()
    {
        var report = UnlockPlanner.Of([SofterThrusters()], [], State());

        var last = report.Route.Select(candidate => candidate.Covers.Count > 0).ToList();

        Assert.Contains(true, last);
        Assert.Contains(false, last);
        Assert.Equal(last.OrderByDescending(covering => covering), last);
    }

    /// <summary>Colonia needs no rule of its own.</summary>
    [Fact]
    public void DistanceSwampsStepCount()
    {
        var report = UnlockPlanner.Of([Thrusters()], [], State());

        var palin = report.Route.ToList().FindIndex(candidate => candidate.Engineer.Name == "Professor Palin");
        var brandon = report.Route.ToList().FindIndex(candidate => candidate.Engineer.Name == "Mel Brandon");

        Assert.True(palin >= 0 && brandon >= 0);
        Assert.True(palin < brandon, "a Bubble chain of three should beat one stop 22,000 ly away");
    }

    /// <summary>
    /// Measured from where the Commander is standing, so being in Colonia flips it — which is the whole
    /// reason no rule about Colonia is needed anywhere.
    /// </summary>
    [Fact]
    public void BeingInColoniaFlipsIt()
    {
        // An on-foot modification two people do: Hero Ferrari in the Bubble and Baltanos in Colonia.
        var jumpAssist = new OnFootBuild(
            "kit-1", "Maverick Suit", OnFootKind.Suit, 7,
            [new KitPlan(OnFootBuild.ModSlot(1), Modification: "Improved jump assist")]);

        var bubble = UnlockPlanner.Of([], [jumpAssist], State()).Route.Select(c => c.Engineer.Name).ToList();

        Assert.True(
            bubble.IndexOf("Hero Ferrari") < bubble.IndexOf("Baltanos"),
            "standing in the Bubble should put the Bubble engineer first");

        var colonia = UnlockPlanner.Of([], [jumpAssist], State(
            """{"timestamp":"2026-08-18T09:30:00Z","event":"FSDJump","StarSystem":"Colonia","StarPos":[-9530.5,-910.28125,19808.125]}"""))
            .Route.Select(candidate => candidate.Engineer.Name).ToList();

        Assert.True(
            colonia.IndexOf("Baltanos") < colonia.IndexOf("Hero Ferrari"),
            "standing in Colonia should put the Colonia engineer first");
    }

    /// <summary>
    /// It shows its work: one sentence with the steps, the distance, the jumps and what it covers, and
    /// a listing underneath naming what cannot be counted in jumps at all.
    /// </summary>
    [Fact]
    public void TheRankingExplainsItself()
    {
        var report = UnlockPlanner.Of([Thrusters(), Drive()], [], State());
        var best = report.Route[0];

        var said = best.Summary();

        Assert.Equal("Felicity Farseer", best.Engineer.Name);
        Assert.Contains("1 step", said);
        Assert.Contains("131 ly", said);
        Assert.Contains("about 5 jumps", said);
        Assert.Contains("1 planned thing covered", said);

        var working = best.Working();

        // The open-ended half, in Frontier's own words rather than as a number.
        Assert.Contains(working, line => line.Contains("exploration rank", StringComparison.Ordinal));

        // And the delivery, which is a shopping run to a known system.
        Assert.Contains(working, line => line.Contains("Meta-alloys", StringComparison.Ordinal));

        // And what it buys.
        Assert.Contains(working, line => line.Contains("Increased FSD Range", StringComparison.Ordinal));
    }

    /// <summary>
    /// A grade the Commander cannot reach with somebody they have already unlocked is still in the way,
    /// so it is still a candidate — as a rank climb rather than an unlock, priced where Frontier priced
    /// it.
    /// </summary>
    [Fact]
    public void ARankClimbIsAlsoAWayIn()
    {
        var report = UnlockPlanner.Of([Thrusters()], [], State(
            """{"timestamp":"2026-08-18T09:10:00Z","event":"EngineerProgress","Engineer":"Professor Palin","EngineerID":300220,"Progress":"Unlocked","Rank":2}"""));

        var palin = report.Route.Single(candidate => candidate.Engineer.Name == "Professor Palin");

        Assert.Single(palin.Chain.Steps);
        Assert.Equal(5, palin.Chain.Steps[0].Grade);
        Assert.True(palin.Chain.Steps[0].NeedsRanking);
        Assert.Equal("Professor Palin", report.Route[0].Engineer.Name);
    }

    /// <summary>
    /// With nothing planned the first key is dead, and the answer collapses to who is nearest — which
    /// is the right answer to a question with no plans behind it.
    /// </summary>
    [Fact]
    public void WithNothingPlannedItRanksByDistance()
    {
        var report = UnlockPlanner.Of([], [], State());

        Assert.NotEmpty(report.Route);
        Assert.All(report.Route, candidate => Assert.Empty(candidate.Covers));

        var jumps = report.Route.Select(candidate => candidate.Chain.Jumps ?? int.MaxValue).ToList();

        Assert.Equal(jumps.OrderBy(count => count), jumps);
    }

    /// <summary>
    /// The route promotes as a chain rather than a line: one checklist item per stop, in flying order,
    /// each carrying the grade that stop actually needs.
    /// </summary>
    [Fact]
    public void PromotionEmitsTheWholeChain()
    {
        var chain = EngineerAccess.ChainTo(
            EngineerDirectory.ByName("Broo Tarquin")!, 5, EngineerProgressState.Empty, null, null);

        var items = UnlockPlanner.Items(chain, ChecklistScope.Universal);

        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.Equal(ChecklistIntentKind.EngineerAccess, item.Intent!.Kind));
        Assert.All(items, item => Assert.Equal(ChecklistItemKind.Derived, item.Kind));

        Assert.Equal(["Liz Ryder", "Hera Tani", "Broo Tarquin"], items.Select(item => item.Intent!.Subject));
        Assert.Equal([3, 3, 5], items.Select(item => item.Intent!.Grade));

        // Distinct keys, or the chain collapses into one item the moment it is proposed.
        Assert.Equal(3, items.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
