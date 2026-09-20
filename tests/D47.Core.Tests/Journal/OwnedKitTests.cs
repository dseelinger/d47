using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Every suit and hand weapon the Commander owns, not only the one being worn.</summary>
public class OwnedKitTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void ASuitBoughtAndNeverWornIsStillOwned()
    {
        var kit = OwnedKit.Empty.Apply(Event(
            """
            {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1",
             "Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
            """));

        Assert.True(kit.Suits.TryGetValue(1837009111675068, out var suit));
        Assert.Equal("utilitysuit_class1", suit!.Symbol);
        Assert.Equal(1, suit.Grade);
        Assert.Empty(suit.Modifications);
    }

    [Fact]
    public void ABoughtWeaponReadsItsClassAndMods()
    {
        var kit = OwnedKit.Empty.Apply(Event(
            """
            {"timestamp":"2026-08-18T09:01:00Z","event":"BuyWeapon","Name":"Wpn_M_AssaultRifle_Kinetic_FAuto",
             "Name_Localised":"Karma AR-50","Class":3,"Price":125000,"SuitModuleID":1845784643934762,
             "WeaponMods":["weapon_clipsize"]}
            """));

        Assert.True(kit.Weapons.TryGetValue(1845784643934762, out var weapon));
        Assert.Equal("wpn_m_assaultrifle_kinetic_fauto", weapon!.Symbol);
        Assert.Equal(3, weapon.Grade);
        Assert.Single(weapon.Modifications);
    }

    [Fact]
    public void SellingASuitRemovesItById()
    {
        var kit = OwnedKit.Empty
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1",
                 "Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
                """))
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T10:00:00Z","event":"SellSuit","SuitID":1837009111675068,
                 "Name":"utilitysuit_class1","Name_Localised":"Maverick Suit","Price":90000,"SuitMods":[]}
                """));

        Assert.Empty(kit.Suits);
    }

    [Fact]
    public void SellingAWeaponRemovesItById()
    {
        var kit = OwnedKit.Empty
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T09:01:00Z","event":"BuyWeapon","Name":"Wpn_M_AssaultRifle_Kinetic_FAuto",
                 "Name_Localised":"Karma AR-50","Class":1,"Price":125000,"SuitModuleID":1845784643934762,
                 "WeaponMods":[]}
                """))
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T11:00:00Z","event":"SellWeapon","Name":"wpn_m_assaultrifle_kinetic_fauto",
                 "Name_Localised":"Karma AR-50","Class":1,"WeaponMods":[],"Price":30000,
                 "SuitModuleID":1845784643934762}
                """));

        Assert.Empty(kit.Weapons);
    }

    /// <summary><c>UpgradeSuit</c> carries the suit's old symbol beside its new class.</summary>
    [Fact]
    public void UpgradingASuitTakesTheGradeFromClassAndNotFromTheSymbolItCarries()
    {
        var kit = OwnedKit.Empty
            .Apply(Event(
                """
                {"timestamp":"2025-11-06T15:00:00Z","event":"BuySuit","Name":"tacticalsuit_class4",
                 "Name_Localised":"Dominator Suit","Price":750000,"SuitID":1845879835891144,"SuitMods":[]}
                """))
            .Apply(Event(
                """
                {"timestamp":"2025-11-06T15:47:34Z","event":"UpgradeSuit","Name":"tacticalsuit_class4",
                 "Name_Localised":"$TacticalSuit_Class1_Name;","SuitID":1845879835891144,"Class":5,
                 "Cost":4500000,"Resources":[]}
                """));

        var suit = kit.Suits[1845879835891144];

        Assert.Equal("tacticalsuit_class5", suit.Symbol);
        Assert.Equal(5, suit.Grade);
    }

    [Fact]
    public void UpgradingAWeaponSetsTheGradeFromClass()
    {
        var kit = OwnedKit.Empty
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T09:01:00Z","event":"BuyWeapon","Name":"Wpn_M_AssaultRifle_Kinetic_FAuto",
                 "Name_Localised":"Karma AR-50","Class":1,"Price":125000,"SuitModuleID":1845784643934762,
                 "WeaponMods":[]}
                """))
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T10:00:00Z","event":"UpgradeWeapon","Name":"wpn_m_assaultrifle_kinetic_fauto",
                 "Name_Localised":"Karma AR-50","SuitModuleID":1845784643934762,"Class":4,"Cost":900000,
                 "Resources":[]}
                """));

        Assert.Equal(4, kit.Weapons[1845784643934762].Grade);
    }

    [Fact]
    public void ASuitLoadoutAddsTheSuitAndEveryModuleInIt()
    {
        var kit = OwnedKit.Empty.Apply(Event(
            """
            {"timestamp":"2025-11-21T03:16:17Z","event":"SuitLoadout","SuitID":1845879835891144,
             "SuitName":"utilitysuit_class5","SuitName_Localised":"$UtilitySuit_Class1_Name;",
             "SuitMods":["suit_improvedjumpassist"],
             "LoadoutID":4293000001,"LoadoutName":"Sneaky-Snipy",
             "Modules":[
               {"SlotName":"PrimaryWeapon1","SuitModuleID":1845880282772980,
                "ModuleName":"wpn_m_sniper_plasma_charged","ModuleName_Localised":"Manticore Executioner",
                "Class":5,"WeaponMods":["weapon_clipsize"]}]}
            """));

        Assert.True(kit.Suits.ContainsKey(1845879835891144));
        Assert.True(kit.Weapons.ContainsKey(1845880282772980));
        Assert.Equal(5, kit.Weapons[1845880282772980].Grade);
    }

    [Fact]
    public void TheFlightSuitIsNotRecordedAsOwned()
    {
        var kit = OwnedKit.Empty.Apply(Event(
            """
            {"timestamp":"2025-08-30T00:29:50Z","event":"SuitLoadout","SuitID":1841811120917527,
             "SuitName":"flightsuit","SuitName_Localised":"Flight Suit","SuitMods":[],
             "LoadoutID":4293000000,"LoadoutName":"Default loadout","Modules":[]}
            """));

        Assert.Empty(kit.Suits);
    }

    [Fact]
    public void LoadoutEquipModuleAddsOrOverwritesThatOneWeapon()
    {
        var kit = OwnedKit.Empty
            .Apply(Event(
                """
                {"timestamp":"2025-11-21T03:16:17Z","event":"SuitLoadout","SuitID":1845879835891144,
                 "SuitName":"utilitysuit_class5","SuitMods":[],
                 "LoadoutID":4293000001,"LoadoutName":"Sneaky-Snipy","Modules":[]}
                """))
            .Apply(Event(
                """
                {"timestamp":"2025-11-21T03:20:00Z","event":"LoadoutEquipModule","LoadoutName":"Sneaky-Snipy",
                 "SuitID":1845879835891144,"LoadoutID":4293000001,"SlotName":"PrimaryWeapon1",
                 "SuitModuleID":1845880282772980,"ModuleName":"wpn_m_sniper_plasma_charged",
                 "ModuleName_Localised":"Manticore Executioner","Class":5,"WeaponMods":[]}
                """));

        Assert.True(kit.Weapons.ContainsKey(1845880282772980));
    }

    [Fact]
    public void LatestEventWinsWhenTheSameWeaponIsSeenTwice()
    {
        var kit = OwnedKit.Empty
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T09:01:00Z","event":"BuyWeapon","Name":"Wpn_M_AssaultRifle_Kinetic_FAuto",
                 "Name_Localised":"Karma AR-50","Class":1,"Price":125000,"SuitModuleID":1845784643934762,
                 "WeaponMods":[]}
                """))
            .Apply(Event(
                """
                {"timestamp":"2026-08-18T09:05:00Z","event":"LoadoutEquipModule","LoadoutName":"Default",
                 "SuitID":1845879835891144,"LoadoutID":4293000001,"SlotName":"PrimaryWeapon1",
                 "SuitModuleID":1845784643934762,"ModuleName":"wpn_m_assaultrifle_kinetic_fauto",
                 "ModuleName_Localised":"Karma AR-50","Class":2,"WeaponMods":["weapon_clipsize"]}
                """));

        Assert.Equal(2, kit.Weapons[1845784643934762].Grade);
        Assert.Single(kit.Weapons[1845784643934762].Modifications);
    }
}
