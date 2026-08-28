using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Docking chatter from the Commander's own carrier is in the tower's voice from the first line
/// (<a href="https://github.com/dseelinger/d47/issues/109">#109</a>).
/// <para>
/// <b>The matcher was never the fault.</b> <c>IsMyCarrier</c> is a <c>Contains</c> against the
/// callsign or the name, and <c>"Sacred Fire BNH-T2F"</c> contains <c>"BNH-T2F"</c>. It failed
/// because d47 did not yet know the callsign: <c>CarrierState</c> learned it at the airlock, and
/// the airlock is 47 seconds after the messages docking chatter consists of.
/// </para>
/// <para>
/// So this is <a href="https://github.com/dseelinger/d47/issues/28">#28</a> working correctly on a
/// late input, and the sibling of <a href="https://github.com/dseelinger/d47/issues/130">#130</a>,
/// which was the same fault one field over. Every existing test starts from a state that already
/// knows the callsign, which is how it survived both.
/// </para>
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
    /// One line of station traffic, in the shape Elite actually writes it: an unlocalised id, the
    /// prose beside it, and the decorated sender.
    /// </summary>
    private static string Chatter(string id, string said, string from = Shown) =>
        $$"""
          {"timestamp":"2026-08-27T21:42:27Z","event":"ReceiveText","From":"{{from}}",
           "Message":"{{id}}","Message_Localised":"{{said}}","Channel":"npc"}
          """.ReplaceLineEndings(" ");

    /// <summary>
    /// Folds a session's events in order and reports what voice each message would be spoken in.
    /// <para>
    /// <b>The three keys are re-read after every event</b>, which is exactly what <c>AppHost</c>
    /// does on every sample — so a message is judged against the state as it stood when that
    /// message arrived, and not against what the session eventually learned. Reading them once at
    /// the end would pass against the code this test exists to fail.
    /// </para>
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

    /// <summary>
    /// The reported session, event for event out of
    /// <c>Journal.2026-08-27T170817.01.log</c> — including the squadron carrier that lands six
    /// seconds after the Commander's own, because that is the thing which must not be matched.
    /// </summary>
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

    /// <summary>
    /// <b>All three, and the first one is the point.</b> The reported session had every one of these
    /// in <c>Mark - Casual, Relaxed and Light</c> — a Comms voice handed out per sender, not the
    /// cast tower voice.
    /// <para>
    /// The no-fire-zone line arrives before the docking request, so a fix that reached only the
    /// docking events would still fail here. That is deliberate: it is the assertion separating
    /// "learned earlier" from "learned early enough".
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <b>The identity still comes by id.</b> The supercruise drop is the earliest key there is, and
    /// it is only read when its MarketID is the carrier id the state already holds — so a drop at
    /// the squadron's carrier teaches nothing, and its traffic keeps the ordinary Comms voice.
    /// <para>
    /// This is the case <a href="https://github.com/dseelinger/d47/issues/28">#28</a> ruled must
    /// never match, and the Commander has one in these very journals.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <b>The display string is kept whole rather than filed as the name.</b> It is what the
    /// <c>From</c> field literally holds, which is what makes it match; the name is a different
    /// thing and <c>CarrierStats</c> is still its authority.
    /// </summary>
    [Fact]
    public void TheDropIsRememberedAsWrittenAndNotAsAName()
    {
        var state = CarrierState.None
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:09:18Z","event":"CarrierLocation","CarrierID":{{Mine}},"StarSystem":"Kuk"}"""))
            .Apply(Event($$"""{"timestamp":"2026-08-27T21:42:15Z","event":"SupercruiseDestinationDrop","Type":"{{Shown}}","MarketID":{{Mine}}}"""));

        Assert.Equal(Shown, state.DisplayName);

        // Nothing was guessed: with no callsign vouched yet there is nothing to strip, so the name
        // is still unknown and the callsign still unknown. Only the undivided string is held.
        Assert.Null(state.Name);
        Assert.Null(state.CallSign);
    }

    /// <summary>
    /// <b>The callsign is learned at the docking request, not at the dock.</b> Counted over the
    /// Commander's 935 journals: 859 <c>DockingRequested</c> and 857 <c>DockingGranted</c> at a
    /// fleet carrier, and every one of them carries <c>MarketID</c>, <c>StationName</c> and
    /// <c>StationType</c> together — everything <c>SaysMyCallsign</c> already tested for.
    /// <para>
    /// It matters on its own rather than only as a second road to the same place: the corpus has
    /// approaches with a docking request and no supercruise drop before it, where the Commander
    /// undocked and asked to come back without ever leaving the pad's vicinity.
    /// </para>
    /// </summary>
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
    /// And a docking request at somebody else's carrier teaches nothing, which is the same id check
    /// the airlock fix rests on rather than a new one.
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

    /// <summary>
    /// <b>An approach with no supercruise drop still reaches the tower</b>, one line later. The
    /// 00:16 approach of the reported session is this shape: undocked, turned round, and asked for a
    /// pad again without the drop that had identified the carrier an hour before.
    /// </summary>
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

    /// <summary>
    /// <b>A carrier d47 has not identified stays unidentified.</b> With no carrier event at all, the
    /// same traffic is a stranger's — which is what leaves the 19 corpus journals that dock before
    /// any carrier event silent, and is the rule rather than an edge of it.
    /// </summary>
    [Fact]
    public void WithNoCarrierKnownTheSameTrafficIsAStrangers()
    {
        var heard = Session(Chatter("$STATION_NoFireZone_entered;", "No fire zone entered."));

        Assert.Equal(VoiceRole.Comms, Assert.Single(heard).Voice);
    }
}
