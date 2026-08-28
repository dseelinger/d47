using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// What the Commander is wearing (Phase 20, "d47 knows what you are wearing").
/// <para>
/// <b>Every event body below is a real one</b>, copied out of a 912-journal corpus rather than
/// invented — including the broken localisation, which is not an oddity to guard against but the
/// common case: 269 of 768 <c>SuitLoadout</c> events carry an unresolved token, and every one of
/// them says Class1 whatever the suit's real class is.
/// </para>
/// </summary>
public class OnFootLoadoutTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A real grade 5 Maverick with two modifications and two weapons.</summary>
    private const string Sneaky =
        """
        {"timestamp":"2025-11-21T03:16:17Z","event":"SuitLoadout","SuitID":1845879835891144,
         "SuitName":"utilitysuit_class5","SuitName_Localised":"$UtilitySuit_Class1_Name;",
         "SuitMods":["suit_improvedjumpassist","suit_quieterfootsteps"],
         "LoadoutID":4293000001,"LoadoutName":"Sneaky-Snipy",
         "Modules":[
           {"SlotName":"PrimaryWeapon1","SuitModuleID":1845880282772980,
            "ModuleName":"wpn_m_sniper_plasma_charged","ModuleName_Localised":"Manticore Executioner",
            "Class":5,"WeaponMods":["weapon_clipsize","weapon_stability","weapon_suppression_unpressurised"]},
           {"SlotName":"SecondaryWeapon","SuitModuleID":1845784702022915,
            "ModuleName":"wpn_s_pistol_plasma_charged","ModuleName_Localised":"Manticore Tormentor",
            "Class":5,"WeaponMods":["weapon_backpackreloading","weapon_clipsize","weapon_stability"]}]}
        """;

    private static OnFootLoadout Worn() => OnFootLoadout.Unknown.Apply(Event(Sneaky));

    [Fact]
    public void ReadsTheSuitItsGradeAndItsModifications()
    {
        var loadout = Worn();

        Assert.True(loadout.IsKnown);
        Assert.Equal("utilitysuit_class5", loadout.SuitSymbol);
        Assert.Equal("Maverick Suit", loadout.Suit?.Name);
        Assert.Equal(5, loadout.Grade);
        Assert.Equal("Sneaky-Snipy", loadout.LoadoutName);
        Assert.Equal(2, loadout.SuitModifications.Count);
        Assert.Equal(2, loadout.FreeSlots);
    }

    /// <summary>
    /// The trap that would have shipped. Frontier's localisation names <b>Class1</b> for a class 5
    /// suit, so anything speaking <c>SuitName_Localised</c> is wrong about the grade more than a
    /// third of the time — and wrong in the direction that reads as a working feature.
    /// </summary>
    [Fact]
    public void NeverSpeaksTheLocalisedNameBecauseItNamesTheWrongGrade()
    {
        var loadout = Worn();

        Assert.Contains("Class1", Event(Sneaky).String("SuitName_Localised"));

        Assert.DoesNotContain("Class1", loadout.Speak());
        Assert.Equal("Maverick Suit, grade 5", loadout.Speak());
    }

    [Fact]
    public void ReadsEachWeaponWithItsOwnGradeAndModifications()
    {
        var loadout = Worn();

        var sniper = loadout.InSlot("PrimaryWeapon1");

        Assert.NotNull(sniper);
        Assert.Equal("Manticore Executioner", sniper.Entry?.Name);
        Assert.Equal(5, sniper.Grade);
        Assert.Equal(3, sniper.Modifications.Count);
        Assert.Equal(1, sniper.FreeSlots);
    }

    /// <summary>
    /// Five of the thirteen observed symbols join their recipe name on a relaxed match and eight
    /// do not. A modification outside the shipped map is said as the symbol rather than dropped,
    /// which is the same contract a ship blueprint has.
    /// </summary>
    [Fact]
    public void NamesTheModificationsItCanAndSaysTheSymbolForTheRestRatherThanNothing()
    {
        var loadout = Worn();

        Assert.All(loadout.SuitModifications, modification => Assert.True(modification.IsNamed));

        var unknown = new FittedModification("suit_somethingfrontieraddedlater");

        Assert.False(unknown.IsNamed);
        Assert.Null(unknown.Name);
        Assert.Equal("suit somethingfrontieraddedlater", unknown.Speak());
    }

    /// <summary>
    /// <c>UpgradeSuit</c> carries the suit's <b>old</b> symbol beside its <b>new</b> class, so
    /// reading the grade off the symbol on that one event is off by one.
    /// </summary>
    [Fact]
    public void AnUpgradeTakesTheGradeFromClassAndNotFromTheSymbolItCarries()
    {
        var loadout = OnFootLoadout.Unknown
            .Apply(Event(
                """
                {"timestamp":"2025-11-06T15:47:34Z","event":"SuitLoadout","SuitID":1845879835891144,
                 "SuitName":"utilitysuit_class3","SuitMods":[],"LoadoutID":4293000001,
                 "LoadoutName":"Sneaky-Snipy","Modules":[]}
                """))
            .Apply(Event(
                """
                {"timestamp":"2025-11-06T15:47:34Z","event":"UpgradeSuit","Name":"utilitysuit_class3",
                 "Name_Localised":"$UtilitySuit_Class1_Name;","SuitID":1845879835891144,"Class":4,
                 "Cost":4500000,"Resources":[]}
                """));

        Assert.Equal("utilitysuit_class4", loadout.SuitSymbol);
        Assert.Equal(4, loadout.Grade);
    }

    /// <summary>
    /// The free suit has no class suffix at all, cannot be upgraded and cannot be modified. It is
    /// in the table so that reading a grade off a symbol has somewhere to stop.
    /// </summary>
    [Fact]
    public void TheFlightSuitHasNoGradeAndNoSlots()
    {
        var loadout = OnFootLoadout.Unknown.Apply(Event(
            """
            {"timestamp":"2025-08-30T00:29:50Z","event":"SuitLoadout","SuitID":1841811120917527,
             "SuitName":"flightsuit","SuitName_Localised":"Flight Suit","SuitMods":[],
             "LoadoutID":4293000000,"LoadoutName":"Default loadout","Modules":[]}
            """));

        Assert.Equal("Flight Suit", loadout.Suit?.Name);
        Assert.Null(loadout.Grade);
        Assert.False(loadout.CanBeModified);
    }

    /// <summary>
    /// Elite spells the same suit two ways — <c>SuitLoadout</c> lower case, <c>BuySuit</c> mixed —
    /// so the table lookup folds. Without that, a Commander's worn suit resolves and the one they
    /// just bought does not.
    /// </summary>
    [Fact]
    public void TheTableLookupFoldsCaseBecauseEliteSpellsTheSameSuitTwoWays()
    {
        Assert.Equal(
            OnFootCatalogue.Find("tacticalsuit_class1")?.Name,
            OnFootCatalogue.Find("TacticalSuit_Class1")?.Name);

        Assert.Equal("Dominator Suit", OnFootCatalogue.Find("TacticalSuit_Class1")?.Name);
    }

    /// <summary>
    /// A snapshot, not a merge. A switch to a suit with one weapon must not leave the other one
    /// standing in a slot the Commander is no longer carrying.
    /// </summary>
    [Fact]
    public void SwitchingLoadoutsReplacesTheWeaponsRatherThanMergingThem()
    {
        var loadout = Worn().Apply(Event(
            """
            {"timestamp":"2025-11-22T00:00:00Z","event":"SwitchSuitLoadout","SuitID":1845810927028016,
             "SuitName":"explorationsuit_class1","SuitName_Localised":"Artemis Suit","SuitMods":[],
             "LoadoutID":4293000002,"LoadoutName":"Exobio",
             "Modules":[{"SlotName":"SecondaryWeapon","SuitModuleID":1833540573318740,
                         "ModuleName":"wpn_s_pistol_kinetic_sauto","ModuleName_Localised":"Karma P-15",
                         "Class":1,"WeaponMods":[]}]}
            """));

        Assert.Equal("Artemis Suit", loadout.Suit?.Name);
        Assert.Single(loadout.Weapons);
        Assert.Null(loadout.InSlot("PrimaryWeapon1"));
    }

    /// <summary>Grade 1 has zero slots, which is an ordering problem rather than a shortage.</summary>
    [Fact]
    public void AGradeOneSuitCarriesNoSlotsAtAll()
    {
        Assert.Equal(0, OnFootRules.SlotsAt(1));
        Assert.Equal(4, OnFootRules.SlotsAt(5));
        Assert.Equal(0, OnFootRules.SlotsAt(0));
    }
}
