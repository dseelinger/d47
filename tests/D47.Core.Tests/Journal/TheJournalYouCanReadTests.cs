using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// One journal event as a line a person can read
/// (<a href="https://github.com/dseelinger/d47/issues/51">#51</a>).
/// <para>
/// <b>Every line asserted here was produced from a real journal line</b>, not from a hand-built
/// one that agrees with what the formatter expects. Four defects were found that way and none of
/// them would have been found any other way — the fields are all plausible and all wrong until you
/// look.
/// </para>
/// </summary>
public class TheJournalYouCanReadTests
{
    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var entry));

        return entry!;
    }

    private static string Said(string line) => JournalSentence.For(Parse(line));

    [Theory]

    // Flying. Every one of these is a line lifted out of the corpus.
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
    public void ARealLineReadsAsASentence(string line, string expected)
    {
        Assert.Equal(expected, Said(line));
    }

    /// <summary>
    /// <b>The first defect reading real output found.</b> <c>BlueprintName</c> is
    /// <c>Armour_HeavyDuty</c>: replacing the underscore with a space and then splitting the camel
    /// hump put two spaces in the middle. It is fixed in <c>Spaced</c> rather than at the call
    /// site, so every future caller inherits it.
    /// </summary>
    [Fact]
    public void ABlueprintNameHasNoDoubleSpaceInIt()
    {
        var said = Said(
            """{ "timestamp":"2026-08-26T00:43:37Z", "event":"EngineerCraft", "Engineer":"Selene Jean", "BlueprintName":"Armour_HeavyDuty", "Level":1 }""");

        Assert.Equal("Selene Jean applied Armour Heavy Duty, grade 1", said);
        Assert.DoesNotContain("  ", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The second.</b> Elite's localised crime name carries a symbol tail, so the line read
    /// "...in no fire zone_hulldamage". An underscore in a sentence is a leaked implementation
    /// detail.
    /// </summary>
    [Fact]
    public void ACrimeNameLosesItsSymbolTail()
    {
        var said = Said(
            """{ "timestamp":"2026-08-21T00:39:10Z", "event":"CommitCrime", "CrimeType":"collidedAtSpeedInNoFireZone_hullDamage" }""");

        Assert.Equal("Committed a crime: collided at speed in no fire zone", said);
        Assert.DoesNotContain("_", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The third.</b> <c>Loadout</c> carries <c>Ship</c> as a bare symbol with no localised
    /// twin, so it read "Loadout reported for the smallcombat01_nx". Saying nothing about which
    /// ship beats saying that; the ship's own name is used where the Commander gave it one.
    /// </summary>
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

    /// <summary>
    /// <b>The Commander asked why a Kestrel was reading as <c>smallcombat01_nx</c>, and the answer
    /// was that this formatter was not asking.</b> d47 solved that on 2026-08-23:
    /// <c>EliteSpecifications.HullSaid</c> is the ladder — the measured row, then the name read off
    /// the hull's own armour, then a spoken match — and it exists precisely because Frontier ships
    /// hulls before the community id list catches up.
    /// <para>
    /// Two hulls, both of which have figures nothing can key, and both of which d47 can name
    /// anyway. Asserted here so a new caller cannot go back to reading the raw symbol.
    /// </para>
    /// </summary>
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
    /// A symbol that arrives wrapped in Frontier's <c>$name;</c> form is unwrapped rather than
    /// shown. A dollar sign in a summary line is the same leak as an underscore.
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
    /// <b>The rule that makes the whole page safe.</b> An event nobody wrote a sentence for reads
    /// as its own name, spaced into words — and loses nothing, because the detail pane beside it
    /// is exactly as complete as any other's. A summary that is missing is a summary; a summary
    /// that is wrong is a data-accuracy defect.
    /// </summary>
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
    /// A field that is absent never produces a blank or a broken sentence — every kind has a
    /// fallback, because a journal from a future game version can omit anything.
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
    /// The noise floor is measured rather than chosen. Across 931 journals
    /// <c>FSSSignalDiscovered</c> and <c>ShipLocker</c> alone are 48% of the corpus by volume, and
    /// a Commander wants to read neither.
    /// </summary>
    [Fact]
    public void TheNoiseFloorNamesTheTwoThatDominateTheCorpus()
    {
        Assert.Contains("FSSSignalDiscovered", JournalSentence.Noise);
        Assert.Contains("ShipLocker", JournalSentence.Noise);

        // And never anything a Commander did. Hiding a jump would make the page a liar.
        Assert.DoesNotContain("FSDJump", JournalSentence.Noise);
        Assert.DoesNotContain("Docked", JournalSentence.Noise);
        Assert.DoesNotContain("Died", JournalSentence.Noise);
    }

    /// <summary>
    /// <b>Other players' text gets no sentence at all, deliberately.</b> The message is the
    /// content, and the page renders it unformatted and muted — a player who types <c>**</c> must
    /// see <c>**</c> rather than bold, and the summary line must never let a message impersonate
    /// d47's own line format. This is the untrusted-input invariant arriving at a new surface.
    /// </summary>
    [Theory]
    [InlineData("ReceiveText")]
    [InlineData("SendText")]
    public void AMessageFromAnotherPlayerIsNeverFormattedIntoASentence(string kind)
    {
        var said = Said(
            $$"""{ "timestamp":"2026-08-26T00:18:40Z", "event":"{{kind}}", "From":"CMDR Hostile", "Message":"**not bold**", "Channel":"local" }""");

        // The bare kind, which is what "no sentence" looks like — the message itself never
        // reaches this line at all.
        Assert.DoesNotContain("not bold", said, StringComparison.Ordinal);
        Assert.DoesNotContain("CMDR Hostile", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// Comms read as the sender and what they said
    /// (<a href="https://github.com/dseelinger/d47/issues/260">#260</a>).
    /// <para>
    /// <b>This reading is the only place comms are written down.</b> They were on the Technical
    /// reading until #231 withdrew it, and the transcript stopped carrying them in #260 — the
    /// conversation is drawn as bubbles, so a station's line arrived in d47's own bubble and
    /// merged into whatever it had just said. The comment here used to claim these two kinds
    /// deliberately had no sentence because the page rendered the message itself; the page never
    /// did, so a message drew as the bare words "Receive Text".
    /// </para>
    /// <para>
    /// <b>Frontier's own text only</b>, which is why the test above still holds unchanged: a
    /// message another player typed has no <c>Message_Localised</c>, so it never reaches this
    /// line. What #260 changed is that a station and an NPC stopped being caught by that net.
    /// </para>
    /// <para>
    /// <b>The empty sender is the case that matters</b>, and it was wrong in the first draft
    /// against real journals: Elite writes <c>From</c> as an empty string for its own channel
    /// notices, which produced a line beginning with a bare colon. Those notices are most of the
    /// comms events in a quiet session.
    /// </para>
    /// </summary>
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

    // Another Commander, whose words are untrusted and stay out of the line entirely — even
    // though the sender is known and the message is harmless. The rule is about where text came
    // from, not about whether this particular text looks dangerous.
    [InlineData(
        """{ "timestamp":"2026-08-02T13:10:04Z", "event":"ReceiveText", "From":"Fixture Vex", "Message":"o7", "Channel":"starsystem" }""",
        "Message received")]

    // A token Elite never localised. Saying it is worse than saying nothing, which is the rule
    // IncomingMessages already applies on the way to the speaker.
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
