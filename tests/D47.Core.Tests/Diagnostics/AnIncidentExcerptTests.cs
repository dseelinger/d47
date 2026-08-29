using D47.Core.Diagnostics.Donation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>
/// Donating the incident behind a bug report
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// The three properties the issue calls load-bearing are the three things asserted here: raw never
/// leaves, what is shown is what leaves, and somebody else's words never travel at all.
/// </para>
/// </summary>
public class AnIncidentExcerptTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static JournalEntry Entry(string compact, int minute = 0) =>
        new(Noon.AddMinutes(minute), Kind(compact), "said", compact, compact);

    /// <summary>
    /// The event name off a line, or nothing where there is no line to read one from — the
    /// unreadable case is one of the things under test, so the helper that builds it must survive
    /// being handed rubbish.
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

    /// <summary>The scrubbed line alone, where the test is about the line rather than the count.</summary>
    private static string? Scrubbed(string json, Pseudonyms names) => JournalScrub.Line(json, names).Json;

    private static ExcerptRequest Window(bool mySpeech = false) =>
        new(Noon.AddMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), mySpeech);

    // ---- The journal half: a field list, and nothing beyond it ----

    /// <summary>
    /// The identity events are the front of every journal file, and they are the first thing the
    /// list exists for.
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

        // Still a Commander event with both fields on it. The shape is what makes it a replay case.
        Assert.Contains("\"event\":\"Commander\"", scrubbed);
        Assert.Contains("\"FID\"", scrubbed);
    }

    /// <summary>
    /// The same person, twice, under the two different field names Elite uses. A reader has to be
    /// able to follow one person across a dozen events, which is the whole of "consistent".
    /// </summary>
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
    /// The rule the whole list is drawn around: everything <em>else</em> in a journal is a fact
    /// about the game, and a scrubber that touched it would turn a replay case into a redaction.
    /// </summary>
    [Fact]
    public void AJumpTravelsExactlyAsEliteWroteIt()
    {
        const string jump =
            """{"timestamp":"2026-08-28T12:00:00Z","event":"FSDJump","StarSystem":"Eurybia","SystemEconomy":"$economy_Industrial;","JumpDist":18.42}""";

        Assert.Equal(jump, Scrubbed(jump, new Pseudonyms()));
    }

    /// <summary>
    /// A donor cannot consent on another player's behalf. The body goes; the fact that a message
    /// arrived, on which channel, stays — because that is often the defect.
    /// </summary>
    [Fact]
    public void AMessageKeepsItsShapeAndLosesItsWords()
    {
        var scrubbed = Scrubbed(
            """{"event":"ReceiveText","From":"Don Tazeme","Message":"Oh no you don't!","Message_Localised":"Oh no you don't!","Channel":"local"}""",
            new Pseudonyms());

        Assert.NotNull(scrubbed);
        Assert.DoesNotContain("Oh no you don't", scrubbed);
        Assert.DoesNotContain("Don Tazeme", scrubbed);

        Assert.Contains("\"Channel\":\"local\"", scrubbed);
        Assert.Contains(JournalScrub.Withheld, scrubbed);
        Assert.Contains("CMDR ALPHA", scrubbed);
    }

    /// <summary>
    /// And the report has to be able to say so. A dropped body counted only in the log half made
    /// the report claim <i>no in-game message arrived in this window</i> over an excerpt that
    /// carried a blanked <c>ReceiveText</c> — found flying it against a real journal, which is the
    /// only place the two halves are ever both non-empty.
    /// </summary>
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
                string.Empty,
                Window(),
                Utc),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("1 in-game message withheld", report);
        Assert.DoesNotContain("no in-game message arrived", report);
    }

    /// <summary>An array of bare strings, which is the shape a wing arrives in.</summary>
    [Fact]
    public void EveryNameInAWingIsReplaced()
    {
        var scrubbed = Scrubbed(
            """{"event":"WingJoin","Others":["Ilse Bruhn","Don Tazeme"]}""",
            new Pseudonyms());

        Assert.NotNull(scrubbed);
        Assert.DoesNotContain("Ilse Bruhn", scrubbed);
        Assert.DoesNotContain("Don Tazeme", scrubbed);
        Assert.Contains("CMDR ALPHA", scrubbed);
        Assert.Contains("CMDR BRAVO", scrubbed);
    }

    /// <summary>
    /// The ship's name is the Commander's, and <c>Loadout</c> carries it as well as the event that
    /// sets it — which is the addition this build made to the enumerated list, and the reason for
    /// it: <c>Loadout</c> is the event every excerpt contains.
    /// </summary>
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
    /// An interdiction is overwhelmingly a Frontier pirate, and Elite says which with
    /// <c>IsPlayer</c> — so the rule fires on the person and leaves the NPC alone. Measured over
    /// the 912-journal corpus: 67 interdictions, not one of them a player, because the Commander
    /// does not fly Open. Plenty of donors will.
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
            """{"event":"Interdicted","Submitted":false,"Interdictor":"Don Tazeme","IsPlayer":true}""",
            new Pseudonyms());

        Assert.DoesNotContain("Don Tazeme", person);
        Assert.Contains("CMDR ALPHA", person);
    }

    /// <summary>
    /// A condition that is absent is a condition that is not met. Elite omits <c>IsPlayer</c> from
    /// events it has nothing to say about, and a missing flag read as permission would fire the
    /// gate on exactly the events nobody has vouched for.
    /// </summary>
    [Fact]
    public void AMissingFlagIsNotPermission()
    {
        var scrubbed = Scrubbed(
            """{"event":"EscapeInterdiction","Interdictor":"Dedy Sofyan"}""",
            new Pseudonyms());

        Assert.Contains("Dedy Sofyan", scrubbed);
    }

    /// <summary>
    /// <c>PVPKill</c> needs no condition for the opposite reason: its victim is a player by
    /// definition, and the event exists only because one was.
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

    /// <summary>
    /// And the one Elite does not flag. A <c>Died</c> carries no <c>IsPlayer</c>, and an NPC's
    /// generated name has the same shape as a Commander's — so "cannot tell" resolves to scrub.
    /// Over-replacing a Frontier pirate costs a replay a name nothing reasons about; under-replacing
    /// hands over the one thing this class exists to keep.
    /// </summary>
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

    /// <summary>
    /// A Frontier symbol is not a person. <c>$ShipName_Military_Federation;</c> killed the
    /// Commander eleven times in the corpus — replacing it would break a lookup a replay may key
    /// on, and no Commander is called one.
    /// </summary>
    [Fact]
    public void AFrontierSymbolIsLeftWhereItIs()
    {
        var scrubbed = Scrubbed(
            """{"event":"Died","KillerName":"$ShipName_Military_Federation;","KillerName_Localised":"Federal Navy Ship","KillerShip":"federation_gunship"}""",
            new Pseudonyms());

        Assert.Contains("$ShipName_Military_Federation;", scrubbed);

        // And its translation goes with it. `X` and `X_Localised` are one datum rendered twice, so
        // a killer that is a ship class stays a ship class in both fields — replacing only the
        // readable half produced "KillerName": "$ShipName_Military_Federation;" beside
        // "KillerName_Localised": "CMDR ALPHA", which is how this was found.
        Assert.Contains("Federal Navy Ship", scrubbed);

        // And a message body still goes, symbol or not: the words are not what a replay needs.
        Assert.DoesNotContain(
            "$COMMS_entered:",
            Scrubbed(
                """{"event":"ReceiveText","From":"","Message":"$COMMS_entered:#name=Eurybia;","Channel":"npc"}""",
                new Pseudonyms()));
    }

    /// <summary>
    /// A carrier's name and its callsign, ruled PII by the Commander on 2026-08-29: both can be
    /// looked up on INARA, and the callsign is the key that site indexes carriers by.
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

        // The same callsign a second time, in the field it mostly lives in, and on an event with
        // no carrier in its name at all.
        var docked = Scrubbed(
            """{"event":"Docked","StationName":"B0X-79X","StationType":"FleetCarrier","MarketID":3712682240}""",
            names);

        Assert.Contains("ZZ0-001", docked);
        Assert.Contains("FleetCarrier", docked);
    }

    /// <summary>
    /// And an ordinary station keeps its name. The rule reaches every event because the treatment
    /// guards itself on the shape Frontier reserves — measured over the corpus: 24 of 968 distinct
    /// station names match it, and every one of the 24 is a carrier.
    /// </summary>
    [Fact]
    public void AnOrdinaryStationIsNotACarrier()
    {
        var docked = Scrubbed(
            """{"event":"Docked","StationName":"Jameson Memorial","StationType":"Orbis","MarketID":128666762}""",
            new Pseudonyms());

        Assert.Contains("Jameson Memorial", docked);
    }

    /// <summary>
    /// Five of the nineteen events carrying a callsign have no <c>StationType</c> to condition on,
    /// and they are the ones that list a Commander's whole fleet. A per-event condition would have
    /// left the callsign exactly there.
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
    /// Somebody else's carrier, seen from across a system, with its name and callsign in one
    /// string — <c>"HMS BROTHEL X8H-B0Y"</c> is a real signal off the corpus. The shape guard
    /// cannot help where the callsign is embedded rather than alone, so the whole value goes,
    /// conditioned on Elite's own word for what the signal is.
    /// </summary>
    [Fact]
    public void AStrangersCarrierGoesWholeWhenElitesSaysItIsOne()
    {
        var names = new Pseudonyms();

        var carrier = Scrubbed(
            """{"event":"FSSSignalDiscovered","SystemAddress":6405910172338,"SignalName":"HMS BROTHEL X8H-B0Y","SignalType":"FleetCarrier","IsStation":true}""",
            names);

        Assert.DoesNotContain("HMS BROTHEL", carrier);
        Assert.DoesNotContain("X8H-B0Y", carrier);
        Assert.Contains("CARRIER ALPHA", carrier);

        // A megaship wears the same shape at the front, and is a game fact. So is a minor faction
        // named for a catalogue star. 464 and 63 distinct in the corpus, and both would have gone
        // under a rule that looked for the shape anywhere rather than at the end.
        Assert.Contains(
            "MVU-891 Bellmarsh-class Reformatory",
            Scrubbed(
                """{"event":"FSSSignalDiscovered","SignalName":"MVU-891 Bellmarsh-class Reformatory","SignalType":"Megaship"}""",
                names));

        Assert.Contains(
            "LP 466-235 Gold Boys",
            Scrubbed("""{"event":"FSDJump","SystemFaction":{"Name":"LP 466-235 Gold Boys"}}""", names));

        // And the drop target, which is where this was found: a field called Type, mixing symbols,
        // ordinary stations and carriers with nothing on the event to tell them apart.
        var drop = Scrubbed(
            """{"event":"SupercruiseDestinationDrop","Type":"GDS PREDATOR B0X-79X","Threat":0}""",
            names);

        Assert.DoesNotContain("GDS PREDATOR", drop);
        Assert.DoesNotContain("B0X-79X", drop);

        Assert.Contains(
            "Ray Gateway",
            Scrubbed("""{"event":"SupercruiseDestinationDrop","Type":"Ray Gateway","Threat":0}""", names));

        // And what you were nearest to when you scanned something, which was the last residue a
        // corpus sweep turned up: 11 CodexEntry lines out of 179,378.
        Assert.DoesNotContain(
            "GDS PREDATOR",
            Scrubbed(
                """{"event":"CodexEntry","Name":"$Codex_Ent_G_Type_Name;","NearestDestination":"GDS PREDATOR B0X-79X"}""",
                names));

        // Every other signal is a game fact and keeps its name. There are hundreds of thousands of
        // these, and rewriting them would gut the replay case.
        var beacon = Scrubbed(
            """{"event":"FSSSignalDiscovered","SignalName":"$USS_HighGradeEmissions;","SignalType":"USS"}""",
            names);

        Assert.Contains("$USS_HighGradeEmissions;", beacon);
    }

    /// <summary>
    /// And the same hole the Commander's name had: <c>CarrierStats</c> is common but not
    /// guaranteed to be in a six-minute window, while d47 says the carrier's name and callsign in
    /// what it tells you about a jump.
    /// </summary>
    [Fact]
    public void TheCarrierIsReplacedInTheLogEvenWhenTheWindowNeverSawIt()
    {
        const string spoken =
            "[12:03:00 INF] D47.Core.Audio.SpeechPipeline: D47 said: GDS PREDATOR (B0X-79X) jumps in 4 minutes.";

        var told = IncidentExcerpt.Take(
            [],
            spoken,
            Window(),
            Utc,
            carrier: new CarrierState { Name = "GDS PREDATOR", CallSign = "B0X-79X" });

        Assert.DoesNotContain(told.Log, line => line.Contains("GDS PREDATOR"));
        Assert.DoesNotContain(told.Log, line => line.Contains("B0X-79X"));
        Assert.Contains(told.Log, line => line.Contains("CARRIER ALPHA (ZZ0-001)"));
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

    /// <summary>
    /// Fail closed. A line the scrubber could not read is a line nobody has checked, and the whole
    /// claim being made about an excerpt is that everything in it was checked.
    /// </summary>
    [Fact]
    public void ALineThatWillNotParseIsWithheldWholeAndCounted()
    {
        Assert.Null(Scrubbed("{\"event\":\"FSDJump\",", new Pseudonyms()));

        var excerpt = IncidentExcerpt.Take(
            [Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}"""), Entry("not json at all", 1)],
            string.Empty,
            Window(),
            Utc);

        Assert.Single(excerpt.Journal);
        Assert.Equal(1, excerpt.Tally.JournalWithheld);
    }

    /// <summary>
    /// Oldest first, whatever order the page held them in. A replay is not a reader, and a state
    /// machine driven backwards is not a regression test.
    /// </summary>
    [Fact]
    public void TheJournalHalfComesOutInTheOrderItHappened()
    {
        var excerpt = IncidentExcerpt.Take(
            [
                Entry("""{"event":"Docked","StationName":"Jameson Memorial"}""", 3),
                Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}""", 1),
            ],
            string.Empty,
            Window(),
            Utc);

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
            string.Empty,
            new ExcerptRequest(Noon.AddMinutes(5), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), false),
            Utc);

        Assert.Single(excerpt.Journal);
        Assert.Contains("FSDJump", excerpt.Journal[0]);
    }

    // ---- The log half: the opposite rule ----

    private const string Log = """
        [12:03:00 INF] D47.App.AppHost: Heard: the docking computer did nothing
        [12:03:01 INF] D47.Core.Audio.SpeechPipeline: ShipAi said: Docking computer engaged, Commander.
        [12:03:02 INF] D47.Core.Callouts.CalloutEngine: Callout message.local: watch where you're going
        [12:03:03 INF] D47.Core.Audio.SpeechPipeline: Don Tazeme said: watch where you're going
        [12:03:04 INF] D47.Core.Configuration.SettingsService: Settings now read for Commander JOHN DEPARAGON (F735466)
        """;

    /// <summary>Announcements, what it said, errors and timings are what the report is evidence of.</summary>
    [Fact]
    public void D47sOwnLinesTravel()
    {
        var excerpt = IncidentExcerpt.Take([], Log, Window(), Utc);

        Assert.Contains(excerpt.Log, line => line.Contains("Docking computer engaged"));
        Assert.Contains(excerpt.Log, line => line.Contains("Settings now read"));
    }

    /// <summary>
    /// Held back unless the Commander says so, and asked per incident. Sometimes the exact words
    /// are the bug and sometimes they are nobody's business, and only they can tell which.
    /// </summary>
    [Fact]
    public void TheCommandersOwnSpeechIsHeldBackUntilTheySoSay()
    {
        var without = IncidentExcerpt.Take([], Log, Window(), Utc);

        Assert.DoesNotContain(without.Log, line => line.Contains("the docking computer did nothing"));
        Assert.Equal(1, without.Tally.MySpeechLines);
        Assert.False(without.Tally.MySpeechIncluded);

        var with = IncidentExcerpt.Take([], Log, Window(mySpeech: true), Utc);

        Assert.Contains(with.Log, line => line.Contains("the docking computer did nothing"));
        Assert.True(with.Tally.MySpeechIncluded);
    }

    /// <summary>
    /// And there is no switch for this one. A re-voiced in-game message is another player's
    /// sentence in both the places d47 writes it down, and the shape stays so the report can still
    /// say a message arrived and when.
    /// </summary>
    [Fact]
    public void SomebodyElsesWordsNeverTravelAtAll()
    {
        foreach (var excerpt in new[]
                 {
                     IncidentExcerpt.Take([], Log, Window(), Utc),
                     IncidentExcerpt.Take([], Log, Window(mySpeech: true), Utc),
                 })
        {
            Assert.DoesNotContain(excerpt.Log, line => line.Contains("watch where you're going"));

            Assert.Contains(excerpt.Log, line => line.Contains("Callout message.local: " + LogScrub.Withheld));
            Assert.Contains(excerpt.Log, line => line.Contains("Don Tazeme said: " + LogScrub.Withheld));

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
            Log,
            Window(),
            Utc);

        Assert.DoesNotContain(excerpt.Log, line => line.Contains("JOHN DEPARAGON"));
        Assert.DoesNotContain(excerpt.Log, line => line.Contains("F735466"));
        Assert.Contains(excerpt.Log, line => line.Contains("CMDR ALPHA"));
    }

    /// <summary>
    /// And the name has to be there to substitute. Elite writes <c>Commander</c> and
    /// <c>LoadGame</c> once, at the front of a session, so an incident three hours in contains
    /// neither — while d47's log names the Commander in what it says, over and over. Without the
    /// identity handed in from outside the window, the log half leaked the name the journal half
    /// exists to remove.
    /// </summary>
    [Fact]
    public void TheNameIsReplacedEvenWhenTheWindowNeverSawItArrive()
    {
        const string spoken =
            "[12:03:00 INF] D47.Core.Audio.SpeechPipeline: D47 said: JOHN DEPARAGON is in Eurybia, docked.";

        var blind = IncidentExcerpt.Take([], spoken, Window(), Utc);

        Assert.Contains(blind.Log, line => line.Contains("JOHN DEPARAGON"));

        var told = IncidentExcerpt.Take(
            [],
            spoken,
            Window(),
            Utc,
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
            "[12:03:00 INF] D47.App.AppHost: Loaded C:\\Users\\dougs\\data\\settings.json",
            Window(),
            Utc,
            [new KeyValuePair<string, string>("C:\\Users\\dougs", "%USERPROFILE%")]);

        Assert.Contains(excerpt.Log, line => line.Contains("%USERPROFILE%\\data\\settings.json"));
        Assert.DoesNotContain(excerpt.Log, line => line.Contains("dougs"));
    }

    /// <summary>
    /// An exception renders across several lines with no timestamp after the first, and a stack
    /// trace cut in half is worse evidence than no stack trace.
    /// </summary>
    [Fact]
    public void AStackTraceStaysWithTheLineItBelongsTo()
    {
        var entry = Assert.Single(LogScrub.Parse(
            "[12:03:00 ERR] D47.App.AppHost: Could not transcribe an utterance\n"
            + "System.InvalidOperationException: no model\n"
            + "   at D47.Stt.WhisperTranscriber.Transcribe()"));

        Assert.Contains("InvalidOperationException", entry.Text);
        Assert.Contains("WhisperTranscriber", entry.Text);
        Assert.Equal(new TimeOnly(12, 3, 0), entry.At);
    }

    /// <summary>
    /// A Commander flying at midnight. The log carries a time of day and no date, so a window whose
    /// start is a larger number than its end is the ordinary case rather than a corrupt one.
    /// </summary>
    [Fact]
    public void AWindowThatCrossesMidnightStillSelects()
    {
        var excerpt = IncidentExcerpt.Take(
            [],
            "[23:58:00 INF] D47.App.AppHost: before\n[00:01:00 INF] D47.App.AppHost: after\n"
            + "[12:00:00 INF] D47.App.AppHost: the afternoon",
            new ExcerptRequest(
                new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5),
                false),
            Utc);

        Assert.Equal(2, excerpt.Log.Count);
        Assert.DoesNotContain(excerpt.Log, line => line.Contains("the afternoon"));
    }

    // ---- The report: what is shown is what leaves ----

    /// <summary>
    /// The sentence about what is missing is said in every case, <b>including the case where
    /// nothing was</b>: a silence about names and "no name was found to replace" read the same to
    /// anybody who does not already know the rule, and only one of them is a claim.
    /// </summary>
    [Fact]
    public void TheReportSaysWhatWasHeldBackEvenWhenNothingWas()
    {
        var quiet = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [Entry("""{"event":"FSDJump","StarSystem":"Eurybia"}""", 3)],
                string.Empty,
                Window(),
                Utc),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("No name or ID was found to replace", quiet);
        Assert.Contains("no in-game message arrived", quiet);
        Assert.Contains("said nothing aloud", quiet);

        var busy = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [Entry("""{"event":"Commander","FID":"F735466","Name":"JOHN DEPARAGON"}""", 3)],
                Log,
                Window(),
                Utc),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("2 names and IDs replaced", busy);
        Assert.Contains("2 in-game messages withheld", busy);
        Assert.Contains("held back (1 line)", busy);
    }

    /// <summary>
    /// The paperwork: which build, when, and where to ask for it back. The excerpt exists to become
    /// a replay case and the issue it rides on is the receipt.
    /// </summary>
    [Fact]
    public void TheReportCarriesItsOwnPaperwork()
    {
        var report = ExcerptReport.Render(
            IncidentExcerpt.Take([], string.Empty, Window(), Utc),
            new ExcerptPaperwork("0.85.0+8b21b3d", Noon));

        Assert.StartsWith(ExcerptReport.Marker, report, StringComparison.Ordinal);
        Assert.Contains("0.85.0+8b21b3d", report);
        Assert.Contains("2026-08-28 12:00:00Z", report);
        Assert.Contains("CorpusReplay", report);
        Assert.Contains("Ask here and it is deleted", report);
    }

    /// <summary>
    /// A log line is free text and d47 has been known to say something with a fence in it. A
    /// three-backtick fence would end there and spill the rest of the log into the issue as prose.
    /// </summary>
    [Fact]
    public void AFenceInsideALogLineDoesNotEndTheBlock()
    {
        var report = ExcerptReport.Render(
            IncidentExcerpt.Take(
                [],
                "[12:03:00 INF] D47.App.AppHost: it printed ``` and carried on",
                Window(),
                Utc),
            new ExcerptPaperwork("0.85.0", Noon));

        Assert.Contains("````text", report);
        Assert.Contains("it printed ``` and carried on", report);
    }
}
