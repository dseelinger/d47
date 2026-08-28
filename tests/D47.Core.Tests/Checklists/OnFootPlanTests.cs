using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Planning a suit or a weapon on the Phase 17 substrate (Phase 20, "Plan a suit or a
/// weapon").
/// <para>
/// <b>Permanence is what these are about.</b> Four slots, no undo, and a wrong modification
/// recoverable only by buying and re-upgrading a fresh item — so the two things that must never go
/// wrong are the ordering (grade before modifications) and the identity (a plan surviving the
/// upgrade it was written to describe).
/// </para>
/// </summary>
public class OnFootPlanTests
{
    private static readonly ChecklistScope Suit = ChecklistScope.Suit(1845879835891144);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState Wearing(string suitSymbol, string mods = "")
    {
        var store = new GameStateStore();

        store.Apply(Event(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        store.Apply(Event(
            $$"""
            {"timestamp":"3311-01-01T00:01:00Z","event":"SuitLoadout","SuitID":1845879835891144,
             "SuitName":"{{suitSymbol}}","SuitMods":[{{mods}}],"LoadoutID":4293000001,
             "LoadoutName":"Sneaky-Snipy","Modules":[]}
            """));

        return store.Active!;
    }

    [Fact]
    public void TheGradeComesFirstBecauseAGradeOneItemHasNoSlots()
    {
        var items = OnFootPlan.Items(
            Suit, new OnFootRequest("Maverick", 5, ["Night vision", "Quieter footsteps"]));

        Assert.Equal(3, items.Count);

        // Order is the deliverable here, not presentation: an engineer's base has no Pioneer
        // Supplies, so a modification attempted first is a wasted trip rather than a wasted turn.
        Assert.Equal(ChecklistIntentKind.Grade, items[0].Intent?.Kind);
        Assert.All(items.Skip(1), item => Assert.Equal(ChecklistIntentKind.Modification, item.Intent?.Kind));
    }

    /// <summary>
    /// The whole reason the scope is keyed on <c>SuitID</c> rather than on the symbol: a Maverick
    /// taken from grade 3 to grade 5 is the same suit, and a plan that lost its list at the moment
    /// the upgrade landed would abandon itself on success.
    /// </summary>
    [Fact]
    public void APlanSurvivesTheUpgradeItWasWrittenToDescribe()
    {
        var before = OnFootPlan.Items(Suit, new OnFootRequest("Maverick", 5, ["Night vision"]));
        var after = OnFootPlan.Items(Suit, new OnFootRequest("Maverick", 5, ["Night vision"]));

        Assert.Equal(before.Select(item => item.Key), after.Select(item => item.Key));
        Assert.All(before, item => Assert.Equal(ChecklistGroup.Suit, item.Scope.Group));
    }

    [Fact]
    public void ARoutingCriticalModificationCarriesItsOneEngineer()
    {
        var items = OnFootPlan.Items(Suit, new OnFootRequest("Maverick", null, ["Night vision"]));

        // One Bubble source, so naming it is a routing fact rather than a preference.
        Assert.Equal("Oden Geiger", Assert.Single(items).Intent?.Engineer);
    }

    [Fact]
    public void AModificationSeveralEngineersOfferNamesNoneOfThem()
    {
        var items = OnFootPlan.Items(Suit, new OnFootRequest("Maverick", null, ["Damage resistance"]));

        // Pinning it to whichever came first would invent a trip the Commander never chose.
        Assert.Null(Assert.Single(items).Intent?.Engineer);
    }

    /// <summary>
    /// Every step, not just the last one. A Commander at grade 2 asking for 5 pays for 3, 4 and 5,
    /// and quoting only the last understates the trip by two thirds.
    /// </summary>
    [Fact]
    public void CostingAGradeCoversEveryStepBetweenHereAndThere()
    {
        var items = OnFootPlan.Items(Suit, new OnFootRequest("Maverick", 5));
        var costing = OnFootPlan.Cost(items, Wearing("utilitysuit_class3"));

        var schematics = Assert.Single(
            costing.Ingredients, ingredient => ingredient.Material.Symbol == "suitschematic");

        // Grade 4 takes 4 and grade 5 takes 5, measured against the game.
        Assert.Equal(9, schematics.Needed);
        Assert.Empty(costing.Uncovered);
    }

    /// <summary>
    /// Held comes from the backpack and the locker, never from the ship's material store. Totalling
    /// micro-resources against a ship material's per-grade cap produces a feasibility verdict that
    /// is nonsense delivered confidently.
    /// </summary>
    [Fact]
    public void ShortfallIsNettedAgainstWhatIsCarriedOnFoot()
    {
        var items = OnFootPlan.Items(Suit, new OnFootRequest("Maverick", 4));
        var costing = OnFootPlan.Cost(items, Wearing("utilitysuit_class3"));

        Assert.All(costing.Ingredients, ingredient =>
            Assert.Equal(D47.Core.Knowledge.MaterialLedger.ShipLocker, ingredient.Material.Ledger));

        // Nothing carried in the fixture, so the shortfall is the whole recipe.
        Assert.All(costing.Shortfall, ingredient => Assert.Equal(ingredient.Needed, ingredient.Short));
    }

    /// <summary>
    /// Three modifications have a different recipe per weapon manufacturer and the journal has one
    /// symbol for all three. Costing one arbitrarily quotes a Takada price for a Manticore.
    /// </summary>
    [Fact]
    public void APerManufacturerModificationRefusesToTotalRatherThanPickingOne()
    {
        var items = OnFootPlan.Items(
            ChecklistScope.Weapon(1845880282772980),
            new OnFootRequest("Manticore Executioner", null, ["Greater range"]));

        var costing = OnFootPlan.Cost(items, Wearing("utilitysuit_class5"));

        Assert.Empty(costing.Ingredients);
        Assert.Contains(costing.Gates, gate => gate.Contains("per manufacturer", StringComparison.Ordinal));
    }

    // ---- What the journal says about it -----------------------------------------------------

    [Fact]
    public void AGradeItemIsDoneWhenTheSuitReachesIt()
    {
        var item = Assert.Single(OnFootPlan.Items(Suit, new OnFootRequest("Maverick", 4)));

        Assert.Equal(
            ChecklistState.Open,
            ChecklistEvaluator.Evaluate(item, Wearing("utilitysuit_class3"))?.State);

        Assert.Equal(
            ChecklistState.Done,
            ChecklistEvaluator.Evaluate(item, Wearing("utilitysuit_class5"))?.State);
    }

    [Fact]
    public void AModificationOnAGradeOneSuitIsBlockedRatherThanOpen()
    {
        var item = Assert.Single(
            OnFootPlan.Items(Suit, new OnFootRequest("Maverick", null, ["Night vision"])));

        var verdict = ChecklistEvaluator.Evaluate(item, Wearing("utilitysuit_class1"));

        // Not a shortage. Gathering fixes nothing here — it is an ordering problem, and the fix is
        // Pioneer Supplies, which the engineer's base does not have.
        Assert.Equal(ChecklistState.Blocked, verdict?.State);
        Assert.Contains("Pioneer Supplies", verdict?.Reason ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void AFittedModificationReadsAsDoneWithoutAnybodyTickingIt()
    {
        var item = Assert.Single(
            OnFootPlan.Items(Suit, new OnFootRequest("Maverick", null, ["Quieter footsteps"])));

        var verdict = ChecklistEvaluator.Evaluate(
            item, Wearing("utilitysuit_class5", "\"suit_quieterfootsteps\""));

        Assert.Equal(ChecklistState.Done, verdict?.State);
        Assert.False(item.TicksByHand);
    }

    /// <summary>
    /// A symbol nothing joins is not evidence of absence. Same contract a ship blueprint has, and
    /// for the same reason: guessing "not fitted" would tell a Commander to spend four slots' worth
    /// of permanent decisions twice.
    /// </summary>
    [Fact]
    public void AModificationWhoseSpellingCannotBeJoinedIsUnverifiedRatherThanOpen()
    {
        var item = Assert.Single(
            OnFootPlan.Items(Suit, new OnFootRequest("Maverick", null, ["Night vision"])));

        var verdict = ChecklistEvaluator.Evaluate(
            item, Wearing("utilitysuit_class5", "\"suit_somethingfrontieraddedlater\""));

        Assert.Equal(ChecklistState.Unverified, verdict?.State);
    }

    [Fact]
    public void AllFourSlotsSpentIsBlockedAndSaysWhyItCannotBeUndone()
    {
        var item = Assert.Single(
            OnFootPlan.Items(Suit, new OnFootRequest("Maverick", null, ["Night vision"])));

        var verdict = ChecklistEvaluator.Evaluate(
            item,
            Wearing(
                "utilitysuit_class5",
                "\"suit_quieterfootsteps\",\"suit_improvedjumpassist\","
                + "\"suit_increasedsprintduration\",\"suit_increasedbatterycapacity\""));

        Assert.Equal(ChecklistState.Blocked, verdict?.State);
        Assert.Contains("fresh item", verdict?.Reason ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// A plan about a suit the Commander is not in says nothing rather than saying "not done" —
    /// the stored verdict then stands as of when it was taken.
    /// </summary>
    [Fact]
    public void APlanAboutADifferentSuitAnswersNothingAtAll()
    {
        var item = Assert.Single(
            OnFootPlan.Items(ChecklistScope.Suit(999), new OnFootRequest("Maverick", 4)));

        Assert.Null(ChecklistEvaluator.Evaluate(item, Wearing("utilitysuit_class3")));
    }
}
