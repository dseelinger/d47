using D47.Core.Diagnostics.Donation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>Donating the incident behind a bug report.</summary>
public class AnIncidentExcerptTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static JournalEntry Entry(string compact, int minute = 0) =>
        new(Noon.AddMinutes(minute), Kind(compact), "said", compact, compact);

    /// <summary>
    /// The event name off a line, or nothing where there is no line to read one from — the unreadable
    /// case is one of the things under test, so the helper that builds it must survive being handed
    /// rubbish.
    /// </summary>
    private static string Kind(string compact)
    {
        var found = compact.IndexOf("\"event\":\"", StringComparison.Ordinal);

        if (found < 0)
        {
            return "Unreadable";
        }

        var at = found + 9;
        var end = compact.IndexOf('"', at);

        return end < 0 ? "Unreadable" : compact[at..end];
    }

    /// <summary>
    /// A log fixture as entries, the way <see cref="IncidentSources.Logs"/> hands them over: parsed
    /// against the day its filename would have carried.
    /// </summary>
    private static IReadOnlyList<LogEntry> Entries(string log, int day = 28) =>
        LogScrub.Parse(log, new DateOnly(2026, 8, day), Utc);

    /// <summary>The scrubbed line alone, where the test is about the line rather than the count.</summary>
    private static string? Scrubbed(string json, Pseudonyms names) => JournalScrub.Line(json, names).Json;

    private static ExcerptRequest Window(bool mySpeech = false) =>
        new(Noon.AddMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), mySpeech);

    // ---- The journal half: a field list, and nothing beyond it ----

    /// <summary>
    /// The identity events are the front of every journal file, and they are the first thing the list
    /// exists for.
    /// </summary>
    [Fact]
    public void TheCommandersNameAndIdAreReplaced()
    {
        var names = new Pseudonyms();

        var scrubbed = Scrubbed(
            """{"timestamp":"2026-08-28T12:00:00Z","event":"Commander","FID":"F735466","Name":"JOHN DEPARAGON"}""",
            names);

        Assert.NotNull(scrubbed);
        Assert.DoesNotContain("JOHN DEPARAGON", scrubbed);
        Assert.DoesNotContain("F735466", scrubbed);
        Assert.Contains("CMDR ALPHA", scrubbed);

        // Still a Commander event with both fields on it.
        Assert.Contains("\"event\":\"Commander\"", scrubbed);
        Assert.Contains("\"FID\"", scrubbed);
    }

    /// <summary>The same person, twice, under the two different field names Elite uses.</summary>
    [Fact]
    public void OnePersonKeepsOneStandInAcrossEvents()
    {
        var names = new Pseudonyms();

        var commander = Scrubbed(
            """{"event":"Commander","FID":"F735466","Name":"JOHN DEPARAGON"}""", names);

        var load = Scrubbed(
            """{"event":"LoadGame","FID":"F735466","Commander":"JOHN DEPARAGON","Ship":"Python"}""", names);

        Assert.Contains("CMDR ALPHA", commander);
        Assert.Contains("CMDR ALPHA", load);

        // Two values seen — a name and an ID — and not four.
        Assert.Equal(2, names.Count);
    }

    /// <summary>
    /// The rule the whole list is drawn around: everything else in a journal is a fact about the game,
    /// and a scrubber that touched it would turn a replay case into a redaction.
    /// </summary>
    [Fact]
    public void AJumpTravelsExactlyAsEliteWroteIt()
    {
        const string jump =
            """{"timestamp":"2026-08-28T12:00:00Z","event":"FSDJump","StarSystem":"Eurybia","SystemEconomy":"$economy_Industrial;","JumpDist":18.42}""";

        Assert.Equal(jump, Scrubbed(jump, new Pseudonyms()));
    }

    /// <summary>A donor cannot consent on another player's behalf.</summary>
    [Fact]
    public void AMessageKeepsItsShapeAndLosesItsWords()
    {
        var scrubbed = Scrubbed(
            """{"event":"ReceiveText","From":"Example Delta","Message":"Oh no you don't!","Message_Localised":"Oh no you don't!","Channel":"local"}""",
            new Pseudonyms());

        Assert.NotNull(scrubbed);
        Assert.DoesNotContain("Oh no you don't", scrubbed);
        Assert.DoesNotContain("Example Delta", scrubbed);

        Assert.Contains("\"Channel\":\"local\"", scrubbed);
        Assert.Contains(JournalScrub.Withheld, scrubbed);
        Assert.Contains("CMDR ALPHA", scrubbed);
    }

    /// <summary>And the report has to be able to say so.</summary>
    [Fact]
    public void ADroppedBodyIsCountedWhicheverHalfItWasIn()
    {
        // One message, not two, though ReceiveText carries the sentence twice.
        Assert.Equal(
            1,
            JournalScrub.Line(
                """{"event":"ReceiveText","From":"","Message":"hello","Message_Localised":"hello","Channel":"npc"}""",
                new Pseudonyms()).BodiesDropped);

        var report = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [Entry("""{"event":"ReceiveText","From":"","Message":"hello","Channel":"npc"}""", 3)],
                [],
                Window()),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("1 in-game message withheld", report);
        Assert.DoesNotContain("no in-game message arrived", report);
    }

    /// <summary>An array of bare strings, which is the shape a wing arrives in.</summary>
    [Fact]
    public void EveryNameInAWingIsReplaced()
    {
        var scrubbed = Scrubbed(
            """{"event":"WingJoin","Others":["Ilse Bruhn","Example Delta"]}""",
            new Pseudonyms());

        Assert.NotNull(scrubbed);
        Assert.DoesNotContain("Ilse Bruhn", scrubbed);
        Assert.DoesNotContain("Example Delta", scrubbed);
        Assert.Contains("CMDR ALPHA", scrubbed);
        Assert.Contains("CMDR BRAVO", scrubbed);
    }

    /// <summary>The ship's name is the Commander's, and <c>Loadout</c> carries it as well as the event that sets it — <c>Loadout</c> being the event every excerpt contains.</summary>
    [Fact]
    public void AShipsNameIsTheSameStandInWhereverItAppears()
    {
        var names = new Pseudonyms();

        var named = Scrubbed(
            """{"event":"SetUserShipName","Ship":"python","UserShipName":"Vera Rubin","UserShipId":"JD-01"}""",
            names);

        var loadout = Scrubbed(
            """{"event":"Loadout","Ship":"python","ShipName":"Vera Rubin","ShipIdent":"JD-01","HullValue":56978179}""",
            names);

        Assert.DoesNotContain("Vera Rubin", named);
        Assert.DoesNotContain("Vera Rubin", loadout);
        Assert.Contains("SHIP ALPHA", named);
        Assert.Contains("SHIP ALPHA", loadout);

        // The hull figure is a fact about a Python, not about anybody.
        Assert.Contains("56978179", loadout);
    }

    /// <summary>
    /// An interdiction is overwhelmingly a Frontier pirate, and Elite says which with <c>IsPlayer</c> —
    /// so the rule fires on the person and leaves the NPC alone.
    /// </summary>
    [Fact]
    public void AnInterdictionIsScrubbedOnlyWhenAPersonDidIt()
    {
        var pirate = Scrubbed(
            """{"event":"Interdicted","Submitted":true,"Interdictor":"Richy Reay","IsPlayer":false,"Faction":"Pai Huldr Blue Brothers"}""",
            new Pseudonyms());

        Assert.Contains("Richy Reay", pirate);
        Assert.Contains("Pai Huldr Blue Brothers", pirate);

        var person = Scrubbed(
            """{"event":"Interdicted","Submitted":false,"Interdictor":"Example Delta","IsPlayer":true}""",
            new Pseudonyms());

        Assert.DoesNotContain("Example Delta", person);
        Assert.Contains("CMDR ALPHA", person);
    }

    /// <summary>A condition that is absent is a condition that is not met.</summary>
    [Fact]
    public void AMissingFlagIsNotPermission()
    {
        var scrubbed = Scrubbed(
            """{"event":"EscapeInterdiction","Interdictor":"Dedy Sofyan"}""",
            new Pseudonyms());

        Assert.Contains("Dedy Sofyan", scrubbed);
    }

    /// <summary>
    /// <c>PVPKill</c> needs no condition for the opposite reason: its victim is a player by definition,
    /// and the event exists only because one was.
    /// </summary>
    [Fact]
    public void AKillInOpenNeedsNoFlagToBeAPerson()
    {
        var scrubbed = Scrubbed(
            """{"event":"PVPKill","Victim":"Ilse Bruhn","CombatRank":5}""",
            new Pseudonyms());

        Assert.DoesNotContain("Ilse Bruhn", scrubbed);
        Assert.Contains("CMDR ALPHA", scrubbed);
        Assert.Contains("\"CombatRank\":5", scrubbed);
    }

    /// <summary>And the one Elite does not flag.</summary>
    [Fact]
    public void ADeathScrubsBecauseItCannotTell()
    {
        var names = new Pseudonyms();

        var single = Scrubbed(
            """{"event":"Died","KillerName":"Dominic Storin","KillerShip":"empire_trader","KillerRank":"Expert"}""",
            names);

        Assert.DoesNotContain("Dominic Storin", single);
        Assert.Contains("empire_trader", single);
        Assert.Contains("Expert", single);

        var wing = Scrubbed(
            """{"event":"Died","Killers":[{"Name":"Cmdr HRC1","Ship":"Vulture","Rank":"Competent"}]}""",
            names);

        Assert.DoesNotContain("Cmdr HRC1", wing);
        Assert.Contains("Vulture", wing);
    }

    /// <summary>A Frontier symbol is not a person.</summary>
    [Fact]
    public void AFrontierSymbolIsLeftWhereItIs()
    {
        var scrubbed = Scrubbed(
            """{"event":"Died","KillerName":"$ShipName_Military_Federation;","KillerName_Localised":"Federal Navy Ship","KillerShip":"federation_gunship"}""",
            new Pseudonyms());

        Assert.Contains("$ShipName_Military_Federation;", scrubbed);

        // And its translation goes with it. `X` and `X_Localised` are one datum rendered twice, so a killer
        // that is a ship class stays a ship class in both fields — replacing only the readable half produced
        // "KillerName": "$ShipName_Military_Federation;" beside "KillerName_Localised": "CMDR ALPHA", which
        // is how this was found.
        Assert.Contains("Federal Navy Ship", scrubbed);

        // And a message body still goes, symbol or not: the words are not what a replay needs.
        Assert.DoesNotContain(
            "$COMMS_entered:",
            Scrubbed(
                """{"event":"ReceiveText","From":"","Message":"$COMMS_entered:#name=Eurybia;","Channel":"npc"}""",
                new Pseudonyms()));
    }

    /// <summary>
    /// A carrier's name and its callsign, ruled PII by the Commander on 2026-08-29: both can be looked
    /// up on INARA, and the callsign is the key that site indexes carriers by.
    /// </summary>
    [Fact]
    public void ACarriersNameAndCallsignBothGo()
    {
        var names = new Pseudonyms();

        var stats = Scrubbed(
            """{"event":"CarrierStats","CarrierID":3712682240,"Callsign":"B0X-79X","Name":"GDS PREDATOR","DockingAccess":"all"}""",
            names);

        Assert.DoesNotContain("GDS PREDATOR", stats);
        Assert.DoesNotContain("B0X-79X", stats);
        Assert.Contains("CARRIER ALPHA", stats);
        Assert.Contains("ZZ0-001", stats);
        Assert.Contains("\"DockingAccess\":\"all\"", stats);

        // The same callsign a second time, in the field it mostly lives in, and on an event with no carrier
        // in its name at all.
        var docked = Scrubbed(
            """{"event":"Docked","StationName":"B0X-79X","StationType":"FleetCarrier","MarketID":3712682240}""",
            names);

        Assert.Contains("ZZ0-001", docked);
        Assert.Contains("FleetCarrier", docked);
    }

    /// <summary>And an ordinary station keeps its name.</summary>
    [Fact]
    public void AnOrdinaryStationIsNotACarrier()
    {
        var docked = Scrubbed(
            """{"event":"Docked","StationName":"Jameson Memorial","StationType":"Orbis","MarketID":128666762}""",
            new Pseudonyms());

        Assert.Contains("Jameson Memorial", docked);
    }

    /// <summary>
    /// Five of the nineteen events carrying a callsign have no <c>StationType</c> to condition on, and
    /// they are the ones that list a Commander's whole fleet.
    /// </summary>
    [Fact]
    public void TheEventsWithNothingToConditionOnAreCoveredToo()
    {
        var names = new Pseudonyms();

        foreach (var line in new[]
                 {
                     """{"event":"StoredShips","StationName":"BNH-T2F","ShipsHere":[],"ShipsRemote":[]}""",
                     """{"event":"Shipyard","StationName":"BNH-T2F","MarketID":3712682240}""",
                     """{"event":"Outfitting","StationName":"BNH-T2F","MarketID":3712682240}""",
                     """{"event":"StoredModules","StationName":"BNH-T2F","Items":[]}""",
                     """{"event":"FCMaterials","CarrierID":"BNH-T2F","CarrierName":"x"}""",
                 })
        {
            var scrubbed = Scrubbed(line, names);

            Assert.DoesNotContain("BNH-T2F", scrubbed);
            Assert.Contains("ZZ0-001", scrubbed);
        }
    }

    /// <summary>
    /// Somebody else's carrier, seen from across a system, with its name and callsign in one string —
    /// <c>"EXAMPLE HAULAGE Q7Z-1AB"</c> is a real signal off the corpus.
    /// </summary>
    [Fact]
    public void AStrangersCarrierGoesWholeWhenElitesSaysItIsOne()
    {
        var names = new Pseudonyms();

        var carrier = Scrubbed(
            """{"event":"FSSSignalDiscovered","SystemAddress":6405910172338,"SignalName":"EXAMPLE HAULAGE Q7Z-1AB","SignalType":"FleetCarrier","IsStation":true}""",
            names);

        Assert.DoesNotContain("EXAMPLE HAULAGE", carrier);
        Assert.DoesNotContain("Q7Z-1AB", carrier);
        Assert.Contains("CARRIER ALPHA", carrier);

        // A megaship wears the same shape at the front, and is a game fact.
        Assert.Contains(
            "MVU-891 Bellmarsh-class Reformatory",
            Scrubbed(
                """{"event":"FSSSignalDiscovered","SignalName":"MVU-891 Bellmarsh-class Reformatory","SignalType":"Megaship"}""",
                names));

        Assert.Contains(
            "LP 466-235 Gold Boys",
            Scrubbed("""{"event":"FSDJump","SystemFaction":{"Name":"LP 466-235 Gold Boys"}}""", names));

        // And the drop target, which is where this was found: a field called Type, mixing symbols, ordinary
        // stations and carriers with nothing on the event to tell them apart.
        var drop = Scrubbed(
            """{"event":"SupercruiseDestinationDrop","Type":"GDS PREDATOR B0X-79X","Threat":0}""",
            names);

        Assert.DoesNotContain("GDS PREDATOR", drop);
        Assert.DoesNotContain("B0X-79X", drop);

        Assert.Contains(
            "Ray Gateway",
            Scrubbed("""{"event":"SupercruiseDestinationDrop","Type":"Ray Gateway","Threat":0}""", names));

        // And what you were nearest to when you scanned something, which was the last residue a corpus sweep
        // turned up: 11 CodexEntry lines out of 179,378.
        Assert.DoesNotContain(
            "GDS PREDATOR",
            Scrubbed(
                """{"event":"CodexEntry","Name":"$Codex_Ent_G_Type_Name;","NearestDestination":"GDS PREDATOR B0X-79X"}""",
                names));

        // Every other signal is a game fact and keeps its name.
        var beacon = Scrubbed(
            """{"event":"FSSSignalDiscovered","SignalName":"$USS_HighGradeEmissions;","SignalType":"USS"}""",
            names);

        Assert.Contains("$USS_HighGradeEmissions;", beacon);
    }

    /// <summary>
    /// And the same hole the Commander's name had: <c>CarrierStats</c> is common but not guaranteed to
    /// be in a six-minute window, while d47 says the carrier's name and callsign in what it tells you
    /// about a jump.
    /// </summary>
    [Fact]
    public void TheCarrierIsReplacedInTheLogEvenWhenTheWindowNeverSawIt()
    {
        const string spoken =
            "[12:03:00 INF] D47.Core.Audio.SpeechPipeline: D47 said: GDS PREDATOR (B0X-79X) jumps in 4 minutes.";

        var told = IncidentExcerpt.Take(
            [],
            Entries(spoken),
            Window(),
            carrier: new CarrierState { Name = "GDS PREDATOR", CallSign = "B0X-79X" });

        Assert.DoesNotContain(told.Log, line => line.Contains("GDS PREDATOR"));
        Assert.DoesNotContain(told.Log, line => line.Contains("B0X-79X"));
        Assert.Contains(told.Log, line => line.Contains("CARRIER ALPHA (ZZ0-001)"));
    }

    /// <summary>
    /// A squadron is PII in both halves — the Commander's ruling of 2026-08-29: a squadron of one is a
    /// pseudonym for a person, and name and id both resolve on INARA.
    /// </summary>
    [Fact]
    public void ASquadronsNameAndIdBothGo()
    {
        var names = new Pseudonyms();

        var startup = Scrubbed(
            """{"event":"SquadronStartup","SquadronName":"EXAMPLE SQUADRON","SquadronID":10101,"CurrentRank":3}""",
            names);

        Assert.DoesNotContain("EXAMPLE SQUADRON", startup);
        Assert.DoesNotContain("10101", startup);
        Assert.Contains("SQUADRON ALPHA", startup);
        Assert.Contains("\"SquadronID\":900000", startup);
        Assert.Contains("\"CurrentRank\":3", startup);

        // And the four-character tag another Commander's ship wears, which is the same field holding a
        // different shape.
        Assert.Contains(
            "\"SquadronID\":\"SQ01\"",
            Scrubbed("""{"event":"ShipTargeted","SquadronID":"EX01","Ship":"python"}""", names));
    }

    /// <summary>The flag that would have undone the two rules above it.</summary>
    [Fact]
    public void TheFlagSayingWhichFactionIsTheSquadronsGoes()
    {
        var names = new Pseudonyms();
        names.Squadron("EXAMPLE SQUADRON");

        var scrubbed = JournalScrub.Line(
            """{"event":"FSDJump","StarSystem":"Suchifuku","Factions":[{"Name":"Suchifuku Pirates","Influence":0.010},{"Name":"Example Independents","Influence":0.684,"SquadronFaction":true}],"SystemFaction":{"Name":"Example Independents"}}""",
            names);

        Assert.DoesNotContain("SquadronFaction", scrubbed.Json);

        // Not falsified into the other answer.
        Assert.DoesNotContain("false", scrubbed.Json);

        // The factions themselves stay — they are Frontier's, and they belong to the galaxy rather than to
        // anybody.
        Assert.Contains("Example Independents", scrubbed.Json);
        Assert.Contains("Suchifuku Pirates", scrubbed.Json);
        Assert.Contains("0.684", scrubbed.Json);

        Assert.Equal(1, scrubbed.FieldsDropped);
    }

    [Fact]
    public void TheReportSaysTheLinkWasDropped()
    {
        var report = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [Entry("""{"event":"FSDJump","Factions":[{"Name":"X","SquadronFaction":true}]}""", 3)],
                [],
                Window()),
            new ExcerptPaperwork("0.87.0", Noon));

        Assert.Contains("1 squadron link dropped", report);

        // And says nothing about it where there was none to drop.
        Assert.DoesNotContain(
            "squadron link",
            ExcerptReport.Render(
                IncidentExcerpt.Take(
                    [Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}""", 3)],
                    [],
                    Window()),
                new ExcerptPaperwork("0.87.0", Noon)));
    }

    /// <summary>A minor faction stays, on the same ruling.</summary>
    [Fact]
    public void AMinorFactionIsNotASquadron()
    {
        var jump = Scrubbed(
            """{"event":"FSDJump","StarSystem":"Zapalang","SystemFaction":{"Name":"Peraesii Empire Consulate"},"Factions":[{"Name":"Zapalang Silver Power Services","Influence":0.085402}]}""",
            new Pseudonyms());

        Assert.Contains("Peraesii Empire Consulate", jump);
        Assert.Contains("Zapalang Silver Power Services", jump);
    }

    /// <summary>
    /// A squadron's carrier is identified quite differently from a private one, and the callsign rules
    /// missed all of it: <c>Callsign</c> holds the squadron tag rather than a <c>XXX-XXX</c> callsign,
    /// <c>StationName</c> holds that bare tag, and a scan reads <c>"EXA EXAMPLE HORIZON | EX01"</c>.
    /// </summary>
    [Fact]
    public void ASquadronsCarrierIsIdentifiedDifferentlyAndStillGoes()
    {
        var names = new Pseudonyms();

        var stats = Scrubbed(
            """{"event":"CarrierStats","CarrierID":3713474048,"CarrierType":"SquadronCarrier","Callsign":"EX01","Name":"EXA EXAMPLE HORIZON","DockingAccess":"squadron"}""",
            names);

        Assert.DoesNotContain("EXA EXAMPLE HORIZON", stats);
        Assert.DoesNotContain("EX01", stats);
        Assert.Contains("SquadronCarrier", stats);

        var signal = Scrubbed(
            """{"event":"FSSSignalDiscovered","SignalName":"EXA EXAMPLE HORIZON | EX01","SignalType":"FleetCarrier"}""",
            names);

        Assert.DoesNotContain("EX01", signal);

        // The bare tag where the station says it is a carrier...
        Assert.DoesNotContain(
            "EX01",
            Scrubbed(
                """{"event":"Docked","StationName":"EX01","StationType":"FleetCarrier","MarketID":3713474048}""",
                names));

        // ...and where it says nothing at all, which is the case the shape rules cannot reach.
        Assert.DoesNotContain(
            "EX01",
            Scrubbed("""{"event":"StoredShips","StationName":"EX01","ShipsHere":[]}""", names));
    }

    /// <summary>The hole the symbol exemption opened for itself.</summary>
    [Fact]
    public void APlayerWearingASymbolsClothesIsStillAPlayer()
    {
        var names = new Pseudonyms();

        var chat = Scrubbed(
            """{"event":"ReceiveText","From":"$cmdr_decorate:#name=EXAMPLE CHARLIE;","Message":"hi","Channel":"player"}""",
            names);

        Assert.DoesNotContain("EXAMPLE CHARLIE", chat);

        // The wrapper survives, because it is game state: a replay that undecorates names takes a different
        // branch on a value that is no longer decorated.
        Assert.Contains("$cmdr_decorate:#name=", chat);

        // An NPC in the same clothes is left entirely alone — it is a Frontier symbol and a lookup a replay
        // may key on.
        Assert.Contains(
            "$npc_name_decorate:#name=Amilia Sutton;",
            Scrubbed(
                """{"event":"ReceiveText","From":"$npc_name_decorate:#name=Amilia Sutton;","Message":"x","Channel":"npc"}""",
                names));
    }

    /// <summary>
    /// And the prose half of that pair, which leaked after the raw half was fixed: the rules run in
    /// table order, so by the time the localised field is reached its partner already holds a stand-in
    /// and the real name is only in the map.
    /// </summary>
    [Fact]
    public void BothHalvesOfAPilotsNameReadAsTheSamePerson()
    {
        var scrubbed = Scrubbed(
            """{"event":"ShipTargeted","PilotName":"$RolePanel2_unmanned; $cmdr_decorate:#name=JOHN DEPARAGON;","PilotName_Localised":"unmanned CMDR JOHN DEPARAGON","Ship":"sidewinder"}""",
            new Pseudonyms());

        Assert.NotNull(scrubbed);
        Assert.DoesNotContain("JOHN DEPARAGON", scrubbed);

        // One person, one stand-in, in both fields — a person with two is a person a reader of the report
        // cannot follow.
        Assert.Contains("CMDR ALPHA", scrubbed);
        Assert.DoesNotContain("CMDR BRAVO", scrubbed);

        // The game state around the name survives.
        Assert.Contains("RolePanel2_unmanned", scrubbed);
        Assert.Contains("unmanned CMDR", scrubbed);
    }

    /// <summary>
    /// Two more found by the same sweep: a private group, which people name after themselves, and
    /// whoever committed a crime against the Commander — a real person every time, since an NPC does
    /// not generate one of these.
    /// </summary>
    [Fact]
    public void APrivateGroupAndAnOffenderAreBothPeople()
    {
        var names = new Pseudonyms();

        var load = Scrubbed(
            """{"event":"LoadGame","FID":"F735466","Commander":"JOHN DEPARAGON","Group":"EXAMPLE BRAVO","Ship":"python"}""",
            names);

        Assert.DoesNotContain("EXAMPLE BRAVO", load);
        Assert.DoesNotContain("JOHN DEPARAGON", load);
        Assert.Contains("\"Ship\":\"python\"", load);

        var crime = Scrubbed(
            """{"event":"CrimeVictim","Offender":"EXAMPLE CHARLIE","CrimeType":"assault","Bounty":400}""",
            names);

        Assert.DoesNotContain("EXAMPLE CHARLIE", crime);
        Assert.Contains("\"CrimeType\":\"assault\"", crime);
        Assert.Contains("\"Bounty\":400", crime);
    }

    /// <summary>A field whose name means one thing wherever it appears, replaced wherever it does.</summary>
    [Fact]
    public void ASquadronIsReplacedWhateverEventNamesIt()
    {
        var names = new Pseudonyms();

        Assert.Contains(
            "SQUADRON ALPHA",
            Scrubbed("""{"event":"SquadronStartup","SquadronName":"The Fuel Rats","CurrentRank":3}""", names));

        Assert.Contains(
            "SQUADRON ALPHA",
            Scrubbed("""{"event":"WonATrophyForSquadron","SquadronName":"The Fuel Rats"}""", names));
    }

    /// <summary>Fail closed.</summary>
    [Fact]
    public void ALineThatWillNotParseIsWithheldWholeAndCounted()
    {
        Assert.Null(Scrubbed("{\"event\":\"FSDJump\",", new Pseudonyms()));

        // **And a line that parses and then will not be read.** Elite writes duplicate keys — an
        // assassination mission carries `Target` twice, 11 lines over the 912-journal corpus — which JsonNode
        // accepts and then throws on at the first enumeration, with an exception type the catch here did not
        // name.
        Assert.Null(Scrubbed(
            """{"event":"MissionAccepted","Target":"$MissionUtil_FactionTag_Terrorists;","Target":"David Eaves","MissionID":1}""",
            new Pseudonyms()));

        var excerpt = IncidentExcerpt.Take(
            [Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}"""), Entry("not json at all", 1)],
            [],
            Window());

        Assert.Single(excerpt.Journal);
        Assert.Equal(1, excerpt.Tally.JournalWithheld);
    }

    /// <summary>Oldest first, whatever order the page held them in.</summary>
    [Fact]
    public void TheJournalHalfComesOutInTheOrderItHappened()
    {
        var excerpt = IncidentExcerpt.Take(
            [
                Entry("""{"event":"Docked","StationName":"Jameson Memorial"}""", 3),
                Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}""", 1),
            ],
            [],
            Window());

        Assert.Contains("FSDJump", excerpt.Journal[0]);
        Assert.Contains("Docked", excerpt.Journal[1]);
    }

    /// <summary>Outside the window is outside the excerpt, on both sides of the mark.</summary>
    [Fact]
    public void OnlyTheWindowTravels()
    {
        var excerpt = IncidentExcerpt.Take(
            [
                Entry("""{"event":"Liftoff"}""", -30),
                Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}""", 4),
                Entry("""{"event":"Touchdown"}""", 90),
            ],
            [],
            new ExcerptRequest(Noon.AddMinutes(5), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), false));

        Assert.Single(excerpt.Journal);
        Assert.Contains("FSDJump", excerpt.Journal[0]);
    }

    // ---- The log half: the opposite rule ----

    private const string Log = """
        [12:03:00 INF] D47.App.AppHost: Heard: the docking computer did nothing
        [12:03:01 INF] D47.Core.Audio.SpeechPipeline: ShipAi said: Docking computer engaged, Commander.
        [12:03:02 INF] D47.Core.Callouts.CalloutEngine: Callout message.local: watch where you're going
        [12:03:03 INF] D47.Core.Audio.SpeechPipeline: Example Delta said: watch where you're going
        [12:03:04 INF] D47.Core.Configuration.SettingsService: Settings now read for Commander JOHN DEPARAGON (F735466)
        """;

    /// <summary>Announcements, what it said, errors and timings are what the report is evidence of.</summary>
    [Fact]
    public void D47sOwnLinesTravel()
    {
        var excerpt = IncidentExcerpt.Take([], Entries(Log), Window());

        Assert.Contains(excerpt.Log, line => line.Contains("Docking computer engaged"));
        Assert.Contains(excerpt.Log, line => line.Contains("Settings now read"));
    }

    /// <summary>Held back unless the Commander says so, and asked per incident.</summary>
    [Fact]
    public void TheCommandersOwnSpeechIsHeldBackUntilTheySoSay()
    {
        var without = IncidentExcerpt.Take([], Entries(Log), Window());

        Assert.DoesNotContain(without.Log, line => line.Contains("the docking computer did nothing"));
        Assert.Equal(1, without.Tally.MySpeechLines);
        Assert.False(without.Tally.MySpeechIncluded);

        var with = IncidentExcerpt.Take([], Entries(Log), Window(mySpeech: true));

        Assert.Contains(with.Log, line => line.Contains("the docking computer did nothing"));
        Assert.True(with.Tally.MySpeechIncluded);
    }

    /// <summary>And there is no switch for this one.</summary>
    [Fact]
    public void SomebodyElsesWordsNeverTravelAtAll()
    {
        foreach (var excerpt in new[]
                 {
                     IncidentExcerpt.Take([], Entries(Log), Window()),
                     IncidentExcerpt.Take([], Entries(Log), Window(mySpeech: true)),
                 })
        {
            Assert.DoesNotContain(excerpt.Log, line => line.Contains("watch where you're going"));

            Assert.Contains(excerpt.Log, line => line.Contains("Callout message.local: " + LogScrub.Withheld));
            Assert.Contains(excerpt.Log, line => line.Contains("Example Delta said: " + LogScrub.Withheld));

            Assert.Equal(2, excerpt.Tally.InGameMessages);
        }
    }

    /// <summary>
    /// The pseudonyms cross over, and nothing else would make them worth having: a scrubbed
    /// <c>LoadGame</c> three lines above the real name has protected nothing.
    /// </summary>
    [Fact]
    public void TheJournalsStandInsReachTheLogToo()
    {
        var excerpt = IncidentExcerpt.Take(
            [Entry("""{"event":"Commander","FID":"F735466","Name":"JOHN DEPARAGON"}""", 3)],
            Entries(Log),
            Window());

        Assert.DoesNotContain(excerpt.Log, line => line.Contains("JOHN DEPARAGON"));
        Assert.DoesNotContain(excerpt.Log, line => line.Contains("F735466"));
        Assert.Contains(excerpt.Log, line => line.Contains("CMDR ALPHA"));
    }

    /// <summary>And the name has to be there to substitute.</summary>
    [Fact]
    public void TheNameIsReplacedEvenWhenTheWindowNeverSawItArrive()
    {
        const string spoken =
            "[12:03:00 INF] D47.Core.Audio.SpeechPipeline: D47 said: JOHN DEPARAGON is in Eurybia, docked.";

        var blind = IncidentExcerpt.Take([], Entries(spoken), Window());

        Assert.Contains(blind.Log, line => line.Contains("JOHN DEPARAGON"));

        var told = IncidentExcerpt.Take(
            [],
            Entries(spoken),
            Window(),
            commander: new CommanderIdentity("F735466", "JOHN DEPARAGON"));

        Assert.DoesNotContain(told.Log, line => line.Contains("JOHN DEPARAGON"));
        Assert.Contains(told.Log, line => line.Contains("CMDR ALPHA is in Eurybia"));
    }

    /// <summary>Whatever else the host asks to be substituted — the Windows account, in practice.</summary>
    [Fact]
    public void TheHostsOwnSubstitutionsAreAppliedToo()
    {
        var excerpt = IncidentExcerpt.Take(
            [],
            Entries("[12:03:00 INF] D47.App.AppHost: Loaded C:\\Users\\dougs\\data\\settings.json"),
            Window(),
            [new KeyValuePair<string, string>("C:\\Users\\dougs", "%USERPROFILE%")]);

        Assert.Contains(excerpt.Log, line => line.Contains("%USERPROFILE%\\data\\settings.json"));
        Assert.DoesNotContain(excerpt.Log, line => line.Contains("dougs"));
    }

    /// <summary>
    /// An exception renders across several lines with no timestamp after the first, and a stack trace
    /// cut in half is worse evidence than no stack trace.
    /// </summary>
    [Fact]
    public void AStackTraceStaysWithTheLineItBelongsTo()
    {
        var entry = Assert.Single(Entries(
            "[12:03:00 ERR] D47.App.AppHost: Could not transcribe an utterance\n"
            + "System.InvalidOperationException: no model\n"
            + "   at D47.Stt.WhisperTranscriber.Transcribe()"));

        Assert.Contains("InvalidOperationException", entry.Text);
        Assert.Contains("WhisperTranscriber", entry.Text);
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 12, 3, 0, TimeSpan.Zero), entry.At);
    }

    /// <summary>A Commander flying at midnight, which is two files rather than one wrap-around case now.</summary>
    [Fact]
    public void AWindowThatCrossesMidnightTakesTheRightNight()
    {
        IReadOnlyList<LogEntry> log =
        [
            .. Entries("[23:58:00 INF] D47.App.AppHost: the night before", day: 27),
            .. Entries("[23:58:00 INF] D47.App.AppHost: just before", day: 28),
            .. Entries(
                "[00:01:00 INF] D47.App.AppHost: just after\n"
                + "[12:00:00 INF] D47.App.AppHost: the afternoon",
                day: 29),
        ];

        var excerpt = IncidentExcerpt.Take(
            [],
            log,
            new ExcerptRequest(
                new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5),
                false));

        Assert.Equal(2, excerpt.Log.Count);
        Assert.Contains(excerpt.Log, line => line.Contains("just before"));
        Assert.Contains(excerpt.Log, line => line.Contains("just after"));
        Assert.DoesNotContain(excerpt.Log, line => line.Contains("the night before"));
        Assert.DoesNotContain(excerpt.Log, line => line.Contains("the afternoon"));
    }

    // ---- The report: what is shown is what leaves ----

    /// <summary>
    /// The sentence about what is missing is said in every case, including the case where nothing was:
    /// a silence about names and "no name was found to replace" read the same to anybody who does not
    /// already know the rule, and only one of them is a claim.
    /// </summary>
    [Fact]
    public void TheReportSaysWhatWasHeldBackEvenWhenNothingWas()
    {
        var quiet = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}""", 3)],
                [],
                Window()),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("No name or ID was found to replace", quiet);
        Assert.Contains("no in-game message arrived", quiet);
        Assert.Contains("said nothing aloud", quiet);

        var busy = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [Entry("""{"event":"Commander","FID":"F735466","Name":"JOHN DEPARAGON"}""", 3)],
                Entries(Log),
                Window()),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("2 names and IDs replaced", busy);
        Assert.Contains("2 in-game messages withheld", busy);
        Assert.Contains("held back (1 line)", busy);
    }

    /// <summary>The paperwork: which build, when, and where to ask for it back.</summary>
    [Fact]
    public void TheReportCarriesItsOwnPaperwork()
    {
        var report = ExcerptReport.Render(
            IncidentExcerpt.Take([], [], Window()),
            new ExcerptPaperwork("0.85.0+8b21b3d", Noon));

        Assert.StartsWith(ExcerptReport.Marker, report, StringComparison.Ordinal);
        Assert.Contains("0.85.0+8b21b3d", report);
        Assert.Contains("2026-08-28 12:00:00Z", report);
        Assert.Contains("CorpusReplay", report);
        // **Not a deletion promise.** The old wording said the excerpt lived in one issue and would be
        // deleted on request, and a public repository can honour neither half: a comment is archived by third
        // parties within the hour and mailed whole to every watcher (<.com/dseelinger/d47/issues/165>).
        Assert.DoesNotContain("it is deleted", report);
        Assert.Contains("beyond anyone's reach", report);

 // **And it names where the rest of the answer is**.
        Assert.Contains(DonationNotice.Url, report);
    }

    /// <summary>A log line is free text and d47 has been known to say something with a fence in it.</summary>
    [Fact]
    public void AFenceInsideALogLineDoesNotEndTheBlock()
    {
        var report = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [],
                Entries("[12:03:00 INF] D47.App.AppHost: it printed ``` and carried on"),
                Window()),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("````text", report);
        Assert.Contains("it printed ``` and carried on", report);
    }
}
