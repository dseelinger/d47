using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// The engineer solver (Phase 28, "The fastest way in").
/// <para>
/// Three claims hold this file up. <b>The best next unlock is the one that covers the most of
/// what is planned</b>, not the shortest chain. <b>The unit is jumps</b>, so a long haul and a
/// long chain of short hops are compared on one scale — which is why <b>Colonia needs no rule of
/// its own</b>. And <b>the ranking shows its work</b>, because a ranking nobody can inspect is an
/// oracle.
/// </para>
/// </summary>
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
    /// Dirty drives at grade 3, which Felicity Farseer does roll — where grade 5 is three other
    /// people entirely, which is the per-grade point made twice over.
    /// </summary>
    private static ShipBuild SofterThrusters() =>
        new("F1", "ship-3", "python", 12, "Bad Idea",
            [new SlotPlan("MainEngines", "Dirty Drive Tuning", 3)]);

    /// <summary>Armour, which Liz Ryder rolls and Felicity Farseer does not.</summary>
    private static ShipBuild Hull() =>
        new("F1", "ship-4", "cobramkiii", 14, "Tin Can",
            [new SlotPlan("Armour", "Heavy Duty", 1, Module: "Armour")]);

    /// <summary>
    /// The plans are read for who could roll them, per grade — because the blueprint table states
    /// it per grade. Five engineers roll dirty drives at grade 1 and three at grade 5, and
    /// deriving the list from an engineer's top grade instead would send a Commander to somebody
    /// who does not roll it.
    /// </summary>
    [Fact]
    public void WhoCanRollIsReadPerGrade()
    {
        Assert.Equal(
            ["Chloe Sedesi", "Mel Brandon", "Professor Palin"],
            PlannedNeeds.Rollers("Dirty Drive Tuning", null, 5, null));

        Assert.Contains("Felicity Farseer", PlannedNeeds.Rollers("Dirty Drive Tuning", null, 1, null));
    }

    /// <summary>
    /// A slot naming an engineer is that engineer and nobody else. The Commander has an opinion,
    /// and offering alternatives answers a question they did not ask.
    /// </summary>
    [Fact]
    public void AnEngineerTheCommanderNamedIsTheAnswer()
    {
        Assert.Equal(
            ["Felicity Farseer"],
            PlannedNeeds.Rollers("Dirty Drive Tuning", null, 5, "Farseer"));
    }

    /// <summary>
    /// The count that belongs to the other tab: how many planned things are waiting on somebody
    /// the Commander has not unlocked. A plan blocked on a person is not a plan blocked on
    /// materials, and the gap analysis cannot say so.
    /// </summary>
    [Fact]
    public void PlansWaitingOnAStrangerAreCounted()
    {
        var report = UnlockPlanner.Of([Thrusters(), Drive()], [], State());

        Assert.Equal(2, report.Planned.Count);
        Assert.Equal(2, report.Waiting);
        Assert.Contains("waiting on somebody you have not unlocked", report.Summary());

        var unlocked = UnlockPlanner.Of([Thrusters(), Drive()], [], State(
            """{"timestamp":"2026-08-18T09:10:00Z","event":"EngineerProgress","Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}"""));

        // Farseer rolls Increased FSD Range to grade 5 and dirty drives only to grade 3, so she
        // clears one of the two and the other is still waiting on a stranger.
        Assert.Equal(1, unlocked.Waiting);
    }

    /// <summary>
    /// The directory sorts by what can be acted on today — within reach, then unlocked, then
    /// locked — rather than alphabetically or by speciality.
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
    /// The headline: <b>the best next unlock is the one that satisfies the most of what is
    /// planned, not the nearest one.</b> Liz Ryder is 81 light years away and covers the armour;
    /// Felicity Farseer is 131 away and covers the drives and the thrusters. Ranked on distance
    /// alone Liz Ryder wins; ranked on the trip per thing it buys, Farseer does — 5 jumps for two
    /// against 3 jumps for one.
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
    /// An unlock that covers something the Commander has planned beats one that covers nothing,
    /// however near it is. The page is answering "how do I get what my plans need", and a nearer
    /// stranger is not an answer to that.
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

    /// <summary>
    /// <b>Colonia needs no rule of its own.</b> Mel Brandon rolls grade 5 dirty drives, like
    /// Professor Palin, and is 22,000 light years away — which is hundreds of jumps and swamps any
    /// step count, so he ranks below a longer chain inside the Bubble without anything here
    /// knowing what Colonia is.
    /// </summary>
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
    /// Measured from where the Commander is standing, so being in Colonia flips it — which is the
    /// whole reason no rule about Colonia is needed anywhere.
    /// </summary>
    [Fact]
    public void BeingInColoniaFlipsIt()
    {
        // An on-foot modification two people do: Hero Ferrari in the Bubble and Baltanos in
        // Colonia. Both are walked up to — neither needs a recommendation — so the only thing
        // between them is where the Commander is standing.
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
    /// It shows its work: one sentence with the steps, the distance, the jumps and what it covers,
    /// and a listing underneath naming what cannot be counted in jumps at all.
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
    /// A grade the Commander cannot reach with somebody they have already unlocked is still in the
    /// way, so it is still a candidate — as a rank climb rather than an unlock, priced where
    /// Frontier priced it.
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
    /// With nothing planned the first key is dead, and the answer collapses to who is nearest —
    /// which is the right answer to a question with no plans behind it.
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
    /// The route promotes as a chain rather than a line: one checklist item per stop, in flying
    /// order, each carrying the grade that stop actually needs. A single line reading "unlock Broo
    /// Tarquin" hides two engineers and two rank climbs behind a tick nobody can make progress on.
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
