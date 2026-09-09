using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The decision the whole phase rests on: two plans can only be diffed if an item knows what
/// it is independently of its position in a list.
/// </summary>
public class ChecklistIdentityTests
{
    private static ChecklistIntent Dirty(int? grade = 5) =>
        new(ChecklistIntentKind.Blueprint, "MainEngines") { Detail = "Dirty Drive Tuning", Grade = grade };

    [Fact]
    public void TheSameIntentSaidDifferentlyIsTheSameItem()
    {
        // The separator is the trap. "MainEngines" and "main engines" are one slot, and a key that told them
        // apart would make a plan restated in a differently-worded conversation read as everything removed
        // and an identical set added.
        var spoken = new ChecklistIntent(ChecklistIntentKind.Blueprint, "main engines")
        {
            Detail = "dirty  drive-tuning",
            Grade = 5,
        };

        Assert.Equal(ChecklistKeys.For(Dirty()), ChecklistKeys.For(spoken));
    }

    [Fact]
    public void PositionInTheListIsNotPartOfIdentity()
    {
        var scope = ChecklistScope.Ship(12);

        var first = EngineeringPlan.Items(scope, "Krait_MkII",
            [new BuildRequest("MainEngines", "Dirty Drive Tuning", 5), new BuildRequest("PowerPlant", "Armoured", 5)]);

        var reordered = EngineeringPlan.Items(scope, "Krait_MkII",
            [new BuildRequest("PowerPlant", "Armoured", 5), new BuildRequest("MainEngines", "Dirty Drive Tuning", 5)]);

        Assert.Equal(
            first.Select(item => item.Key).Order(StringComparer.Ordinal),
            reordered.Select(item => item.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>Deciding on a grade for a slot planned without one is the same item changing its
    /// mind.</summary>
    [Fact]
    public void ChangingTheGradeOnASlotIsTheSameItem()
    {
        Assert.Equal(ChecklistKeys.For(Dirty()), ChecklistKeys.For(Dirty(grade: null)));
    }

    /// <summary>And so is changing what is planned for it.</summary>
    [Fact]
    public void ADifferentBlueprintInTheSameSlotIsTheSameItem()
    {
        var burst = new ChecklistIntent(ChecklistIntentKind.Blueprint, "Hardpoint1") { Detail = "Sturdy", Grade = 5 };
        var cannon = new ChecklistIntent(ChecklistIntentKind.Blueprint, "Hardpoint1") { Detail = "Overcharged", Grade = 5 };

        Assert.Equal(ChecklistKeys.For(burst), ChecklistKeys.For(cannon));
    }

    /// <summary>
    /// A slot still holds a blueprint and an experimental at once, because those are two things the
    /// game lets one module carry — so the kind is in the key even though the content is not.
    /// </summary>
    [Fact]
    public void ABlueprintAndAnExperimentalOnOneSlotAreStillTwoItems()
    {
        var blueprint = new ChecklistIntent(ChecklistIntentKind.Blueprint, "Hardpoint1") { Detail = "Sturdy" };
        var effect = new ChecklistIntent(ChecklistIntentKind.Experimental, "Hardpoint1") { Detail = "Thermal Shock" };

        Assert.NotEqual(ChecklistKeys.For(blueprint), ChecklistKeys.For(effect));
    }

    /// <summary>And the one-to-many kinds still key on what they are about.</summary>
    [Fact]
    public void TheOneToManyKindsStillKeyOnTheirContent()
    {
        var stability = new ChecklistIntent(ChecklistIntentKind.Modification, "Maverick") { Detail = "Extra Ammo Capacity" };
        var reach = new ChecklistIntent(ChecklistIntentKind.Modification, "Maverick") { Detail = "Greater Range" };

        Assert.NotEqual(ChecklistKeys.For(stability), ChecklistKeys.For(reach));

        var steel = new ChecklistIntent(ChecklistIntentKind.Commodity, "Orbital 1") { Detail = "Steel" };
        var titanium = new ChecklistIntent(ChecklistIntentKind.Commodity, "Orbital 1") { Detail = "Titanium" };

        Assert.NotEqual(ChecklistKeys.For(steel), ChecklistKeys.For(titanium));
    }

    [Fact]
    public void AnAuthoredKeyIsTheLowestUnusedNumberAndSkipsTombstones()
    {
        var scope = ChecklistScope.Universal;

        ChecklistItem Note(string key, ChecklistTombstone tombstone = ChecklistTombstone.None) => new()
        {
            Key = key,
            Scope = scope,
            Kind = ChecklistItemKind.Authored,
            Text = key,
            Tombstone = tombstone,
        };

        // note-2 is gone but its key is not free: reusing it would graft the old item's history onto a new
        // one.
        Assert.Equal("note-3", ChecklistKeys.Note([Note("note-1"), Note("note-2", ChecklistTombstone.Abandoned)]));
    }

    [Fact]
    public void AnAuthoredKeySurvivesTheWordingChanging()
    {
        var document = ChecklistDocument.For("F1").AddNote(ChecklistScope.Universal, "buy limpets").Document;
        var item = document.Items.Single();

        // The whole reason an authored key is minted rather than derived from the text: editing the wording
        // must not be delete-and-recreate, which would lose the tick.
        var edited = item with { Text = "buy collector limpets" };

        Assert.True(item.Id.Same(edited.Id));
    }

    [Fact]
    public void TwoShipsDoNotShareAKey()
    {
        var one = new ChecklistItemId(ChecklistScope.Ship(12), "bp/mainengines/dirty-drive-tuning/g5");
        var two = new ChecklistItemId(ChecklistScope.Ship(13), "bp/mainengines/dirty-drive-tuning/g5");

        Assert.False(one.Same(two));
    }
}
