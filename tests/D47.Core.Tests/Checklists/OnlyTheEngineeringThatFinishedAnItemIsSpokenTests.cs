using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// An item is said to be done only on the tick whose events did the engineering that finished it. Every
/// other move to Done ticks the item with nothing said (#446).
/// </summary>
public class OnlyTheEngineeringThatFinishedAnItemIsSpokenTests
{
    private const int ShipId = 37;

    private const long SuitId = 1845879835891144;

    private const long WeaponId = 1845784702022915;

    [Fact]
    public void TheLoginLoadoutTicksABlueprintSilently()
    {
        using var install = new TempInstall();
        var (game, checklists) = AtLogin(install);
        var item = Save(checklists, Blueprint("MainEngines"));

        var loadout = Loadout(grade: 5);
        game.Apply(loadout);

        Assert.DoesNotContain(checklists.Poll(events: [loadout]), Done);
        Assert.Empty(checklists.Drain());
        Assert.Equal(ChecklistState.Done, Find(checklists, item).State);
    }

    [Fact]
    public void AnEngineerCraftOnTheSlotIsSpoken()
    {
        using var install = new TempInstall();
        var (game, checklists) = Aboard(install, grade: 4);
        var item = Save(checklists, Blueprint("MainEngines"));
        checklists.Poll();

        var craft = Craft("MainEngines", level: 5);
        game.Apply(craft);
        checklists.Poll(events: [craft]);

        var done = Assert.Single(checklists.Drain());
        Assert.Equal("5A Thrusters is at grade 5 and finished.", done.Text);
        Assert.Equal(ChecklistState.Done, Find(checklists, item).State);
    }

    [Fact]
    public void AnExperimentalIsSpokenOnTheCraftThatAppliedIt()
    {
        using var install = new TempInstall();
        var (game, checklists) = Aboard(install, grade: 5);
        var item = Save(checklists, new ChecklistIntent(ChecklistIntentKind.Experimental, "MainEngines")
        {
            Detail = "Drag Drives",
        });
        checklists.Poll();
        Assert.Equal(ChecklistState.Open, Find(checklists, item).State);

        var craft = Event(
            """
            {"timestamp":"2026-09-24T14:00:00Z","event":"EngineerCraft","Slot":"MainEngines",
             "Module":"int_engine_size5_class5","ApplyExperimentalEffect":"special_engine_overloaded",
             "ApplyExperimentalEffect_Localised":"Drag Drives","Engineer":"Palin","EngineerID":300220,
             "BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0}
            """);
        game.Apply(craft);
        checklists.Poll(events: [craft]);

        Assert.Single(checklists.Drain(), Done);
        Assert.Equal(ChecklistState.Done, Find(checklists, item).State);
    }

    [Fact]
    public void AnEngineerCraftOnAnotherSlotSaysNothing()
    {
        using var install = new TempInstall();
        var (game, checklists) = AtLogin(install);
        var item = Save(checklists, Blueprint("MainEngines"));

        var loadout = Loadout(grade: 5);
        game.Apply(loadout);
        checklists.Poll(events: [loadout, Craft("Radar", level: 5)]);

        Assert.Empty(checklists.Drain());
        Assert.Equal(ChecklistState.Done, Find(checklists, item).State);
    }

    [Fact]
    public void AModuleFittedInOutfittingSaysNothing()
    {
        using var install = new TempInstall();
        var (game, checklists) = AtLogin(install);
        var item = Save(checklists, new ChecklistIntent(ChecklistIntentKind.Module, "MainEngines")
        {
            Detail = "Thrusters",
        });
        checklists.Poll(announce: false);

        var loadout = Loadout(grade: null);
        game.Apply(loadout);
        checklists.Poll(events: [loadout]);

        Assert.Empty(checklists.Drain());
        Assert.Equal(ChecklistState.Done, Find(checklists, item).State);
    }

