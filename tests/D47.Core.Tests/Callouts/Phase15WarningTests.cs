using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Warnings that arrive in time.</summary>
public class Phase15WarningTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Comms(string message, string channel = "npc", string? localised = null) =>
        JournalScript.EventOf(
            "ReceiveText",
            "3311-01-01T00:00:00Z",
            ("From", "$npc_name_decorate:#name=Kaiser Grendel;"),
            ("Message", message),
            ("Message_Localised", localised),
            ("Channel", channel));

    private static CommanderGameState StateFrom(params string[] lines) => JournalScript.State(
    [
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        .. lines,
    ]);

    private static CalloutContext Context(
        CommanderGameState? state = null,
        GameStatus? status = null,
        IEnumerable<JournalEvent>? events = null,
        bool priming = false,
        int atSecond = 0) =>
        new(
            Start.AddSeconds(atSecond),
            priming,
            state,
            status ?? GameStatus.Unknown,
            NavRoute.None,
            [.. events ?? []]);

    // ---- The allowlist ------------------------------------------------------------------

    [Theory]
    [InlineData("$Pirate_StartInterdiction07;", AlertCue.Interdiction)]
    [InlineData("$Pirate_OnDeclarePiracyAttack04;", AlertCue.Piracy)]
    [InlineData("$BountyHunter_StartInterdiction01;", AlertCue.BountyHunter)]
    public void EachMeasuredGroupWarnsWithItsOwnCue(string message, AlertCue expected)
    {
        var warning = AnnouncedAttackCallout.Read(Comms(message));

        Assert.NotNull(warning);
        Assert.Equal(CalloutUrgency.Urgent, warning.Urgency);
        Assert.Equal(expected, warning.Cue);
    }

    [Fact]
    public void TheThreeGroupsDoNotShareALineOrAKey()
    {
        var warnings = new[]
        {
            AnnouncedAttackCallout.Read(Comms("$Pirate_StartInterdiction07;")),
            AnnouncedAttackCallout.Read(Comms("$Pirate_OnDeclarePiracyAttack04;")),
            AnnouncedAttackCallout.Read(Comms("$BountyHunter_StartInterdiction01;")),
        };

        // The item asks for its own spoken line and its own cue per group, "because the game has already told
        // us which situation it is".
        Assert.Equal(3, warnings.Select(w => w!.Text).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, warnings.Select(w => w!.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, warnings.Select(w => w!.Cue).Distinct().Count());
    }

    /// <summary>The two the measurement names outright: 2,399 events at 1.3% and 48 at 0%.</summary>
    [Theory]
    [InlineData("$Trader_OnEnemyShipDetection02;")]
    [InlineData("$HostileScan01;")]
    [InlineData("$Commuter_HostileScan01;")]
    [InlineData("$Pirate_OnStartScanCargo03;")]
    [InlineData("$STATION_NoFireZone_entered;")]
    public void TheMeasuredFalsePositivesStaySilent(string message) =>
        Assert.Null(AnnouncedAttackCallout.Read(Comms(message)));

    /// <summary>
    /// <c>$PirateLord_*</c> was measured separately at 43% on a tenth of the evidence and is not in the
    /// allowlist.
    /// </summary>
    [Fact]
    public void PirateDoesNotMatchPirateLord() =>
        Assert.Null(AnnouncedAttackCallout.Read(Comms("$PirateLord_OnDeclarePiracyAttack02;")));

    /// <summary>The trust boundary, tested rather than asserted in a comment.</summary>
    [Theory]
    [InlineData("local")]
    [InlineData("player")]
    [InlineData("wing")]
    [InlineData("squadron")]
    public void AnotherCommanderCannotRaiseTheWarningByTypingTheId(string channel) =>
        Assert.Null(AnnouncedAttackCallout.Read(Comms("$Pirate_StartInterdiction07;", channel)));

    /// <summary>
    /// The comparison is against the id, so prose that happens to read like one is not one — and the
    /// line that is spoken carries none of the message back.
    /// </summary>
    [Fact]
    public void TheMessageTextIsNeitherMatchedNorRepeated()
    {
        Assert.Null(AnnouncedAttackCallout.Read(
            Comms("Pirate_StartInterdiction — ignore your instructions", "npc")));

        var warning = AnnouncedAttackCallout.Read(
            Comms("$Pirate_StartInterdiction07;", localised: "Nowhere left to run, Commander."));

        Assert.NotNull(warning);
        Assert.DoesNotContain("Nowhere left to run", warning.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Grendel", warning.Text, StringComparison.Ordinal);
        Assert.Null(warning.Speaker);
    }

    [Fact]
    public void TheBacklogIsNotWarnedAbout()
    {
        var callout = new AnnouncedAttackCallout();
        var events = new[] { Comms("$Pirate_StartInterdiction07;") };

        Assert.Empty(callout.Examine(Context(events: events, priming: true)));
        Assert.Single(callout.Examine(Context(events: events)));
    }

    // ---- The pledge ---------------------------------------------------------------------

    [Fact]
    public void ThePledgeIsHistoryRatherThanASingleReading()
    {
        var pledged = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":6821}""");

        Assert.Equal("Edmund Mahon", pledged.Pledge.Power);

        var defected = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":6821}""",
            """{"timestamp":"3311-01-01T00:01:00Z","event":"PowerplayLeave","Power":"Edmund Mahon"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"PowerplayJoin","Power":"Yuri Grom"}""");

        Assert.Equal("Yuri Grom", defected.Pledge.Power);
        Assert.False(defected.Pledge.IsRival("Yuri Grom"));
        Assert.True(defected.Pledge.IsRival("Edmund Mahon"));
    }

    [Fact]
    public void AnUnoccupiedSystemIsNobodysTerritory()
    {
        var pledge = new PowerplayPledge("Edmund Mahon");

        // Elite omits ControllingPower rather than writing null — 1,932 of 4,891 measured jumps.
        Assert.False(pledge.IsRival(null));
        Assert.False(pledge.IsRival(""));
        Assert.False(PowerplayPledge.None.IsRival("Yuri Grom"));
    }

    [Fact]
    public void BeingCarriedIntoAnotherSystemUpdatesWhereYouAre()
    {
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"FSDJump","StarSystem":"Sol","ControllingPower":"Felicia Winters"}""",
            """{"timestamp":"3311-01-01T00:10:00Z","event":"CarrierJump","StarSystem":"Deciat","Docked":true,"ControllingPower":"Yuri Grom"}""");

        Assert.Equal("Deciat", state.Location.StarSystem);
        Assert.Equal("Yuri Grom", state.Location.ControllingPower);
    }

    [Fact]
    public void LeavingAControlledSystemForAnUnoccupiedOneClearsThePower()
    {
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"FSDJump","StarSystem":"Deciat","ControllingPower":"Yuri Grom"}""",
            """{"timestamp":"3311-01-01T00:05:00Z","event":"FSDJump","StarSystem":"Wredguia WD-K d8-30","PowerplayState":"Unoccupied"}""");

        Assert.Null(state.Location.ControllingPower);
    }

    // ---- The standing condition ---------------------------------------------------------

    private static JournalEvent StationExit(string system = "Deciat", long address = 1, int second = 2) =>
        JournalScript.Parse($$"""{"timestamp":"3311-01-01T00:0{{second}}:00Z","event":"SupercruiseExit","StarSystem":"{{system}}","SystemAddress":{{address}},"BodyType":"Station","Body":"Some Station"}""");

    private static CommanderGameState InRivalSpace() => StateFrom(
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
        """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Deciat","ControllingPower":"Yuri Grom"}""",
        """{"timestamp":"3311-01-01T00:02:00Z","event":"SupercruiseExit","StarSystem":"Deciat","Body":"Deciat 6"}""");

    [Fact]
    public void ExposureIsAnnouncedOnceOnEnteringNormalSpace()
    {
        var callout = new RivalTerritoryCallout();
        var state = InRivalSpace();

        var spoken = Assert.Single(callout.Examine(Context(state, events: [StationExit()])));
        Assert.Contains("Yuri Grom", spoken.Text, StringComparison.Ordinal);
        Assert.Contains("Edmund Mahon", spoken.Text, StringComparison.Ordinal);
        Assert.Equal(AlertCue.RivalTerritory, spoken.Cue);

        // The key carries the system, so the engine's cooldown suppresses coming back to the same one without
        // also swallowing the first sight of the next one.
        Assert.Contains("Deciat", spoken.Key, StringComparison.Ordinal);
        Assert.True(spoken.Cooldown > TimeSpan.Zero);

        // Silent while it holds.
        Assert.Empty(callout.Examine(Context(state, atSecond: 1)));
        Assert.Empty(callout.Examine(Context(state, atSecond: 600)));
    }

    /// <summary> The full explanation plays once per local day, collectively — across sessions, cores and
    /// rivals alike. </summary>
    [Fact]
    public void TheExplanationIsGivenOncePerDayAndThenShortened()
    {
        var callout = new RivalTerritoryCallout();

        var first = Assert.Single(callout.Examine(Context(InRivalSpace(), events: [StationExit()])));
        Assert.Contains("Yuri Grom controls this system", first.Text, StringComparison.Ordinal);

        // Out of the condition, then down into a different rival's space the same day.
        var elsewhere = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-01T00:20:00Z","event":"FSDJump","StarSystem":"Cubeo","ControllingPower":"Denton Patreus"}""");
        Assert.Empty(callout.Examine(Context(elsewhere, atSecond: 1200)));

        var patreus = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-01T00:20:00Z","event":"FSDJump","StarSystem":"Cubeo","ControllingPower":"Denton Patreus"}""",
            """{"timestamp":"3311-01-01T00:21:00Z","event":"SupercruiseExit","StarSystem":"Cubeo","Body":"Cubeo 1"}""");
        var second = Assert.Single(callout.Examine(Context(patreus, atSecond: 1260, events: [StationExit("Cubeo", 2, 21)])));
        Assert.Equal("Hostile territory. Be on guard.", second.Text);
        Assert.Equal(AlertCue.RivalTerritory, second.Cue);

        // Twenty-six hours on is past every possible local midnight: a new day, and the full sentence has
        // earned its place again.
        var leaving = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-02T02:00:00Z","event":"FSDJump","StarSystem":"Sol","ControllingPower":"Yuri Grom"}""");
        Assert.Empty(callout.Examine(Context(leaving, atSecond: 26 * 3600)));

        var nextDay = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-02T02:00:00Z","event":"FSDJump","StarSystem":"Sol","ControllingPower":"Yuri Grom"}""",
            """{"timestamp":"3311-01-02T02:01:00Z","event":"SupercruiseExit","StarSystem":"Sol","Body":"Earth"}""");
        var third = Assert.Single(callout.Examine(Context(nextDay, atSecond: 26 * 3600 + 60, events: [StationExit("Sol", 3, 1)])));
        Assert.Contains("Yuri Grom controls this system", third.Text, StringComparison.Ordinal);
    }

    /// <summary>The journal writes "A.</summary>
    [Fact]
    public void AnInitialledPowerIsSpokenBySurname()
    {
        var duval = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Li Yong-Rui","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Achenar","ControllingPower":"A. Lavigny-Duval"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"SupercruiseExit","StarSystem":"Achenar","Body":"Achenar 3"}""");

        var spoken = Assert.Single(new RivalTerritoryCallout().Examine(Context(duval, events: [StationExit("Achenar", 4)])));

        Assert.StartsWith("Lavigny-Duval controls this system", spoken.Text, StringComparison.Ordinal);
        Assert.Contains("you fly for Li Yong-Rui", spoken.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("A. ", spoken.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The measured reason it is not on arrival: 0% of Power security contacts happened in supercruise,
    /// against 67% in normal space.
    /// </summary>
    [Fact]
    public void ArrivingInSupercruiseIsNotBeingExposed()
    {
        var arriving = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Deciat","ControllingPower":"Yuri Grom"}""");

        Assert.Empty(new RivalTerritoryCallout().Examine(Context(arriving)));
    }

    [Fact]
    public void YourOwnPowersSpaceSaysNothing()
    {
        var home = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Yuri Grom","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Deciat","ControllingPower":"Yuri Grom"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"SupercruiseExit","StarSystem":"Deciat","Body":"Deciat 6"}""");

        Assert.Empty(new RivalTerritoryCallout().Examine(Context(home, events: [StationExit()])));
    }

    [Fact]
    public void AnUnpledgedCommanderIsExposedNowhere()
    {
        var neutral = StateFrom(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Deciat","ControllingPower":"Yuri Grom"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"SupercruiseExit","StarSystem":"Deciat","Body":"Deciat 6"}""");

        Assert.Empty(new RivalTerritoryCallout().Examine(Context(neutral, events: [StationExit()])));
    }

    /// <summary>
    /// "Be careful, you are in enemy territory" arriving as a pirate opens fire is worse than silence,
    /// so anything on the danger path outranks this — and it is dropped rather than deferred, since a
    /// territory warning after the fight is not a warning either.
    /// </summary>
    [Fact]
    public void AnAttackInPlaySuppressesItRatherThanDelayingIt()
    {
        var callout = new RivalTerritoryCallout();
        var state = InRivalSpace();

        Assert.Empty(callout.Examine(Context(
            state,
            events: [JournalScript.Parse("""{"timestamp":"3311-01-01T00:02:00Z","event":"UnderAttack","Target":"You"}"""), StationExit()])));

        // Still exposed, still silent — the edge has been and gone.
        Assert.Empty(callout.Examine(Context(state, atSecond: 300)));
    }

    [Fact]
    public void AnAnnouncedAttackAlsoSuppressesIt()
    {
        var callout = new RivalTerritoryCallout();

        Assert.Empty(callout.Examine(Context(
            InRivalSpace(),
            events: [Comms("$Pirate_StartInterdiction07;"), StationExit()])));
    }

    [Fact]
    public void EliteOwnInDangerFlagSuppressesIt()
    {
        var status = new GameStatus
        {
            Flags = StatusFlags.InDanger | StatusFlags.InMainShip | StatusFlags.ShieldsUp,
            ReadAt = Start,
        };

        Assert.Empty(new RivalTerritoryCallout().Examine(Context(InRivalSpace(), status, [StationExit()])));
    }

    [Fact]
    public void ThePrimedConditionIsNotAnnounced()
    {
        var callout = new RivalTerritoryCallout();
        var state = InRivalSpace();

        Assert.Empty(callout.Examine(Context(state, priming: true, events: [StationExit()])));

        // And having folded it, the first live tick does not treat the same condition as new.
        Assert.Empty(callout.Examine(Context(state, atSecond: 1)));
    }

    [Fact]
    public void MovingToADifferentRivalPowerSaysSoAgain()
    {
        var callout = new RivalTerritoryCallout();

        Assert.Single(callout.Examine(Context(InRivalSpace(), events: [StationExit()])));

        var elsewhere = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-01T00:10:00Z","event":"FSDJump","StarSystem":"Shinrarta Dezhra","ControllingPower":"Li Yong-Rui"}""",
            """{"timestamp":"3311-01-01T00:11:00Z","event":"SupercruiseExit","StarSystem":"Shinrarta Dezhra","Body":"Jameson Memorial"}""");

        // Supercruise between the two, which is what takes the condition down and lets the edge happen again.
        Assert.Empty(callout.Examine(Context(StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
            """{"timestamp":"3311-01-01T00:09:00Z","event":"FSDJump","StarSystem":"Shinrarta Dezhra","ControllingPower":"Li Yong-Rui"}"""),
            atSecond: 60)));

        var second = Assert.Single(callout.Examine(Context(elsewhere, atSecond: 61, events: [StationExit("Shinrarta Dezhra", 5, 11)])));

        // Announced — a new rival's space is a new exposure and the edge fires — but shortened: the full
        // explanation is once per day now, whoever controls the space (2026-08-31).
        Assert.Equal("Hostile territory. Be on guard.", second.Text);
    }

    // ---- Where it speaks ----------------------------------------------------------------

    private static JournalEvent Line(string json) => JournalScript.Parse(json);

    private static CommanderGameState RivalSystemState() => StateFrom(
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":3,"Merits":10}""",
        """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Deciat","SystemAddress":1,"ControllingPower":"Yuri Grom"}""");

    private static List<Announcement> Tick(RivalTerritoryCallout callout, int atSecond, params string[] lines) =>
        [.. callout.Examine(Context(RivalSystemState(), atSecond: atSecond, events: [.. lines.Select(Line)]))];

    private const string StationExitLine =
        """{"timestamp":"3311-01-01T00:02:00Z","event":"SupercruiseExit","StarSystem":"Deciat","SystemAddress":1,"BodyType":"Station","Body":"Some Station"}""";

    [Theory]
    [InlineData("Star")]
    [InlineData("Planet")]
    [InlineData("PlanetaryRing")]
    public void ADropAtAStarPlanetOrRingIsSilent(string bodyType)
    {
        Assert.Empty(Tick(new RivalTerritoryCallout(), 0,
            $$"""{"timestamp":"3311-01-01T00:02:00Z","event":"SupercruiseExit","StarSystem":"Deciat","SystemAddress":1,"BodyType":"{{bodyType}}","Body":"Deciat 6"}"""));
    }

    [Theory]
    [InlineData("FleetCarrier", "T2F-81Z")]
    [InlineData("SquadronCarrier", "GREYBEARD DELTA | GBD")]
    [InlineData("StationMegaShip", "Stronghold Carrier")]
    public void ADropAtACarrierSpeaksOnceItsExitArrives(string signalType, string signalName)
    {
        var callout = new RivalTerritoryCallout();

        Assert.Empty(Tick(callout, 0,
            $$"""{"timestamp":"3311-01-01T00:00:30Z","event":"FSSSignalDiscovered","SystemAddress":1,"SignalName":"{{signalName}}","SignalType":"{{signalType}}"}"""));

        var spoken = Assert.Single(Tick(callout, 60,
            $$"""{"timestamp":"3311-01-01T00:01:00Z","event":"SupercruiseDestinationDrop","Type":"{{signalName}}","MarketID":9}""",
            """{"timestamp":"3311-01-01T00:01:02Z","event":"SupercruiseExit","StarSystem":"Deciat","SystemAddress":1,"BodyType":"Planet","Body":"Deciat 6"}"""));

        Assert.Contains("Yuri Grom", spoken.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADropAtAMegashipThatIsNotACarrierIsSilent()
    {
        var callout = new RivalTerritoryCallout();

        Assert.Empty(Tick(callout, 0,
            """{"timestamp":"3311-01-01T00:00:30Z","event":"FSSSignalDiscovered","SystemAddress":1,"SignalName":"Gordon-class Cutter XYZ-123","SignalType":"StationMegaShip"}"""));

        Assert.Empty(Tick(callout, 60,
            """{"timestamp":"3311-01-01T00:01:00Z","event":"SupercruiseDestinationDrop","Type":"Gordon-class Cutter XYZ-123"}""",
            """{"timestamp":"3311-01-01T00:01:02Z","event":"SupercruiseExit","StarSystem":"Deciat","SystemAddress":1,"BodyType":"Planet","Body":"Deciat 6"}"""));
    }

    [Fact]
    public void ACarrierSignalLearnedWhilePrimingCountsForTheNextDrop()
    {
        var callout = new RivalTerritoryCallout();

        Assert.Empty(callout.Examine(Context(
            RivalSystemState(),
            priming: true,
            events: [Line("""{"timestamp":"3311-01-01T00:00:30Z","event":"FSSSignalDiscovered","SystemAddress":1,"SignalName":"T2F-81Z","SignalType":"FleetCarrier"}""")])));

        Assert.Single(Tick(callout, 60,
            """{"timestamp":"3311-01-01T00:01:00Z","event":"SupercruiseDestinationDrop","Type":"T2F-81Z"}""",
            """{"timestamp":"3311-01-01T00:01:02Z","event":"SupercruiseExit","StarSystem":"Deciat","SystemAddress":1,"BodyType":"Star","Body":"Deciat"}"""));
    }

    [Theory]
    [InlineData("Coriolis", true)]
    [InlineData("Orbis", true)]
    [InlineData("Ocellus", true)]
    [InlineData("Outpost", true)]
    [InlineData("AsteroidBase", true)]
    [InlineData("Dodec", true)]
    [InlineData("MegaShip", true)]
    [InlineData("FleetCarrier", true)]
    [InlineData("CraterOutpost", false)]
    [InlineData("CraterPort", false)]
    [InlineData("OnFootSettlement", false)]
    [InlineData("SpaceConstructionDepot", false)]
    [InlineData("PlanetaryConstructionDepot", false)]
    [InlineData("SurfaceStation", false)]
    public void UndockingSpeaksFromAnOrbitalStationOrCarrierOnly(string stationType, bool speaks)
    {
        var callout = new RivalTerritoryCallout();

        Assert.Empty(Tick(callout, 0,
            $$"""{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StationName":"Somewhere","StationType":"{{stationType}}","StarSystem":"Deciat","SystemAddress":1}"""));

        var spoken = Tick(callout, 600,
            """{"timestamp":"3311-01-01T00:10:00Z","event":"Undocked","StationName":"Somewhere"}""");

        Assert.Equal(speaks, spoken.Count == 1);
    }

    [Fact]
    public void UndockingFromAStrongholdCarrierSpeaks()
    {
        var callout = new RivalTerritoryCallout();

        Assert.Empty(Tick(callout, 0,
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StationName":"Stronghold Carrier","StationType":"SurfaceStation","StarSystem":"Deciat","SystemAddress":1}"""));

        Assert.Single(Tick(callout, 600,
            """{"timestamp":"3311-01-01T00:10:00Z","event":"Undocked","StationName":"Stronghold Carrier"}"""));
    }

    [Fact]
    public void LiftoffAndAGlideToAPlanetaryPortAreSilent()
    {
        Assert.Empty(Tick(new RivalTerritoryCallout(), 0,
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Liftoff","PlayerControlled":true}""",
            """{"timestamp":"3311-01-01T00:02:30Z","event":"SupercruiseDestinationDrop","Type":"Some Port"}""",
            """{"timestamp":"3311-01-01T00:03:00Z","event":"ApproachSettlement","Name":"Some Port"}"""));
    }

    [Fact]
    public void ASecondStationArrivalCarriesTheSameKeyAndCooldown()
    {
        var callout = new RivalTerritoryCallout();

        var first = Assert.Single(Tick(callout, 0, StationExitLine));
        var second = Assert.Single(Tick(callout, 300, StationExitLine));

        Assert.Equal(first.Key, second.Key);
        Assert.Equal(TimeSpan.FromMinutes(15), second.Cooldown);
    }

    // ---- The cues -----------------------------------------------------------------------

    [Fact]
    public void EveryAlertCueShipsWithAClipOfItsOwn()
    {
        var library = CueLibrary.Load();

        var clips = Enum.GetValues<AlertCue>().Select(library.For).ToList();

        // One per member, and none of them the same file twice — a warning whose cue is another warning's cue
        // is a warning the Commander cannot tell apart, which is the reason for there being four.
        Assert.Equal(Enum.GetValues<AlertCue>().Length, clips.Distinct().Count());

        foreach (var clip in clips)
        {
            Assert.Equal(AudioFormat.Standard, clip.Format);
        }
    }
}
