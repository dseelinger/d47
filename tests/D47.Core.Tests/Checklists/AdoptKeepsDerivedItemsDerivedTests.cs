using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// <see cref="ChecklistDocument.Adopt"/> is what <c>ChecklistService.Adopt</c> uses to hand a document
/// saved before a Commander was known to the one who appears, so a derived item keeps its intent, key,
/// source and hull rather than arriving as a plain authored note the checklist can no longer check
/// against the journal.
/// </summary>
public class AdoptKeepsDerivedItemsDerivedTests
{
    [Fact]
    public void ADerivedItemAdoptedIntoAFreshDocumentStaysDerived()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "LargeHardpoint1")
        {
            Detail = "Weapon_LongRange",
            Grade = 5,
        };

        var item = new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(53),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Grade 5 Long Range Weapon on LargeHardpoint1",
            Intent = intent,
            State = ChecklistState.Open,
            Provenance = ChecklistProvenance.Asserted,
            Hull = "anaconda",
            Goal = "long-range-loadout",
        };

        var mine = ChecklistDocument.For("F1", "Jameson").Adopt(item);

        var adopted = Assert.Single(mine.Items);

        Assert.Equal(item.Key, adopted.Key);
        Assert.Equal(ChecklistItemKind.Derived, adopted.Kind);
        Assert.Equal(item.Intent, adopted.Intent);
        Assert.Equal(item.Source, adopted.Source);
        Assert.Equal(item.State, adopted.State);
        Assert.Equal(item.Provenance, adopted.Provenance);
        Assert.Equal(item.Hull, adopted.Hull);
        Assert.Equal(item.Goal, adopted.Goal);
    }

    [Fact]
    public void ADerivedItemAlreadyHeldUnderTheSameIdIsNotDuplicated()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "LargeHardpoint1")
        {
            Detail = "Weapon_LongRange",
            Grade = 5,
        };

        var item = new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(53),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Grade 5 Long Range Weapon on LargeHardpoint1",
            Intent = intent,
        };

        var mine = ChecklistDocument.For("F1", "Jameson") with { Items = [item] };

        var again = mine.Adopt(item with { Text = "A different sentence for the same slot" });

        Assert.Equal("Grade 5 Long Range Weapon on LargeHardpoint1", Assert.Single(again.Items).Text);
    }

    [Fact]
    public void AnAuthoredNoteAdoptedIntoAFreshDocumentKeepsItsTextAndState()
    {
        var note = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = "buy limpets",
            State = ChecklistState.Done,
        };

        var mine = ChecklistDocument.For("F1", "Jameson").Adopt(note);

        var adopted = Assert.Single(mine.Items);

        Assert.Equal("buy limpets", adopted.Text);
        Assert.Equal(ChecklistState.Done, adopted.State);
        Assert.Equal(ChecklistItemKind.Authored, adopted.Kind);
    }

    [Fact]
    public void AnAuthoredNoteAdoptedIntoADocumentAlreadyUsingItsKeyGetsAFreshOne()
    {
        var already = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = "refuel at Jameson Memorial",
        };

        var incoming = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = "buy limpets",
        };

        var mine = (ChecklistDocument.For("F1", "Jameson") with { Items = [already] }).Adopt(incoming);

        Assert.Equal(2, mine.Items.Count);
        Assert.Contains(mine.Items, other => other.Text == "refuel at Jameson Memorial" && other.Key == "note-1");
        Assert.Contains(mine.Items, other => other.Text == "buy limpets" && other.Key != "note-1");
    }
}