    [Fact]
    public void ASuitGradeIsSpokenOnTheUpgradeAndNotOnTheSnapshot()
    {
        using var install = new TempInstall();
        var game = Commander();
        var checklists = TestSurface.Checklists(install.Paths, game);

        var wearing = SuitLoadout("utilitysuit_class3");
        game.Apply(wearing);

        var grade = Assert.Single(
            OnFootPlan.Items(ChecklistScope.Suit(SuitId), new OnFootRequest("Maverick", 4)),
            item => item.Intent?.Kind == ChecklistIntentKind.Grade);
        Save(checklists, grade);
        checklists.Poll();
        Assert.Empty(checklists.Drain());

        var upgrade = Event(
            $$"""
            {"timestamp":"2026-09-24T14:00:00Z","event":"UpgradeSuit","Name":"utilitysuit_class3",
             "SuitID":{{SuitId}},"Class":4,"Cost":1500000}
            """);
        game.Apply(upgrade);
        checklists.Poll(events: [upgrade]);

        Assert.Single(checklists.Drain(), Done);
        Assert.Equal(ChecklistState.Done, Find(checklists, grade).State);

        // The same grade reached by a SuitLoadout alone, on a fresh install, is not spoken.
        using var other = new TempInstall();
        var elsewhere = Commander();
        var silent = TestSurface.Checklists(other.Paths, elsewhere);
        elsewhere.Apply(wearing);
        Save(silent, grade);
        silent.Poll();

        var snapshot = SuitLoadout("utilitysuit_class4");
        elsewhere.Apply(snapshot);
        silent.Poll(events: [snapshot]);

        Assert.Empty(silent.Drain());
        Assert.Equal(ChecklistState.Done, Find(silent, grade).State);
    }

    [Fact]
    public void AWeaponGradeIsSpokenOnTheUpgrade()
    {
        using var install = new TempInstall();
        var game = Commander();
        var checklists = TestSurface.Checklists(install.Paths, game);

        game.Apply(Event(
            $$"""
            {"timestamp":"2026-09-24T13:00:00Z","event":"SuitLoadout","SuitID":{{SuitId}},
             "SuitName":"utilitysuit_class3","SuitMods":[],"LoadoutID":4293000001,"LoadoutName":"Fixture",
             "Modules":[{"SlotName":"PrimaryWeapon1","SuitModuleID":{{WeaponId}},
               "ModuleName":"wpn_s_pistol_plasma_charged","Class":2,"WeaponMods":[]}]}
            """));

        var grade = Assert.Single(
            OnFootPlan.Items(ChecklistScope.Weapon(WeaponId), new OnFootRequest("Manticore Tormentor", 3)),
            item => item.Intent?.Kind == ChecklistIntentKind.Grade);
        Save(checklists, grade);
        checklists.Poll();
        Assert.Empty(checklists.Drain());

        var upgrade = Event(
            $$"""
            {"timestamp":"2026-09-24T14:00:00Z","event":"UpgradeWeapon","Name":"wpn_s_pistol_plasma_charged",
             "Class":3,"SuitModuleID":{{WeaponId}},"Cost":750000}
            """);
        game.Apply(upgrade);
        checklists.Poll(events: [upgrade]);

        Assert.Single(checklists.Drain(), Done);
        Assert.Equal(ChecklistState.Done, Find(checklists, grade).State);
    }

    [Fact]
    public void AnEngineerUnlockedLiveSaysNothing()
    {
        using var install = new TempInstall();
        var game = Commander();
        var checklists = TestSurface.Checklists(install.Paths, game);

        game.Apply(Event(
            """
            {"timestamp":"2026-09-24T13:00:00Z","event":"EngineerProgress","Engineer":"Liz Ryder",
             "EngineerID":300080,"Progress":"Invited"}
            """));

        var access = new ChecklistIntent(ChecklistIntentKind.EngineerAccess, "Liz Ryder") { Grade = 1 };
        var item = Save(checklists, new ChecklistItem
        {
            Key = ChecklistKeys.For(access),
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Unlock Liz Ryder",
            Intent = access,
        });
        checklists.Poll();
        Assert.Empty(checklists.Drain());

        var unlocked = Event(
            """
            {"timestamp":"2026-09-24T14:00:00Z","event":"EngineerProgress","Engineer":"Liz Ryder",
             "EngineerID":300080,"Progress":"Unlocked","Rank":1}
            """);
        game.Apply(unlocked);
        checklists.Poll(events: [unlocked]);

        Assert.Empty(checklists.Drain());
        Assert.Equal(ChecklistState.Done, Find(checklists, item).State);
    }

