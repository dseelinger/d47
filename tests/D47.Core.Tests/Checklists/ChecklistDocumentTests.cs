using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The two kinds, the three groups, and revision as a diff (Phase 17).
/// </summary>
public class ChecklistDocumentTests
{
    private static ChecklistDocument Empty => ChecklistDocument.For("F1", "Jameson");

    private static ChecklistItem Derived(string slot, string blueprint, int grade, ChecklistScope scope)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = blueprint,
            Grade = grade,
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = scope,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = $"Grade {grade} {blueprint} on {slot}",
            Intent = intent,
        };
    }

    [Fact]
    public void ADerivedItemRefusesAManualTickAndSaysWhy()
    {
        var scope = ChecklistScope.Ship(12);
        var document = Empty with { Items = [Derived("MainEngines", "Dirty Drive Tuning", 5, scope)] };

        var change = document.Complete(document.Items[0].Id);

        Assert.False(change.Changed);
        Assert.Contains("worked out from your journal", change.Report, StringComparison.Ordinal);

        // Not "quietly does nothing": the reason is the point, because the next journal read
        // would either undo the tick or leave it standing and lying.
        Assert.Contains("leave it standing and wrong", change.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAuthoredItemTicksAndUnticks()
    {
        var document = Empty.AddNote(ChecklistScope.Universal, "buy limpets").Document;
        var id = document.Items[0].Id;

        document = document.Complete(id).Document;
        Assert.True(document.Find(id)!.IsComplete);

        document = document.Uncomplete(id).Document;
        Assert.False(document.Find(id)!.IsComplete);
    }

    [Fact]
    public void CompletingIsNotRemoving()
    {
        var document = Empty.AddNote(ChecklistScope.Universal, "buy limpets").Document;
        document = document.Complete(document.Items[0].Id).Document;

        // Kept, checked. On something that runs for weeks, seeing how far you have come is most
        // of the point.
        Assert.Single(document.Items);
        Assert.True(document.Items[0].IsComplete);
    }

    [Fact]
    public void DeletingADerivedItemIsRefusedBecauseRevisingIsHowAPlanDropsOne()
    {
        var scope = ChecklistScope.Ship(12);
        var document = Empty with { Items = [Derived("MainEngines", "Dirty Drive Tuning", 5, scope)] };

        var change = document.Delete(document.Items[0].Id);

        Assert.False(change.Changed);
        Assert.Contains("revising the plan", change.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisingKeepsWhatBothVersionsShare()
    {
        var scope = ChecklistScope.Ship(12);

        var engines = Derived("MainEngines", "Dirty Drive Tuning", 5, scope);
        var boosters = Derived("Slot01", "Heavy Duty", 5, scope);

        var document = Empty with { Items = [engines, boosters with { State = ChecklistState.Done }] };

        // A revision that changes the engines and says nothing new about the boosters.
        var revised = document.Revise(
            scope,
            ChecklistSource.EngineeringPlan,
            [Derived("MainEngines", "Drive Strengthening", 5, scope), boosters]);

        var kept = revised.Document.Find(boosters.Id);

        Assert.NotNull(kept);
        Assert.True(kept.IsComplete);
        Assert.True(kept.IsLive);
    }

    /// <summary>
    /// Designed out means the <em>slot</em> left the plan, since Phase 26. Changing what is
    /// planned for a slot is an edit to the item rather than the loss of one — see
    /// <see cref="ChangingWhatIsPlannedForASlotKeepsTheItemAndItsHistory"/>.
    /// </summary>
    [Fact]
    public void AnItemDoneAndThenDesignedOutTombstonesAsSuperseded()
    {
        var scope = ChecklistScope.Ship(12);
        var cannon = Derived("Hardpoint1", "Overcharged", 5, scope);

        var document = Empty with { Items = [cannon with { State = ChecklistState.Done }] };

        // A revision that has stopped caring about the hardpoint entirely.
        var revised = document.Revise(
            scope,
            ChecklistSource.EngineeringPlan,
            [Derived("MainEngines", "Dirty Drive Tuning", 5, scope)]);

        var gone = revised.Document.Find(cannon.Id);

        Assert.NotNull(gone);
        Assert.False(gone.IsLive);

        // Not merely abandoned. The Commander really did spend that fortnight, and a history that
        // forgets it is a history that lies.
        Assert.Equal(ChecklistTombstone.Superseded, gone.Tombstone);
        Assert.Contains("done and then designed out", revised.Report, StringComparison.Ordinal);
    }

    /// <summary>
    /// Changing a slot from one blueprint to another is the same item changing its mind (Phase 26
    /// , "A plan is keyed to its slot").
    /// <para>
    /// This is what the slot key buys, seen from the outside: before it, the first edit to a slot
    /// tombstoned whatever history it had and opened an identical-looking new item beside the
    /// corpse — which is exactly the failure item identity exists to prevent, arriving through a
    /// door nobody had shut.
    /// </para>
    /// </summary>
    [Fact]
    public void ChangingWhatIsPlannedForASlotKeepsTheItemAndItsHistory()
    {
        var scope = ChecklistScope.Ship(12);
        var pulse = Derived("Hardpoint1", "Long Range", 5, scope);

        var document = Empty with { Items = [pulse] };

        var revised = document.Revise(
            scope,
            ChecklistSource.EngineeringPlan,
            [Derived("Hardpoint1", "Overcharged", 3, scope)]);

        // One item, still live, still the same identity — and nothing tombstoned beside it.
        var item = Assert.Single(revised.Document.Items);

        Assert.True(item.IsLive);
        Assert.True(item.Id.Same(pulse.Id));
    }

    [Fact]
    public void AnOpenItemDroppedByARevisionIsAbandonedRatherThanSuperseded()
    {
        var scope = ChecklistScope.Ship(12);
        var engines = Derived("MainEngines", "Dirty Drive Tuning", 5, scope);

        var revised = (Empty with { Items = [engines] })
            .Revise(scope, ChecklistSource.EngineeringPlan, [Derived("PowerPlant", "Armoured", 5, scope)]);

        Assert.Equal(ChecklistTombstone.Abandoned, revised.Document.Find(engines.Id)!.Tombstone);
    }

    [Fact]
    public void ARevisionThatBringsSomethingBackRestoresItsHistory()
    {
        var scope = ChecklistScope.Ship(12);
        var cannon = Derived("Hardpoint1", "Overcharged", 5, scope);

        var document = (Empty with { Items = [cannon with { State = ChecklistState.Done }] })
            .Revise(scope, ChecklistSource.EngineeringPlan, [Derived("Hardpoint1", "Sturdy", 5, scope)])
            .Document;

        // Changed their mind back. The item that was already earned comes back carrying it.
        var back = document.Revise(scope, ChecklistSource.EngineeringPlan, [cannon]).Document.Find(cannon.Id);

        Assert.NotNull(back);
        Assert.True(back.IsLive);
        Assert.True(back.IsComplete);
    }

    [Fact]
    public void RevisingOnePlanLeavesAnotherPlanInTheSameScopeAlone()
    {
        var scope = ChecklistScope.System("Sol");

        var facility = new ChecklistIntent(ChecklistIntentKind.Facility, "Sol 3") { Detail = "Coriolis" };

        var colonisation = new ChecklistItem
        {
            Key = ChecklistKeys.For(facility),
            Scope = scope,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.ColonisationPlan,
            Text = "Coriolis at Sol 3",
            Intent = facility,
        };

        var note = Empty.AddNote(scope, "ask Jim about the Krait build").Document.Items[0];

        var document = Empty with { Items = [colonisation, note] };

        var revised = document.Revise(scope, ChecklistSource.ColonisationPlan, []);

        // The Commander's own note survives a plan being emptied. Scope alone would not be enough
        // to keep the two apart, which is why the source is part of what a revision matches on.
        Assert.NotNull(revised.Document.Find(note.Id));
        Assert.True(revised.Document.Find(note.Id)!.IsLive);
    }

    [Fact]
    public void AplanCannotReviseTheCommandersOwnNotes()
    {
        var change = Empty.Revise(ChecklistScope.Universal, ChecklistSource.Commander, []);

        Assert.False(change.Changed);
    }

    [Theory]
    [InlineData(ChecklistItemKind.Derived, false, "says nothing about what to look for")]
    [InlineData(ChecklistItemKind.Authored, true, "tick nobody checked")]
    public void TheTwoKindsAreCheckedAgainstEachOther(ChecklistItemKind kind, bool withIntent, string expected)
    {
        var item = new ChecklistItem
        {
            Key = "k",
            Scope = ChecklistScope.Universal,
            Kind = kind,
            Text = "something",
            Intent = withIntent ? new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") : null,
        };

        Assert.Contains(expected, ChecklistValidation.Problem(item) ?? string.Empty, StringComparison.Ordinal);
    }
}
