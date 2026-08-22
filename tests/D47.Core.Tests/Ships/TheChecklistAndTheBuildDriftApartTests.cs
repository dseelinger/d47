using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// Asking, once, when the ship the Commander has boarded carries a build their checklist does not
/// (list.md Phase 38, "Ask before the plan and the checklist drift apart").
/// <para>
/// <b>Three rules, and all three are about not being a nuisance.</b> It fires on a swap and only
/// where the two actually disagree; <i>no</i> leaves both untouched and is not asked again for that
/// same difference; <i>yes</i> revises rather than rebuilds, so the ordering the Commander spent an
/// evening on survives.
/// </para>
/// </summary>
public class TheChecklistAndTheBuildDriftApartTests
{
    private static ChecklistService Checklists(TempInstall install) =>
        new(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

    private static JournalEvent Boarding(int ship)
    {
        Assert.True(JournalEvent.TryParse(
            $$"""
              {"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"python","ShipID":{{ship}},"MaxJumpRange":20.0,"UnladenMass":350.0,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0}]}
              """,
            NullLogger.Instance,
            out var journalEvent));

        return journalEvent!;
    }

    private sealed record Bench(
        ShipBuildStore Store, ChecklistService Checklists, ShipPlanService Ships, ShipDriftWatch Drift)
    {
        public string BuildId { get; init; } = string.Empty;
    }

    private static Bench Set(TempInstall install, bool planned = true)
    {
        var store = new ShipBuildStore(
            Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        var checklists = Checklists(install);
        var ships = new ShipPlanService(store, checklists, () => null);
        var build = ships.BuildFor(12, "python", "Bad Idea");

        if (planned)
        {
            ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));
        }

        return new Bench(store, checklists, ships, new ShipDriftWatch(ships, checklists))
        {
            BuildId = build.Id,
        };
    }

    [Fact]
    public void BoardingAShipWhoseBuildTheChecklistHasNotGotAsks()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        var asked = bench.Drift.Observe([Boarding(12)]);

        Assert.NotNull(asked);
        Assert.Contains("your checklist has not got", asked, StringComparison.Ordinal);

