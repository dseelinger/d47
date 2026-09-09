using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Docking chatter from the Commander's own carrier is in the tower's voice from the first line.
/// </summary>
public class TheTowerSpeaksFromTheFirstLineTests
{
    private const long Mine = 3715429376;
    private const long Squadron = 3713474048;
    private const string Sign = "BNH-T2F";
    private const string Shown = "Sacred Fire " + Sign;

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// One line of station traffic, in the shape Elite actually writes it: an unlocalised id, the prose
    /// beside it, and the decorated sender.
    /// </summary>
    private static string Chatter(string id, string said, string from = Shown) =>
        $$"""
          {"timestamp":"2026-08-27T21:42:27Z","event":"ReceiveText","From":"{{from}}",
           "Message":"{{id}}","Message_Localised":"{{said}}","Channel":"npc"}
          """.ReplaceLineEndings(" ");

    /// <summary>
    /// Folds a session's events in order and reports what voice each message would be spoken in.
    /// </summary>
    private static List<(string Id, VoiceRole Voice)> Session(params string[] lines)
    {
        var carrier = CarrierState.None;
        var reader = new IncomingMessages { Enabled = () => true, IncludeNpcs = () => true };
        var heard = new List<(string, VoiceRole)>();

        foreach (var line in lines)
        {
            var journalEvent = Event(line);

            carrier = carrier.Apply(journalEvent);

            reader.CarrierName = carrier.Name;
            reader.CarrierCallSign = carrier.CallSign;
            reader.CarrierDisplayName = carrier.DisplayName;

            if (journalEvent.Kind == "ReceiveText" && reader.Read(journalEvent) is { } read)
            {
                heard.Add((journalEvent.String("Message")!, read.Voice));
            }
        }

        return heard;
    }

    /// <summary>Event for event out of <c>Journal.2026-08-27T170817.01.log</c> — including the squadron carrier
    /// that lands six seconds after the Commander's own, because that is the thing which must not be
    /// matched.</summary>
    private static readonly string[] TheReportedApproach =
    [
        $$"""{"timestamp":"2026-08-27T21:09:18Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":{{Mine}},"StarSystem":"Kuk"}""",
        $$"""{"timestamp":"2026-08-27T21:09:24Z","event":"CarrierLocation","CarrierType":"SquadronCarrier","CarrierID":{{Squadron}},"StarSystem":"Col 285 Sector GT-G c11-9"}""",
        $$"""{"timestamp":"2026-08-27T21:42:15Z","event":"SupercruiseDestinationDrop","Type":"{{Shown}}","Threat":0,"MarketID":{{Mine}}}""",
        Chatter("$STATION_NoFireZone_entered;", "No fire zone entered."),
        $$"""{"timestamp":"2026-08-27T21:42:31Z","event":"DockingRequested","MarketID":{{Mine}},"StationName":"{{Sign}}","StationType":"FleetCarrier"}""",
        Chatter("$DockingChatter_Neutral;", "Ensure to observe starport protocol during your visit, pilot."),
        Chatter("$STATION_docking_granted;", "Docking request granted."),
        $$"""{"timestamp":"2026-08-27T21:42:31Z","event":"DockingGranted","LandingPad":10,"MarketID":{{Mine}},"StationName":"{{Sign}}","StationType":"FleetCarrier"}""",
        $$"""{"timestamp":"2026-08-27T21:43:18Z","event":"Docked","StationName":"{{Sign}}","StationType":"FleetCarrier","MarketID":{{Mine}},"StarSystem":"Kuk"}""",
    ];

    /// <summary>All three, and the first one is the point.</summary>
    [Fact]
    public void EveryLineOfTheReportedApproachIsTheTowers()
    {
        var heard = Session(TheReportedApproach);

        Assert.Equal(3, heard.Count);
        Assert.All(heard, line => Assert.Equal(VoiceRole.TowerControl, line.Voice));

        // Named, so a failure says which line went wrong rather than only that one did.
        Assert.Equal("$STATION_NoFireZone_entered;", heard[0].Id);
        Assert.Equal(VoiceRole.TowerControl, heard[0].Voice);
    }

    /// <summary>The identity still comes by id.</summary>
    [Fact]
    public void ASquadronCarriersChatterIsNotTheTowers()
    {
        var heard = Session(
            $$"""{"timestamp":"2026-08-27T21:09:18Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":{{Mine}},"StarSystem":"Kuk"}""",
            $$"""{"timestamp":"2026-08-27T21:09:24Z","event":"CarrierLocation","CarrierType":"SquadronCarrier","CarrierID":{{Squadron}},"StarSystem":"Col 285"}""",
            $$"""{"timestamp":"2026-08-27T21:42:15Z","event":"SupercruiseDestinationDrop","Type":"Squadron Pride XYZ-99Z","Threat":0,"MarketID":{{Squadron}}}""",
            Chatter("$STATION_NoFireZone_entered;", "No fire zone entered.", from: "Squadron Pride XYZ-99Z"));

        Assert.Equal(VoiceRole.Comms, Assert.Single(heard).Voice);
    }

