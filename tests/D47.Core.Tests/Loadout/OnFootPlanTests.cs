using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>
/// Suit and weapon plans: the Ships arrangement, on foot (Phase 27, "The same page, on
/// foot").
/// <para>
/// <b>The plan owns what and the checklist owns when.</b> Nothing in this file writes to a
/// checklist without a proposal, which is the same boundary the ship plans keep.
/// </para>
/// </summary>
public class OnFootPlanTests
{
    private static OnFootBuildStore Store(TempInstall install) =>
        new(Path.Combine(install.Root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

    private static ChecklistService Checklists(TempInstall install, CommanderGameState? state = null) =>
        new(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

    private static OnFootPlanService Service(
        TempInstall install, OnFootBuildStore store, CommanderGameState? state = null) =>
        new(store, Checklists(install, state), () => state);

    /// <summary>The Commander on foot in a grade 3 Maverick, which is what the journal reports.</summary>
    private static CommanderGameState OnFoot()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":1837009111675068,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[{"SlotName":"PrimaryWeapon1","SuitModuleID":1845784622401778,"ModuleName":"wpn_m_assaultrifle_laser_fauto","Class":2,"WeaponMods":[]}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// Something the Commander does not own has no journal id, so buying it is the plan's first
    /// step rather than a precondition sitting outside it — the on-foot reading of "a hull you do
    /// not own is not in the fleet".
    /// </summary>
    [Fact]
    public void AnIntendedSuitHasNoItemIdAndIsNotOwned()
    {
        using var install = new TempInstall();

        var build = Service(install, Store(install)).Intend("Maverick");

        Assert.NotNull(build);
        Assert.Null(build.ItemId);
        Assert.False(build.IsOwned);
        Assert.Null(build.Scope);
        Assert.Contains("intended", build.Describe(), StringComparison.Ordinal);
    }

    /// <summary>The shipped table is what answers "is that a suit", so a typo is refused.</summary>
    [Fact]
    public void ASuitNoTableKnowsIsRefusedRatherThanInvented()
    {
        using var install = new TempInstall();

        Assert.Null(Service(install, Store(install)).Intend("Maverock Mark Nine"));
    }

    /// <summary>
    /// <b>On foot the buy event carries the id</b>, which is the opposite of the ship side — so
    /// adoption needs one event where Phase 26 needed the second of two.
    /// </summary>
    [Fact]
    public void BuyingTheSuitAdoptsThePlan()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var intended = kit.Intend("Maverick")!;

        kit.Plan(intended.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        var said = kit.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1","Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
            """)]);

        Assert.Single(said);

        var adopted = store.Find(intended.Id);

        Assert.NotNull(adopted);
        Assert.Equal(1837009111675068, adopted.ItemId);
        Assert.True(adopted.IsOwned);

        // The same identity throughout, and the plan came with it.
        Assert.Equal(intended.Id, adopted.Id);
        Assert.Equal(5, adopted.PlannedGrade);
    }

    /// <summary>
    /// A weapon is adopted on its own event and its own id field, and a suit plan is not bound to
    /// a weapon the Commander bought.
    /// </summary>
    [Fact]
    public void BuyingAWeaponAdoptsOnlyAWeaponPlan()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var suit = kit.Intend("Maverick")!;
        var weapon = kit.Intend("Karma AR-50")!;

        kit.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"BuyWeapon","Name":"Wpn_M_AssaultRifle_Kinetic_FAuto","Name_Localised":"Karma AR-50","Class":1,"Price":125000,"SuitModuleID":1845784643934762,"WeaponMods":[]}
            """)]);

        Assert.False(store.Find(suit.Id)!.IsOwned);
        Assert.Equal(1845784643934762, store.Find(weapon.Id)!.ItemId);
        Assert.Equal(OnFootKind.Weapon, store.Find(weapon.Id)!.Kind);
    }

