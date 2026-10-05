using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Adventures;

public class SuitModAndLiveryBeatsTests
{
    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private static AdventureStanding Fold(AdventureTrigger counted, params string[] events)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", ToLantern, "Scoop here."),
                Beat("The Count", "catalyst", counted, "Done."),
            ],
        };

        var standing = AdventureFold.Apply(
            AdventureFold.Start(adventure),
            Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(1))}}", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }"""));

        return events.Select((json, index) => Event(json.Replace("@", Stamp(Accepted.AddMinutes(2 + index)), StringComparison.Ordinal)))
            .Aggregate(standing, AdventureFold.Apply);
    }

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

    [Fact]
    public async Task TheWritersBriefForbidsArxPurchasesAndNamesBothKinds()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying("""{"opening": "Again.", "reply": "Here.", "beats": [{"title": "The Ore", "function": "finale", "kind": "mine", "count": 20, "line": "Refined."}]}"""));

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(AdventureReach.Session, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 1, 10, 5)),
            Now,
            CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("No objective may need an ARX purchase", brief);
        Assert.Contains("\"suitmod\"", brief);
        Assert.Contains("\"livery\"", brief);
    }
}