    [Fact]
    public void ALowerGradeInALoadoutIsStillSaidToBeUndone()
    {
        using var install = new TempInstall();
        var (game, checklists) = Aboard(install, grade: 5);
        Save(checklists, Blueprint("MainEngines"));
        checklists.Poll(announce: false);

        var loadout = Loadout(grade: 3);
        game.Apply(loadout);
        checklists.Poll(events: [loadout]);

        var undone = Assert.Single(checklists.Drain());
        Assert.StartsWith("checklist.undone.", undone.Key, StringComparison.Ordinal);
    }

    private static bool Done(ChecklistNews news) => news.Key.StartsWith("checklist.done.", StringComparison.Ordinal);

    private static ChecklistIntent Blueprint(string slot) => new(ChecklistIntentKind.Blueprint, slot)
    {
        Detail = "Engine_Dirty",
        Grade = 5,
    };

    private static GameStateStore Commander()
    {
        var game = new GameStateStore();
        game.Apply(Event("""{"timestamp":"2026-09-24T13:15:20Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        return game;
    }

    /// <summary>At <c>LoadGame</c>, the loadout not yet written, past the priming tick.</summary>
    private static (GameStateStore Game, ChecklistService Checklists) AtLogin(TempInstall install)
    {
        var game = Commander();
        game.Apply(Event(
            $$"""
            {"timestamp":"2026-09-24T13:15:27Z","event":"LoadGame","FID":"F1","Commander":"Jameson",
             "Ship":"cobramkv","ShipID":{{ShipId}}}
            """));

        var checklists = TestSurface.Checklists(install.Paths, game);
        checklists.Poll(announce: false);
        return (game, checklists);
    }

    /// <summary>Aboard with the thrusters at a grade.</summary>
    private static (GameStateStore Game, ChecklistService Checklists) Aboard(TempInstall install, int grade)
    {
        var game = Commander();
        game.Apply(Loadout(grade));
        return (game, TestSurface.Checklists(install.Paths, game));
    }

    private static ChecklistItem Save(ChecklistService checklists, ChecklistIntent intent) =>
        Save(checklists, new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(ShipId),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = intent.Detail ?? intent.Subject,
            Intent = intent,
            Hull = "cobramkv",
        });

    private static ChecklistItem Save(ChecklistService checklists, ChecklistItem item)
    {
        checklists.List.Save([ChecklistDocument.For("F1", "Jameson") with { Items = [item] }]);
        return item;
    }

    private static ChecklistItem Find(ChecklistService checklists, ChecklistItem item) =>
        Assert.Single(checklists.Document.Items, candidate => candidate.Id.Same(item.Id));

    private static JournalEvent Loadout(int? grade)
    {
        var engineering = grade is { } level
            ? $$""","Engineering":{"BlueprintName":"Engine_Dirty","Level":{{level}},"Quality":1.0}"""
            : string.Empty;

        return Event(
            $$"""
            {"timestamp":"2026-09-24T13:15:47Z","event":"Loadout","Ship":"cobramkv","ShipID":{{ShipId}},
             "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true{{engineering}}}]}
            """);
    }

    private static JournalEvent Craft(string slot, int level) => Event(
        $$"""
        {"timestamp":"2026-09-24T14:00:00Z","event":"EngineerCraft","Slot":"{{slot}}",
         "Module":"int_engine_size5_class5","Engineer":"Palin","EngineerID":300220,
         "BlueprintName":"Engine_Dirty","Level":{{level}},"Quality":1.0}
        """);

    private static JournalEvent SuitLoadout(string suit) => Event(
        $$"""
        {"timestamp":"2026-09-24T13:00:00Z","event":"SuitLoadout","SuitID":{{SuitId}},
         "SuitName":"{{suit}}","SuitMods":[],"LoadoutID":4293000001,"LoadoutName":"Fixture","Modules":[]}
        """);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