    /// <summary>
    /// Frontier's own localisation reports every suit above grade 1 as Class1, so the symbol is
    /// what the adoption matches on. Reading the localised name would bind the plan to the wrong
    /// grade's spelling — and here to nothing at all, since the suit is a Dominator.
    /// </summary>
    [Fact]
    public void AdoptionReadsTheSymbolRatherThanTheBrokenLocalisedName()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var intended = kit.Intend("Dominator")!;

        var said = kit.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"TacticalSuit_Class2","Name_Localised":"$TacticalSuit_Class1_Name;","Price":750000,"SuitID":42,"SuitMods":[]}
            """)]);

        Assert.Single(said);
        Assert.Equal(42, store.Find(intended.Id)!.ItemId);
    }

    /// <summary>
    /// Selling is the buy backwards (#274): the item goes and the plan stays, so the line answers
    /// "not bought yet" again rather than claiming the sold suit is still on the Commander. Both
    /// event bodies are real, and the sale carries the id the buy did.
    /// </summary>
    [Fact]
    public void SellingTheSuitGivesUpTheItemAndKeepsThePlan()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store, OnFoot());

        var intended = kit.Intend("Maverick")!;

        kit.Plan(intended.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        kit.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1","Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
            """)]);

        Assert.Equal("on you, grade 3", kit.Entry(intended.Id)!.Where());

        var said = kit.Observe([Event("""
            {"timestamp":"2026-08-18T10:00:00Z","event":"SellSuit","SuitID":1837009111675068,"SuitMods":[],"Name":"utilitysuit_class1","Name_Localised":"Maverick Suit","Price":90000}
            """)]);

        Assert.Single(said);

        var kept = store.Find(intended.Id)!;

        // The item is gone and the intent is not: owned is derived and intended is authored.
        Assert.Null(kept.ItemId);
        Assert.False(kept.IsOwned);
        Assert.Null(kept.Scope);
        Assert.Equal(5, kept.PlannedGrade);
        Assert.Equal("not bought yet", kit.Entry(intended.Id)!.Where());
    }

    /// <summary>
    /// A weapon sale disowns the weapon's build and nothing else, and a sale of something no build
    /// points at is not news (#274).
    /// </summary>
    [Fact]
    public void SellingAWeaponDisownsThatBuildAloneAndSaysNothingForAnUnplannedOne()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var suit = kit.Intend("Maverick")!;
        var weapon = kit.Intend("Karma AR-50")!;

        kit.Observe([
            Event("""
                {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1","Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
                """),
            Event("""
                {"timestamp":"2026-08-18T09:01:00Z","event":"BuyWeapon","Name":"Wpn_M_AssaultRifle_Kinetic_FAuto","Name_Localised":"Karma AR-50","Class":1,"Price":125000,"SuitModuleID":1845784643934762,"WeaponMods":[]}
                """)]);

        var said = kit.Observe([
            Event("""
                {"timestamp":"2026-08-18T10:00:00Z","event":"SellWeapon","Name":"wpn_m_assaultrifle_kinetic_fauto","Name_Localised":"Karma AR-50","Class":1,"WeaponMods":[],"Price":75000,"SuitModuleID":1845784643934762}
                """),
            Event("""
                {"timestamp":"2026-08-18T10:00:30Z","event":"SellWeapon","Name":"wpn_m_launcher_rocket_sauto","Name_Localised":"Karma L-6","Class":1,"WeaponMods":[],"Price":105000,"SuitModuleID":1845784668255858}
                """)]);

        // One sale, one thing said: the second weapon was never planned for.
        Assert.Single(said);
        Assert.False(store.Find(weapon.Id)!.IsOwned);
        Assert.True(store.Find(suit.Id)!.IsOwned);
    }

    /// <summary>
    /// Buying the replacement adopts the plan back onto it, which is the point of keeping the
    /// build: the id is what the sale gave up, and the plan is what it did not (#274).
    /// </summary>
    [Fact]
    public void BuyingAgainAfterASaleAdoptsTheSamePlanOntoTheNewItem()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var intended = kit.Intend("Maverick")!;

        kit.Plan(intended.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        kit.Observe([
            Event("""
                {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1","Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
                """),
            Event("""
                {"timestamp":"2026-08-18T10:00:00Z","event":"SellSuit","SuitID":1837009111675068,"SuitMods":[],"Name":"utilitysuit_class1","Name_Localised":"Maverick Suit","Price":90000}
                """),
            Event("""
                {"timestamp":"2026-08-18T11:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1","Name_Localised":"Maverick Suit","Price":150000,"SuitID":1845810920854798,"SuitMods":[]}
                """)]);

        var again = store.Find(intended.Id)!;

        Assert.Equal(1845810920854798, again.ItemId);
        Assert.Equal(5, again.PlannedGrade);
        Assert.Single(store.Builds);
    }

    /// <summary>A slot holds one plan, because a slot holds one thing.</summary>
    [Fact]
    public void PlanningASlotTwiceReplacesRatherThanAdds()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var build = kit.Intend("Maverick")!;

        kit.Plan(build.Id, new KitPlan(OnFootBuild.ModSlot(1), Modification: "Night Vision"));
        kit.Plan(build.Id, new KitPlan(OnFootBuild.ModSlot(1), Modification: "Extra Ammo Capacity"));

        var slot = Assert.Single(store.Find(build.Id)!.Slots);

        Assert.Equal("Extra Ammo Capacity", slot.Modification);
    }

    /// <summary>
    /// The mod slot is read as the mod slot it is. A hand edit naming a hardpoint is refused and
    /// reported rather than stored and then shown as something that can never promote.
    /// </summary>
    [Fact]
    public void ASlotThatIsNotAModSlotIsRefusedAndReported()
    {
        using var install = new TempInstall();
        var store = Store(install);

        File.WriteAllText(
            store.Path,
            """
            {"kit":[{"id":"kit-1","equipment":"Maverick Suit","kind":"suit","slots":[
              {"slot":"Hardpoint1","modification":"Night Vision"},
              {"slot":"Mod 1","modification":"Night Vision"}]}]}
            """);

        Assert.True(store.Poll());

        var build = Assert.Single(store.Builds);

        Assert.Equal("Mod 1", Assert.Single(build.Slots).Slot);
        Assert.Contains(store.Problems, problem => problem.Reason.Contains("Hardpoint1", StringComparison.Ordinal));
    }

    /// <summary>One build per item, enforced where the file is read as well as where it is written.</summary>
    [Fact]
    public void OneItemHasOneBuild()
    {
        using var install = new TempInstall();
        var store = Store(install);

        File.WriteAllText(
            store.Path,
            """
            {"kit":[
              {"id":"kit-1","equipment":"Maverick Suit","kind":"suit","itemId":7},
              {"id":"kit-2","equipment":"Maverick Suit","kind":"suit","itemId":7}]}
            """);

        Assert.True(store.Poll());
        Assert.Single(store.Builds);
        Assert.Single(store.Problems);
    }

    /// <summary>
    /// Promotion is a proposal. Accepting stays the Commander's own act, and the grade comes first
    /// because a grade 1 item has no modification slots and an engineer's base has no Pioneer
    /// Supplies.
    /// </summary>
    [Fact]
    public void PromotingProposesWithTheGradeFirst()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var state = OnFoot();
        var checklists = Checklists(install, state);
        var kit = new OnFootPlanService(store, checklists, () => state);

        var build = kit.BuildFor(OnFootKind.Suit, 1837009111675068, "Maverick Suit");

        kit.Plan(build.Id, new KitPlan(OnFootBuild.ModSlot(1), Modification: "Night Vision"));
        kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        kit.Promote(build.Id);

        Assert.Empty(checklists.Document.Items);

        var proposal = Assert.Single(checklists.Proposals.Pending);

        Assert.Equal(ChecklistSource.OnFootPlan, proposal.Source);

        // The grade first, whatever order the slots were planned in.
        Assert.Equal(ChecklistIntentKind.Grade, proposal.Items[0].Intent?.Kind);
        Assert.Contains(
            proposal.Items,
            item => item.Intent?.Kind == ChecklistIntentKind.Modification);
    }

    /// <summary>
    /// Something not owned has no list for its items to be in, so promotion says so rather than
    /// inventing a scope.
    /// </summary>
    [Fact]
    public void PromotingSomethingYouDoNotOwnSaysSoRatherThanInventingAList()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var build = kit.Intend("Maverick")!;

        kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        Assert.Contains("do not own", kit.Promote(build.Id), StringComparison.Ordinal);
    }

    /// <summary>
    /// The free slot count comes from the grade the plan is <em>aiming</em> at. Refusing a
    /// modification because the suit has no slots yet would be refusing the plan for being a plan.
    /// </summary>
    [Fact]
    public void TheFreeSlotCountFollowsThePlannedGradeRatherThanTheCurrentOne()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var kit = Service(install, store);

        var build = kit.Intend("Maverick")!;

        // Grade 1 has no modification slots at all, which is the ordering constraint.
        Assert.Null(kit.FirstFreeSlot(store.Find(build.Id)!, 1));

        kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 4));

        Assert.Equal(OnFootBuild.ModSlot(1), kit.FirstFreeSlot(store.Find(build.Id)!, 1));

        kit.Plan(build.Id, new KitPlan(OnFootBuild.ModSlot(1), Modification: "Night Vision"));

        Assert.Equal(OnFootBuild.ModSlot(2), kit.FirstFreeSlot(store.Find(build.Id)!, 1));
    }

    /// <summary>
    /// The suit being worn and the weapons carried are the index, and each keeps its own build.
    /// </summary>
    [Fact]
    public void TheIndexIsWhatYouAreWearingAndCarrying()
    {
        using var install = new TempInstall();
        var state = OnFoot();
        var kit = Service(install, Store(install), state);

        var carried = kit.Kit();

        Assert.Equal(2, carried.Count);
        Assert.Equal(OnFootKind.Suit, carried[0].Kind);
        Assert.Equal(3, carried[0].Grade);
        Assert.True(carried[0].IsCarried);
        Assert.Equal(OnFootKind.Weapon, carried[1].Kind);
    }

    /// <summary>
    /// Nothing named means the thing there is only one of. A second weapon makes it a question
    /// rather than a guess, which is the rule every other matcher in the repository follows.
    /// </summary>
    [Fact]
    public void NothingNamedMeansTheOneThingThereIsOneOf()
    {
        using var install = new TempInstall();
        var state = OnFoot();
        var kit = Service(install, Store(install), state);

        var suit = OnFootPlanService.Which(kit, null);

        Assert.NotNull(suit);
        Assert.Equal(OnFootKind.Suit, suit.Kind);

        Assert.NotNull(OnFootPlanService.Which(kit, null, weapon: true));
        Assert.NotNull(OnFootPlanService.Which(kit, "Maverick Suit"));
    }

    /// <summary>
    /// Dropping a plan keeps what it already put on the checklist: the Commander ordered their
    /// list around those lines, and silently removing them is a history that lies.
    /// </summary>
    [Fact]
    public void DroppingAPlanKeepsWhatItAlreadyPromoted()
    {
        using var install = new TempInstall();
        var store = Store(install);
        var state = OnFoot();
        var checklists = Checklists(install, state);
        var kit = new OnFootPlanService(store, checklists, () => state);

        var build = kit.BuildFor(OnFootKind.Suit, 1837009111675068, "Maverick Suit");

        kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));
        kit.Promote(build.Id);

        foreach (var pending in checklists.Proposals.Pending.ToList())
        {
            checklists.Accept(pending.Id);
        }

        Assert.NotEmpty(checklists.Document.Items);

        var said = kit.Delete(build.Id);

        Assert.Empty(store.Builds);
        Assert.NotEmpty(checklists.Document.Items);
        Assert.Contains("still there", said, StringComparison.Ordinal);
    }
}
