using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>One journal event as a line a person can read.</summary>
public class TheJournalYouCanReadTests
{
    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var entry));

        return entry!;
    }

    private static string Said(string line) => JournalSentence.For(Parse(line));

    [Theory]

    // Flying.
    [InlineData(
        """{ "timestamp":"2026-08-26T02:12:59Z", "event":"FSDJump", "StarSystem":"LDS 2314", "JumpDist":29.44 }""",
        "Jumped to LDS 2314 — 29.44 ly")]
    [InlineData(
        """{ "timestamp":"2026-08-26T00:29:17Z", "event":"Docked", "StationName":"Prospector's Rest", "StarSystem":"Kuk" }""",
        "Docked at Prospector's Rest, Kuk")]
    [InlineData(
        """{ "timestamp":"2026-08-26T00:28:16Z", "event":"SupercruiseExit", "StarSystem":"Kuk", "Body":"Kuk B 3" }""",
        "Dropped out of supercruise at Kuk B 3")]
    [InlineData(
        """{ "timestamp":"2026-08-21T15:13:44Z", "event":"FuelScoop", "Scooped":4.709673, "Total":32.0 }""",
        "Scooped 4.7 tonnes of fuel")]

    // The localised name wins. "fedcorecomposites" is the symbol; nobody reads that.
    [InlineData(
        """{ "timestamp":"2026-08-26T02:17:22Z", "event":"MaterialCollected", "Category":"Manufactured", "Name":"fedcorecomposites", "Name_Localised":"Core Dynamics Composites", "Count":3 }""",
        "Collected 3 × Core Dynamics Composites")]

    // Danger reads as danger.
    [InlineData(
        """{ "timestamp":"2026-07-03T01:40:29Z", "event":"HullDamage", "Health":0.535646, "PlayerPilot":true }""",
        "Hull damage — 54% remaining")]
    [InlineData(
        """{ "timestamp":"2026-02-03T13:23:28Z", "event":"Died", "KillerName":"Martin Caspersson" }""",
        "Destroyed by Martin Caspersson")]
    [InlineData(
        """{ "timestamp":"2026-01-03T01:00:07Z", "event":"SquadronApplicationApproved", "SquadronID":57404, "SquadronName":"GREYBEARD DELTA" }""",
        "Application to GREYBEARD DELTA approved")]
    public void ARealLineReadsAsASentence(string line, string expected)
    {
        Assert.Equal(expected, Said(line));
    }

    /// <summary>The first defect reading real output found.</summary>
    [Fact]
    public void ABlueprintNameHasNoDoubleSpaceInIt()
    {
        var said = Said(
            """{ "timestamp":"2026-08-26T00:43:37Z", "event":"EngineerCraft", "Engineer":"Selene Jean", "BlueprintName":"Armour_HeavyDuty", "Level":1 }""");

        Assert.Equal("Selene Jean applied Armour Heavy Duty, grade 1", said);
        Assert.DoesNotContain("  ", said, StringComparison.Ordinal);
    }

    /// <summary>The second.</summary>
    [Fact]
    public void ACrimeNameLosesItsSymbolTail()
    {
        var said = Said(
            """{ "timestamp":"2026-08-21T00:39:10Z", "event":"CommitCrime", "CrimeType":"collidedAtSpeedInNoFireZone_hullDamage" }""");

        Assert.Equal("Committed a crime: collided at speed in no fire zone", said);
        Assert.DoesNotContain("_", said, StringComparison.Ordinal);
    }

    /// <summary>The third.</summary>
    [Fact]
    public void ALoadoutNeverShowsAShipSymbol()
    {
        Assert.Equal(
            "Loadout reported for Tulimiekka (Kestrel Mk II)",
            Said("""{ "timestamp":"2026-08-26T00:00:00Z", "event":"Loadout", "Ship":"smallcombat01_nx", "ShipName":"Tulimiekka" }"""));

        Assert.Equal(
            "Loadout reported for the Kestrel Mk II",
            Said("""{ "timestamp":"2026-08-26T00:00:00Z", "event":"Loadout", "Ship":"smallcombat01_nx" }"""));
    }

    /// <summary><c>EliteSpecifications.HullSaid</c> is the ladder — the measured row, then the name read off the hull's own armour, then a spoken match — and it exists because Frontier ships hulls before the community id list catches up.</summary>
    [Theory]
    [InlineData("smallcombat01_nx", "Kestrel")]
    [InlineData("explorer_nx", "Caspian Explorer")]
    public void AHullWithNoMeasuredRowIsStillNamed(string symbol, string expected)
    {
        var said = Said(
            $$"""{ "timestamp":"2026-08-26T00:00:00Z", "event":"ShipyardSwap", "ShipType":"{{symbol}}" }""");

        Assert.Contains(expected, said, StringComparison.Ordinal);
        Assert.DoesNotContain(symbol, said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A symbol that arrives wrapped in Frontier's <c>$name;</c> form is unwrapped rather than shown.
    /// </summary>
    [Fact]
    public void AWrappedSymbolIsUnwrapped()
    {
        var said = Said(
            """{ "timestamp":"2026-08-26T00:55:30Z", "event":"ModuleBuy", "BuyItem":"$int_hullreinforcement_size2_class2_name;", "BuyItem_Localised":"Hull Reinforcement", "BuyPrice":28800 }""");

        Assert.Equal("Bought a Hull Reinforcement for 28,800 Cr", said);
        Assert.DoesNotContain("$", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A nine-figure sale speaks a banded figure rather than eleven digits, and drops "Cr" once it's
 /// banded.
    /// </summary>
    [Fact]
    public void ANineFigureSaleSpeaksABandedFigure()
    {
        var said = Said(
            """{ "timestamp":"2026-08-26T00:55:30Z", "event":"MarketSell", "MarketID":1, "Type":"platinum", "Type_Localised":"Platinum", "Count":1200, "SellPrice":302136, "TotalSale":362563200, "AvgPricePaid":0 }""");

        Assert.Equal("Sold 1,200 × Platinum for 362.6 million", said);
        Assert.DoesNotContain("Cr", said, StringComparison.Ordinal);
    }

    /// <summary>The rule that makes the whole page safe.</summary>
    [Fact]
    public void AnEventWithNoSentenceReadsAsItsOwnName()
    {
        Assert.Equal(
            "Fake Event Nobody Wrote",
            Said("""{ "timestamp":"2026-08-26T00:00:00Z", "event":"FakeEventNobodyWrote" }"""));

        // And an acronym stays an acronym rather than becoming "F S S Signal Discovered".
        Assert.Equal(
            "FSS Signal Discovered",
            Said("""{ "timestamp":"2026-08-26T00:00:00Z", "event":"FSSSignalDiscovered" }"""));
    }

    /// <summary>
    /// A field that is absent never produces a blank or a broken sentence — every kind has a fallback,
    /// because a journal from a future game version can omit anything.
    /// </summary>
    [Theory]
    [InlineData("FSDJump")]
    [InlineData("Docked")]
    [InlineData("Scan")]
    [InlineData("MaterialCollected")]
    [InlineData("EngineerCraft")]
    [InlineData("MarketBuy")]
    [InlineData("Bounty")]
    [InlineData("MissionAccepted")]
    [InlineData("Died")]
    [InlineData("CarrierJump")]
    public void AKindWithNoFieldsAtAllStillReads(string kind)
    {
        var said = Said($$"""{ "timestamp":"2026-08-26T00:00:00Z", "event":"{{kind}}" }""");

        Assert.False(string.IsNullOrWhiteSpace(said));
        Assert.DoesNotContain("  ", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A module bought with Merc Coins carries <c>BuyPrice: 0</c> and the real cost in
 /// <c>BuyMercCoinsPrice</c> instead.
    /// </summary>
    [Fact]
    public void AMercPricedModuleSaysWhatItCost()
    {
        Assert.Equal(
            "Bought a Cargo Rack for 40 Merc Coins",
            Said("""{ "timestamp":"2026-09-02T00:00:00Z", "event":"ModuleBuy", "BuyItem":"$int_cargorack_size1_class1_name;", "BuyItem_Localised":"Cargo Rack", "BuyPrice":0, "BuyMercCoinsPrice":40 }"""));

        Assert.Equal(
            "Bought and stored a Cargo Rack for 40 Merc Coins",
            Said("""{ "timestamp":"2026-09-02T00:00:00Z", "event":"ModuleBuyAndStore", "BuyItem":"$int_cargorack_size1_class1_name;", "BuyItem_Localised":"Cargo Rack", "BuyPrice":0, "BuyMercCoinsPrice":40 }"""));
    }

    /// <summary>
    /// A credit-priced module names its price on both buying arms, and the merc fallback only fires
    /// when <c>BuyPrice</c> is zero.
    /// </summary>
    [Fact]
    public void ACreditPricedModuleIsUnaffectedByTheMercFallback()
    {
        Assert.Equal(
            "Bought a Cargo Rack for 5,760 Cr",
            Said("""{ "timestamp":"2026-08-26T00:00:00Z", "event":"ModuleBuy", "BuyItem":"$int_cargorack_size1_class1_name;", "BuyItem_Localised":"Cargo Rack", "BuyPrice":5760, "BuyMercCoinsPrice":0 }"""));

        // Storing it away costs the same and reads the same.
        Assert.Equal(
            "Bought and stored a Cargo Rack for 5,760 Cr",
            Said("""{ "timestamp":"2026-08-26T00:00:00Z", "event":"ModuleBuyAndStore", "BuyItem":"$int_cargorack_size1_class1_name;", "BuyItem_Localised":"Cargo Rack", "BuyPrice":5760, "BuyMercCoinsPrice":0 }"""));
    }

    /// <summary>
    /// The Rhino update (4.4.1.0, live 2026-09-02) is the first build the Commander has run that writes
 /// <c>GameModeChange</c>.
    /// </summary>
    [Theory]
    [InlineData("MainGame", "Back in the main game")]
    [InlineData("Operation", "Entered an operation")]
    public void AGameModeChangeSaysWhichMode(string mode, string expected)
    {
        Assert.Equal(
            expected,
            Said($$"""{ "timestamp":"2026-09-02T21:36:54Z", "event":"GameModeChange", "GameMode":"{{mode}}" }"""));
    }

    /// <summary>A third mode Frontier has not shipped yet still reads as words rather than nothing.</summary>
    [Fact]
    public void AnUnknownGameModeStillReadsAsWords()
    {
        var said = Said(
            """{ "timestamp":"2026-09-02T21:36:54Z", "event":"GameModeChange", "GameMode":"SomeFutureMode" }""");

        Assert.Contains("Some Future Mode", said, StringComparison.Ordinal);
    }

    [Fact]
    public void ABlankGameModeReadsAsNoModeAtAll()
    {
        Assert.Equal(
            "Game mode changed",
            Said("""{ "timestamp":"2026-09-02T21:36:54Z", "event":"GameModeChange", "GameMode":"" }"""));
    }

    /// <summary>The noise floor is measured rather than chosen.</summary>
    [Fact]
    public void TheNoiseFloorNamesTheTwoThatDominateTheCorpus()
    {
        Assert.Contains("FSSSignalDiscovered", JournalSentence.Noise);
        Assert.Contains("ShipLocker", JournalSentence.Noise);

        // And never anything a Commander did.
        Assert.DoesNotContain("FSDJump", JournalSentence.Noise);
        Assert.DoesNotContain("Docked", JournalSentence.Noise);
        Assert.DoesNotContain("Died", JournalSentence.Noise);
    }

    /// <summary>Other players' text gets no sentence at all, deliberately.</summary>
    [Theory]
    [InlineData("ReceiveText")]
    [InlineData("SendText")]
    public void AMessageFromAnotherPlayerIsNeverFormattedIntoASentence(string kind)
    {
        var said = Said(
            $$"""{ "timestamp":"2026-08-26T00:18:40Z", "event":"{{kind}}", "From":"CMDR Hostile", "Message":"**not bold**", "Channel":"local" }""");

        // The bare kind, which is what "no sentence" looks like — the message itself never reaches this line
        // at all.
        Assert.DoesNotContain("not bold", said, StringComparison.Ordinal);
        Assert.DoesNotContain("CMDR Hostile", said, StringComparison.Ordinal);
    }

 /// <summary>Comms read as the sender and what they said.</summary>
    [Theory]

    // Elite's own channel notice: no sender at all, and the words are only in the localised form.
    [InlineData(
        """{ "timestamp":"2026-08-02T12:55:41Z", "event":"ReceiveText", "From":"", "Message":"$COMMS_entered:#name=Wyrd;", "Message_Localised":"Entered Channel: Wyrd", "Channel":"npc" }""",
        "Entered Channel: Wyrd")]

    // An NPC, whose name arrives wrapped in a localisation decorator.
    [InlineData(
        """{ "timestamp":"2026-08-02T12:56:44Z", "event":"ReceiveText", "From":"$npc_name_decorate:#name=Tim O'Shea;", "From_Localised":"Tim O'Shea", "Message":"$MinerCriticalDamage01;", "Message_Localised":"No, no, nooooooo!", "Channel":"npc" }""",
        "Tim O'Shea: No, no, nooooooo!")]

    // A station: named plainly, and the message localised.
    [InlineData(
        """{ "timestamp":"2026-08-02T13:02:11Z", "event":"ReceiveText", "From":"$STATION_Evans Port;", "From_Localised":"Evans Port", "Message":"$STATION_docking_granted;", "Message_Localised":"Docking request granted.", "Channel":"npc" }""",
        "Evans Port: Docking request granted.")]

    // Another Commander, whose words are untrusted and stay out of the line entirely — even though the sender
    // is known and the message is harmless.
    [InlineData(
        """{ "timestamp":"2026-08-02T13:10:04Z", "event":"ReceiveText", "From":"Fixture Vex", "Message":"o7", "Channel":"starsystem" }""",
        "Message received")]

    // A token Elite never localised.
    [InlineData(
        """{ "timestamp":"2026-08-02T13:11:00Z", "event":"ReceiveText", "From":"", "Message":"$Pirate_Attack;", "Channel":"npc" }""",
        "Message received")]

    // And the Commander's own typing, which is free text like anybody else's.
    [InlineData(
        """{ "timestamp":"2026-08-02T13:12:40Z", "event":"SendText", "To":"Fixture Vex", "Message":"o7 fly safe" }""",
        "Message sent")]
    public void CommsSayWhoSpokeAndWhatTheySaid(string line, string expected) =>
        Assert.Equal(expected, Said(line));
}
