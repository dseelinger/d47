using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// The fleet, the fleet the Commander intends, and one build per ship (list.md Phase 26).
/// <para>
/// <b>The plan owns what and the checklist owns when.</b> Everything here is about the first half:
/// nothing in this file writes to a checklist without a proposal.
/// </para>
/// </summary>
public class ShipPlanTests
{
    private static ShipBuildStore Store(TempInstall install) =>
        new(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

    private static ChecklistService Checklists(TempInstall install, CommanderGameState? state = null) =>
        new(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

    private static ShipPlanService Service(
        TempInstall install, ShipBuildStore store, CommanderGameState? state = null) =>
        new(store, Checklists(install, state), () => state);

    /// <summary>
    /// A hull the Commander does not own has no ship id, because the journal's id is what a ship
    /// list is keyed by and a Corsair nobody has bought has none.
    /// </summary>
    [Fact]
    public void AnIntendedHullHasNoShipIdAndIsNotOwned()
    {
        using var install = new TempInstall();
        var ships = Service(install, Store(install));

        var build = ships.Intend("Python");

        Assert.NotNull(build);
        Assert.Null(build.ShipId);
        Assert.False(build.IsOwned);
        Assert.Null(build.Scope);
        Assert.Contains("intended", build.Describe(), StringComparison.Ordinal);
    }

    /// <summary>The shipped table is what answers "is that a hull", so a typo is refused.</summary>
    [Fact]
    public void AHullNoTableKnowsIsRefusedRatherThanInvented()
    {
        using var install = new TempInstall();

        Assert.Null(Service(install, Store(install)).Intend("Corsaire Mark Nine"));
    }

    /// <summary>
    /// The identity is stable and independent of the ship id from the moment the build is made —
    /// which is what there is to rebind when the hull is bought.
    /// </summary>
    [Fact]
    public void BuyingTheHullAdoptsThePlanRatherThanMakingTheCommanderRePointIt()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var ships = Service(install, store);

        var intended = ships.Intend("Python")!;

        ships.Plan(intended.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        // The journal writes ShipyardNew with the id. ShipyardBuy, measured against real journals,
        // carries no id for the new ship at all - which is why this matches on the other event.
        var said = ships.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"ShipyardNew","ShipType":"python","NewShipID":21}
            """)]);

        Assert.Single(said);

        var adopted = store.Find(intended.Id);

        Assert.NotNull(adopted);
        Assert.Equal(21, adopted.ShipId);
        Assert.True(adopted.IsOwned);

        // The same identity throughout, and the slot plan came with it.
        Assert.Equal(intended.Id, adopted.Id);
        Assert.Equal("Dirty Drive Tuning", adopted.For("MainEngines")?.Blueprint);
    }

    /// <summary>
    /// Two Corsairs planned and one bought is a question rather than a guess: adopting the wrong
    /// one silently is worse than adopting neither.
    /// </summary>
    [Fact]
    public void TwoIntendedHullsOfOneTypeAreNotAdoptedAtAll()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var ships = Service(install, store);

        ships.Intend("Python", "one");
        ships.Intend("Python", "two");

        var said = ships.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"ShipyardNew","ShipType":"python","NewShipID":21}
            """)]);

        Assert.Empty(said);
        Assert.All(store.Builds, build => Assert.False(build.IsOwned));
    }

    /// <summary>A slot holds one plan, because a slot holds one module.</summary>
    [Fact]
    public void PlanningASlotTwiceReplacesRatherThanAdds()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var ships = Service(install, store);

        var build = ships.Intend("Python")!;

        ships.Plan(build.Id, new SlotPlan("Hardpoint1", "Long Range", 5));
        ships.Plan(build.Id, new SlotPlan("Hardpoint1", "Overcharged", 3));

        var slot = Assert.Single(store.Find(build.Id)!.Slots);

        Assert.Equal("Overcharged", slot.Blueprint);
        Assert.Equal(3, slot.Grade);
    }

    /// <summary>
    /// One build per ship. Comparing a combat fit against an exploration fit for the same hull is
    /// a planner feature this deliberately does not have.
    /// </summary>
    [Fact]
    public void AShipHasOneBuild()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var ships = Service(install, store);

        var first = ships.BuildFor(12, "python", "Bad Idea");
        var again = ships.BuildFor(12, "python", "Bad Idea");

        Assert.Equal(first.Id, again.Id);
        Assert.Single(store.Builds);
    }

    /// <summary>And a hand edit that puts two on one ship is reported rather than obeyed.</summary>
    [Fact]
    public void AFileWithTwoBuildsForOneShipIsRefusedAndSaidSo()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "ships.json");

        File.WriteAllText(path, """
        {
          "ships": [
            { "id": "ship-1", "hull": "python", "shipId": 12 },
            { "id": "ship-2", "hull": "python", "shipId": 12 }
          ]
        }
        """);

        var store = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);
        store.Poll();

        Assert.Single(store.Builds);
        Assert.Single(store.Problems);
    }

    /// <summary>
    /// Promotion goes through the proposal path, so nothing lands on the checklist unasked.
    /// </summary>
    [Fact]
    public void PromotingProposesRatherThanWriting()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var checklists = Checklists(install);
        var ships = new ShipPlanService(store, checklists, () => null);

        var build = ships.BuildFor(12, "python", "Bad Idea");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));
        ships.Promote(build.Id);

        // Nothing on the list, and something waiting.
        Assert.Empty(checklists.Document.Items);
        Assert.NotEmpty(checklists.Proposals.Pending);

        checklists.Accept();

        // Promotion is one-to-many: the modification, plus the rank the grade needs.
        var items = checklists.Document.Items;

        Assert.Contains(items, item => item.Intent?.Kind == ChecklistIntentKind.Blueprint);
        Assert.Contains(items, item => item.Intent?.Kind == ChecklistIntentKind.EngineerAccess);
    }

    /// <summary>
    /// A prospective hull has no list to be on, and that is said rather than invented: scoping a
    /// Corsair's hardpoints to the universal list would outlive the decision to buy one.
    /// </summary>
    [Fact]
    public void APlanForAHullYouDoNotOwnCannotBePromoted()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var checklists = Checklists(install);
        var ships = new ShipPlanService(store, checklists, () => null);

        var build = ships.Intend("Python")!;

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        var said = ships.Promote(build.Id);

        Assert.Contains("do not own", said, StringComparison.Ordinal);
        Assert.Empty(checklists.Proposals.Pending);
    }

    /// <summary>
    /// Dropping a plan keeps what it already put on the checklist. The Commander ordered their
    /// list around those lines, and silently removing them is a history that lies.
    /// </summary>
    [Fact]
    public void DroppingABuildKeepsWhatItAlreadyPromoted()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var checklists = Checklists(install);
        var ships = new ShipPlanService(store, checklists, () => null);

        var build = ships.BuildFor(12, "python", "Bad Idea");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        ships.Promote(build.Id);
        checklists.Accept();

        var before = checklists.Document.Items.Count(item => item.IsLive);

        Assert.True(before > 0);

        var said = ships.Delete(build.Id);

        Assert.Empty(store.Builds);
        Assert.Equal(before, checklists.Document.Items.Count(item => item.IsLive));
        Assert.Contains("still there", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// Changing a slot and promoting again is a revision rather than a rebuild — which is the slot
    /// key of item one, seen from the far end of the phase.
    /// </summary>
    [Fact]
    public void ChangingASlotAndPromotingAgainRevisesRatherThanRebuilds()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var checklists = Checklists(install);
        var ships = new ShipPlanService(store, checklists, () => null);

        var build = ships.BuildFor(12, "python", "Bad Idea");

        ships.Plan(build.Id, new SlotPlan("Hardpoint1", "Long Range", 5));
        ships.Promote(build.Id);
        checklists.Accept();

        var first = checklists.Document.Items.Single(item => item.Intent?.Kind == ChecklistIntentKind.Blueprint);

        ships.Plan(build.Id, new SlotPlan("Hardpoint1", "Overcharged", 3));
        ships.Promote(build.Id);
        checklists.Accept();

        var after = checklists.Document.Items
            .Where(item => item.Intent?.Kind == ChecklistIntentKind.Blueprint)
            .ToList();

        // One item, still live, still the same identity. Nothing tombstoned beside it.
        var only = Assert.Single(after);

        Assert.True(only.IsLive);
        Assert.True(only.Id.Same(first.Id));
    }

    /// <summary>
    /// The fleet lists the ship being flown, which the journal's stored-ships snapshot never does:
    /// StoredShips is what is in the racks, and the one under the Commander is by definition not.
    /// </summary>
    [Fact]
    public void TheShipBeingFlownIsInTheFleet()
    {
        using var install = new TempInstall();
        var store = new GameStateStore();

        store.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        store.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[]}"""));

        var state = store.Active!;
        var ships = Service(install, Store(install), state);
        var fleet = ships.Fleet();

        var active = Assert.Single(fleet);

        Assert.True(active.IsActive);
        Assert.True(active.IsOwned);
        Assert.Equal("Bad Idea", active.Name);
        Assert.Contains("flying", active.Where(), StringComparison.Ordinal);
    }

    /// <summary>Change is detected by content, not by a stamp. Phase 21's correction.</summary>
    [Fact]
    public void TwoWritesInsideOneTickAreBothSeen()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "ships.json");
        var store = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);

        File.WriteAllText(path, """{ "ships": [ { "id": "ship-1", "hull": "python" } ] }""");
        Assert.True(store.Poll());

        File.WriteAllText(path, """{ "ships": [ { "id": "ship-2", "hull": "anaconda" } ] }""");
        Assert.True(store.Poll());

        Assert.Equal("anaconda", store.Builds[0].Hull);
        Assert.False(store.Poll());
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
