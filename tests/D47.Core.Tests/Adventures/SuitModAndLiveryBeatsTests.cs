using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Adventures;

public class SuitModAndLiveryBeatsTests
{
    private static AdventureStanding Fold(AdventureTrigger counted, params string[] events) =>
        FoldCounting(counted, "The Count", events);

    private static AdventureTrigger Of(TriggerKind kind, int count = 1, string? filter = null) =>
        new() { Kind = kind, Count = count, Filter = filter };

    private static string Suit(string suitMods, string weaponMods = "") =>
        $$"""{ "timestamp":"@", "event":"SuitLoadout", "SuitID":1, "SuitMods":[{{suitMods}}], "Modules":[{ "SlotName":"PrimaryWeapon1", "SuitModuleID":2, "WeaponMods":[{{weaponMods}}] }] }""";

    private static string Ship(int shipId, string paint, string decal = "decal_a") =>
        $$"""{ "timestamp":"@", "event":"Loadout", "ShipID":{{shipId}}, "Modules":[{ "Slot":"PaintJob", "Item":"{{paint}}" }, { "Slot":"Decal1", "Item":"{{decal}}" }, { "Slot":"ShipCockpit", "Item":"{{paint}}" }] }""";

    [Fact]
    public void AWeaponModCountsWhenItFirstAppearsAndNotWhenSeenAgain()
    {
        var trigger = Of(TriggerKind.SuitMod, 2, "weapon_stability");

        Assert.Equal(0, Fold(trigger, Suit("", "\"weapon_handling\""), Suit("", "\"weapon_handling\"")).Counted);
        Assert.Equal(1, Fold(trigger, Suit("", "\"weapon_handling\""), Suit("", "\"weapon_handling\",\"weapon_stability\"")).Counted);
        Assert.Equal(1, Fold(trigger, Suit("", "\"weapon_handling\""), Suit("", "\"weapon_handling\",\"weapon_stability\""), Suit("", "\"weapon_handling\",\"weapon_stability\"")).Counted);
    }

    [Fact]
    public void ASuitModFilterIgnoresOtherMods()
    {
        var trigger = Of(TriggerKind.SuitMod, 1, "suit_nightvision");

        Assert.Equal(1, Fold(trigger, Suit("\"suit_improvedjumpassist\""), Suit("\"suit_improvedjumpassist\",\"suit_increasedsprintduration\"")).Current);
        Assert.Equal(2, Fold(trigger, Suit("\"suit_improvedjumpassist\""), Suit("\"suit_improvedjumpassist\",\"suit_nightvision\"")).Current);
    }

    [Fact]
    public void AnUnfilteredSuitModBeatCountsEveryNewMod()
    {
        var trigger = Of(TriggerKind.SuitMod, 3);

        Assert.Equal(2, Fold(trigger, Suit("\"suit_a\""), Suit("\"suit_a\",\"suit_b\",\"suit_c\"")).Counted);
    }

    [Fact]
    public void ALiveryBeatFiresOnTheSecondChangedLoadoutAndNotOnAShipSeenFirst()
    {
        var trigger = Of(TriggerKind.Livery, 2);

        Assert.Equal(0, Fold(trigger, Ship(7, "paint_a")).Counted);
        Assert.Equal(1, Fold(trigger, Ship(7, "paint_a"), Ship(7, "paint_b")).Counted);
        Assert.Equal(1, Fold(trigger, Ship(7, "paint_a"), Ship(7, "paint_a")).Counted + 1);
        Assert.Equal(2, Fold(trigger, Ship(7, "paint_a"), Ship(7, "paint_b"), Ship(7, "paint_b", "decal_b")).Current);
    }

    [Fact]
    public void ACockpitChangeIsNotLivery()
    {
        var trigger = Of(TriggerKind.Livery, 1);

        Assert.Equal(1, Fold(trigger, Ship(7, "paint_a"), """{ "timestamp":"@", "event":"Loadout", "ShipID":7, "Modules":[{ "Slot":"PaintJob", "Item":"paint_a" }, { "Slot":"Decal1", "Item":"decal_a" }, { "Slot":"ShipCockpit", "Item":"other" }] }""").Current);
    }

    [Fact]
    public void ALoadoutSeenBeforeAcceptanceIsTheBaseline()
    {
        var adventure = LanternRoute(Accepted) with { Beats = [Beat("The Count", "catalyst", Of(TriggerKind.Livery), "Done.")] };
        var before = Event(Ship(7, "paint_a").Replace("@", Stamp(Accepted.AddMinutes(-5)), StringComparison.Ordinal));
        var after = Event(Ship(7, "paint_b").Replace("@", Stamp(Accepted.AddMinutes(2)), StringComparison.Ordinal));

        var standing = AdventureFold.Apply(AdventureFold.Apply(AdventureFold.Start(adventure), before), after);

        Assert.True(standing.IsDone);
    }

    [Fact]
    public void BothKindsAreWordsTheFileUses()
    {
        Assert.Contains("suitmod", AdventureValidation.Kinds);
        Assert.Contains("livery", AdventureValidation.Kinds);
    }

}