        // **It asks and never acts.** The question is a proposal, on the same accept-or-decline
        // boundary the model writes across, so nothing is on the list yet.
        Assert.Single(bench.Checklists.Proposals.Pending);
        Assert.Empty(bench.Checklists.Document.Items);
    }

    /// <summary>
    /// The live half of the list.md Phase 44 defect, on this watch. Ship ids are per Commander,
    /// and the last ship seen is a bare id — so a second Commander logging in aboard their own
    /// ship 12 reads as the ship already seen, and their build is never compared. A reset on the
    /// switch makes their first Loadout a swap.
    /// </summary>
    [Fact]
    public void ANewCommanderInTheSameShipIdIsAskedAboutTheirOwnBuild()
    {
        using var install = new TempInstall();
        var state = new GameStateStore();
        state.Apply(Identity("F1", "Alice"));

        var store = new ShipBuildStore(
            Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state.Active);

        var ships = new ShipPlanService(store, checklists, () => state.Active);
        var drift = new ShipDriftWatch(ships, checklists);

        var alice = ships.BuildFor(12, "python", "Bad Idea");
        ships.Plan(alice.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));

        Assert.NotNull(drift.Observe([Boarding(12)]));

        // Bob logs in, also in a ship 12, with a plan of his own for it. A different plan, so
        // that Alice's still-outstanding question cannot be mistaken for a "no" to Bob's: the
        // outstanding question is keyed by the same bare ship id, which is the other thing the
        // reset drops.
        state.Apply(Identity("F2", "Bob"));
        var bob = ships.BuildFor(12, "python", "Other Idea");
        ships.Plan(bob.Id, new SlotPlan("MainEngines", "Drive Strengthening", 3, "Felicity Farseer"));

        // Same id, so without the reset this is the ship already seen — the defect.
        Assert.Null(drift.Observe([Boarding(12)]));

        drift.Reset();

        var asked = drift.Observe([Boarding(12)]);
        Assert.NotNull(asked);
        Assert.Contains("Other Idea", asked, StringComparison.Ordinal);
    }

    private static JournalEvent Identity(string fid, string name)
    {
        Assert.True(JournalEvent.TryParse(
            $$"""{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"{{fid}}","Name":"{{name}}"}""",
            NullLogger.Instance,
            out var journalEvent));

        return journalEvent!;
    }

    [Fact]
    public void AShipWhoseChecklistAlreadyAgreesIsNotAskedAbout()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        // Promoted, so the two agree exactly.
        bench.Ships.Promote(bench.BuildId);

        Assert.Null(bench.Drift.Observe([Boarding(12)]));
        Assert.Empty(bench.Checklists.Proposals.Pending);
    }

    [Fact]
    public void AShipWithNoPlanAtAllIsNotAskedAbout()
    {
        using var install = new TempInstall();
        var bench = Set(install, planned: false);

        Assert.Null(bench.Drift.Observe([Boarding(12)]));
    }

    [Fact]
    public void ItFiresOnASwapRatherThanOnEveryLoadout()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));

        // A Loadout arrives on every re-outfit and every dock. Asking on each would be a question
        // every few minutes.
        Assert.Null(bench.Drift.Observe([Boarding(12)]));
    }

    [Fact]
    public void SayingYesRevisesRatherThanRebuilds()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        bench.Ships.Promote(bench.BuildId);

        static IReadOnlyList<string> Order(ChecklistService checklists) =>
            [.. checklists.Document.Items.Where(item => item.IsLive).Select(item => item.Key)];

        var promoted = Order(bench.Checklists);

        Assert.True(promoted.Count > 1);

        // **The evening's work.** The Commander puts the list in the order they mean to work it,
        // which is the state a rebuild costs and a revision keeps — nothing promoted from a plan
        // ticks by hand, so ordering is what there is to lose.
        var scope = bench.Store.Builds.Single(build => build.ShipId == 12).Scope!;

        bench.Checklists.Move(new ChecklistItemId(scope, promoted[^1]), -1);

        var theirs = Order(bench.Checklists);

        Assert.NotEqual(promoted, theirs);

        // Then the plan grows a slot, and the question is asked on the next boarding.
        bench.Ships.Plan(bench.BuildId, new SlotPlan("PowerPlant", "Armoured", 3, "Hera Tani"));

        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));
        Assert.NotEmpty(bench.Checklists.Accept());

        var after = Order(bench.Checklists);

        // What was already there is where they put it, and the new lines are added after rather
        // than the whole list being written again in the plan's own order.
        Assert.Equal(theirs, [.. after.Take(theirs.Count)]);
        Assert.True(after.Count > theirs.Count);

        Assert.Contains(
            bench.Checklists.Document.Items,
            item => item.IsLive && item.Text.Contains("Armoured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SayingNoLeavesBothAloneAndIsNotAskedAgain()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));

        bench.Checklists.Decline();

        Assert.Empty(bench.Checklists.Document.Items);
        Assert.Empty(bench.Checklists.Proposals.Pending);

        // Boarding another ship and coming back is the same difference, and the answer stands.
        Assert.Null(bench.Drift.Observe([Boarding(7)]));
        Assert.Null(bench.Drift.Observe([Boarding(12)]));
        Assert.Empty(bench.Checklists.Proposals.Pending);

        // **Remembered on the build rather than in memory**, so a restart does not re-ask.
        bench.Store.Poll();

        var settled = bench.Store.Builds.Single(build => build.ShipId == 12).Settled;

        Assert.NotNull(settled);

        var fresh = new ShipDriftWatch(
            new ShipPlanService(bench.Store, bench.Checklists, () => null), bench.Checklists);

        Assert.Null(fresh.Observe([Boarding(12)]));
    }

    [Fact]
    public void ADifferentDisagreementIsANewQuestionAndIsAsked()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));

        bench.Checklists.Decline();

        Assert.Null(bench.Drift.Observe([Boarding(7)]));
        Assert.Null(bench.Drift.Observe([Boarding(12)]));

        // The Commander plans something else. That is not the difference they said no to.
        bench.Ships.Plan(bench.BuildId, new SlotPlan("PowerPlant", "Armoured", 3, "Hera Tani"));

        Assert.Null(bench.Drift.Observe([Boarding(7)]));
        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));
    }

    [Fact]
    public void NothingIsAskedWhileSomethingIsAlreadyWaitingOnThatShip()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));

        var waiting = bench.Checklists.Proposals.Pending.Count;

        Assert.Null(bench.Drift.Observe([Boarding(7)]));
        Assert.Null(bench.Drift.Observe([Boarding(12)]));
        Assert.Equal(waiting, bench.Checklists.Proposals.Pending.Count);
    }
}