    /// <summary>The display string is kept whole rather than filed as the name.</summary>
    [Fact]
    public void TheDropIsRememberedAsWrittenAndNotAsAName()
    {
        var state = CarrierState.None
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:09:18Z","event":"CarrierLocation","CarrierID":{{Mine}},"StarSystem":"Kuk"}"""))
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:42:15Z","event":"SupercruiseDestinationDrop","Type":"{{Shown}}","MarketID":{{Mine}}}"""));

        Assert.Equal(Shown, state.DisplayName);

        // Nothing was guessed: with no callsign vouched yet there is nothing to strip, so the name is still
        // unknown and the callsign still unknown.
        Assert.Null(state.Name);
        Assert.Null(state.CallSign);
    }

    /// <summary>The callsign is learned at the docking request, not at the dock.</summary>
    [Theory]
    [InlineData("DockingRequested")]
    [InlineData("DockingGranted")]
    public void TheDockingEventsTeachTheCallsign(string kind)
    {
        var state = CarrierState.None
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:09:18Z","event":"CarrierLocation","CarrierID":{{Mine}},"StarSystem":"Kuk"}"""))
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:42:31Z","event":"{{kind}}","MarketID":{{Mine}},"StationName":"{{Sign}}","StationType":"FleetCarrier"}"""));

        Assert.Equal(Sign, state.CallSign);
    }

    /// <summary>
    /// And a docking request at somebody else's carrier teaches nothing, which is the same id check the
    /// airlock fix rests on rather than a new one.
    /// </summary>
    [Theory]
    [InlineData("DockingRequested")]
    [InlineData("DockingGranted")]
    public void ADockingRequestElsewhereTeachesNothing(string kind)
    {
        var state = CarrierState.None
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:09:18Z","event":"CarrierLocation","CarrierID":{{Mine}},"StarSystem":"Kuk"}"""))
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:42:31Z","event":"{{kind}}","MarketID":{{Squadron}},"StationName":"XYZ-99Z","StationType":"FleetCarrier"}"""));

        Assert.Null(state.CallSign);
    }

    /// <summary>An approach with no supercruise drop still reaches the tower, one line later.</summary>
    [Fact]
    public void AnApproachWithNoDropIsStillTheTowersFromTheRequest()
    {
        var heard = Session(
            $$"""{"timestamp":"2026-08-28T00:09:00Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":{{Mine}},"StarSystem":"Kuk"}""",
            $$"""{"timestamp":"2026-08-28T00:16:27Z","event":"DockingRequested","MarketID":{{Mine}},"StationName":"{{Sign}}","StationType":"FleetCarrier"}""",
            Chatter("$DockingChatter_Neutral;", "Ensure to observe starport protocol during your visit, pilot."),
            Chatter("$STATION_docking_granted;", "Docking request granted."));

        Assert.Equal(2, heard.Count);
        Assert.All(heard, line => Assert.Equal(VoiceRole.TowerControl, line.Voice));
    }

    /// <summary>A carrier d47 has not identified stays unidentified.</summary>
    [Fact]
    public void WithNoCarrierKnownTheSameTrafficIsAStrangers()
    {
        var heard = Session(Chatter("$STATION_NoFireZone_entered;", "No fire zone entered."));

        Assert.Equal(VoiceRole.Comms, Assert.Single(heard).Voice);
    }

    // ---- And the role has to reach a voice ------------------------------------------------

    /// <summary>The half of #109 that the identity fix alone did not deliver, found by flying it.</summary>
    [Theory]
    [InlineData(VoiceRole.TowerControl)]
    [InlineData(VoiceRole.CarrierCaptain)]
    public void ACastRoleKeepsItsVoiceEvenWithASenderOnTheLine(VoiceRole role)
    {
        var cast = new VoiceCast { Pool = ["pool-one", "pool-two", "pool-three"] };

        cast.Assign(role, "the-cast-voice");

        Assert.Equal("the-cast-voice", cast.ForSender(Shown, isPlayer: false, role).VoiceId);

        // And it does not drift: the same sender asked twice is still the cast voice rather than an
        // assignment made on the first call and kept.
        Assert.Equal("the-cast-voice", cast.ForSender(Shown, isPlayer: false, role).VoiceId);
    }

    /// <summary>
    /// And everybody else still gets a voice of their own, which is what stops this being a fix that
    /// collapses the whole cast onto one.
    /// </summary>
    [Fact]
    public void StrangersStillGetAVoiceEach()
    {
        var cast = new VoiceCast { Pool = ["pool-one", "pool-two", "pool-three"] };

        cast.Assign(VoiceRole.TowerControl, "the-cast-voice");

        var first = cast.ForSender("Iron Duke XYZ-99Z", isPlayer: false).VoiceId;
        var second = cast.ForSender("Kaiser Grendel", isPlayer: false).VoiceId;

        Assert.NotEqual(first, second);
        Assert.NotEqual("the-cast-voice", first);
        Assert.NotEqual("the-cast-voice", second);
    }

    /// <summary>An uncast role falls through to the pool: the rule is "cast", not "not Comms", so a Commander who has cleared the tower's voice gets a pool voice rather than silence.</summary>
    [Fact]
    public void AnUncastRoleStillDrawsFromThePool()
    {
        var cast = new VoiceCast { Pool = ["pool-one", "pool-two"] };

        Assert.Contains(
            cast.ForSender(Shown, isPlayer: false, VoiceRole.TowerControl).VoiceId,
            (string[])["pool-one", "pool-two"]);
    }
}
