using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Every suit and weapon the Commander owns is still answerable after a restart.</summary>
public class KitOutlivesTheSessionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-kit").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string File_ => Path.Combine(_root, "kit.json");

    private KitStore Store() => new(File_, NullLogger<KitStore>.Instance);

    private static readonly DateTimeOffset Folded = new(2026, 8, 20, 5, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState State(KitStore? store, params string[] events)
    {
        var gameState = new GameStateStore
        {
            RestoreKit = fid => store?.For(fid),
        };

        gameState.Apply(Event(
            """{"timestamp":"2026-08-20T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var json in events)
        {
            gameState.Apply(Event(json));
        }

        return gameState.Active!;
    }

    private static string BoughtSuit(string timestamp, long suitId) =>
        $$"""
          {"timestamp":"{{timestamp}}","event":"BuySuit","Name":"tacticalsuit_class3",
           "Name_Localised":"Dominator Suit","Price":300000,"SuitID":{{suitId}},
           "SuitMods":["suit_increasedbackpackcapacity"]}
          """;

    private static string BoughtWeapon(string timestamp, long moduleId) =>
        $$"""
          {"timestamp":"{{timestamp}}","event":"BuyWeapon","Name":"Wpn_M_AssaultRifle_Kinetic_FAuto",
           "Name_Localised":"Karma AR-50","Class":3,"Price":125000,"SuitModuleID":{{moduleId}},
           "WeaponMods":["weapon_clipsize"]}
          """;

    /// <summary>A suit and a weapon, both round-tripped with their grade, modifications and SeenAt intact.</summary>
    [Fact]
    public void EveryItemGradeModificationAndSeenAtSurvivesTheRoundTrip()
    {
        var flown = State(
            store: null,
            BoughtSuit("2026-08-20T01:00:00Z", 1837009111675068),
            BoughtWeapon("2026-08-20T01:05:00Z", 1845784643934762));

        var writing = Store();
        writing.Save([flown], Folded);

        var reading = Store();
        reading.Load();

        var kit = reading.For("F1")!;

        var suit = kit.Suits[1837009111675068];
        Assert.Equal("tacticalsuit_class3", suit.Symbol);
        Assert.Equal(3, suit.Grade);
        Assert.Equal("suit_increasedbackpackcapacity", Assert.Single(suit.Modifications).Symbol);
        Assert.Equal(new DateTimeOffset(2026, 8, 20, 1, 0, 0, TimeSpan.Zero), suit.SeenAt);

        var weapon = kit.Weapons[1845784643934762];
        Assert.Equal("wpn_m_assaultrifle_kinetic_fauto", weapon.Symbol);
        Assert.Equal(3, weapon.Grade);
        Assert.Equal("weapon_clipsize", Assert.Single(weapon.Modifications).Symbol);
        Assert.Equal(new DateTimeOffset(2026, 8, 20, 1, 5, 0, TimeSpan.Zero), weapon.SeenAt);
    }

    /// <summary>A file for one Commander is not applied to another.</summary>
    [Fact]
    public void ACommandersKitIsNotAppliedToAnother()
    {
        var first = State(store: null, BoughtSuit("2026-08-20T01:00:00Z", 1));

        var store = Store();
        store.Save([first], Folded);

        var other = new GameStateStore();
        other.Apply(Event("""{"timestamp":"2026-08-21T00:00:00Z","event":"Commander","FID":"F2","Name":"Braben"}"""));
        other.Apply(Event(BoughtWeapon("2026-08-21T01:00:00Z", 2)));

        store.Save([other.Active!], Folded);

        var reading = Store();
        reading.Load();

        Assert.NotNull(reading.For("F1")!.Suits.GetValueOrDefault(1));
        Assert.Null(reading.For("F1")!.Weapons.GetValueOrDefault(2));

        Assert.NotNull(reading.For("F2")!.Weapons.GetValueOrDefault(2));
        Assert.Null(reading.For("F2")!.Suits.GetValueOrDefault(1));
    }

    /// <summary>Selling an item removes it from the file, not merely from memory.</summary>
    [Fact]
    public void ASoldSuitIsGoneFromTheFile()
    {
        var flown = State(
            store: null,
            BoughtSuit("2026-08-20T01:00:00Z", 1),
            """{"timestamp":"2026-08-20T02:00:00Z","event":"SellSuit","SuitID":1,"Name":"tacticalsuit_class3","Price":1,"SuitMods":[]}""");

        var store = Store();
        store.Save([flown], Folded);

        var reading = Store();
        reading.Load();

        Assert.Null(reading.For("F1")!.Suits.GetValueOrDefault(1));
    }

    /// <summary>Restoring merges stored under live rather than replacing what the session has already seen.</summary>
    [Fact]
    public void RestoringMergesStoredKitUnderWhatTheLiveSessionAlreadyHolds()
    {
        var stored = State(store: null, BoughtSuit("2026-08-20T01:00:00Z", 1));

        var store = Store();
        store.Save([stored], Folded);

        // A different weapon bought live, after the stored suit was already known.
        var restored = State(store, BoughtWeapon("2026-08-21T01:00:00Z", 2));

        Assert.NotNull(restored.Kit.Suits.GetValueOrDefault(1));
        Assert.NotNull(restored.Kit.Weapons.GetValueOrDefault(2));
    }

    [Fact]
    public void SaveIsOwedOnlyOnATickCarryingAMayChangeEvent()
    {
        Assert.True(OwnedKit.MayChange(Event(BoughtSuit("2026-08-20T01:00:00Z", 1))));
        Assert.True(OwnedKit.MayChange(Event(
            """{"timestamp":"2026-08-20T01:00:00Z","event":"SellWeapon","SuitModuleID":1,"Name":"x","Price":1,"WeaponMods":[]}""")));
        Assert.False(OwnedKit.MayChange(Event(
            """{"timestamp":"2026-08-20T01:00:00Z","event":"FSDJump","StarSystem":"Sol"}""")));
    }

    [Fact]
    public void AMissingOrUnreadableFileRebuildsRatherThanFailing()
    {
        var reading = Store();
        reading.Load();

        Assert.Null(reading.For("F1"));

        System.IO.File.WriteAllText(File_, "{ this is not json");

        var corrupt = Store();
        corrupt.Load();

        Assert.Null(corrupt.For("F1"));
    }
}
