using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The carrier is called by its name, in the 62% of sessions where Elite never says what that is
/// (#130). Reported as <em>"Docking granted, Commander Deparagon. Welcome home to BNH-T2F"</em>.
/// <para>
/// <b>Not a wording bug.</b> All five surfaces that name the carrier already prefer the name and
/// fall back to the callsign — the two callouts, the adventure brief, <c>get_fleet</c> and the
/// <c>Situation</c> block the model reads. They said the callsign because <c>CarrierState.Name</c>
/// was null, and <c>CarrierStats</c> is its only source: 34 corpus journals dock at BNH-T2F and
/// only 13 contain one anywhere.
/// </para>
/// </summary>
public class TheCarrierIsCalledByItsNameTests
{
    private const long Id = 3715429376;
    private const string Sign = "BNH-T2F";
    private const string Called = "Sacred Fire";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// A carrier whose callsign is vouched by id, the way the airlock fix established, and with no
    /// <c>CarrierStats</c> anywhere — which is the session this issue is about.
    /// </summary>
    private static CarrierState Known()
    {
        var state = CarrierState.None;

        state = state.Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:00:00Z","event":"CarrierLocation","CarrierID":{{Id}},"StarSystem":"Deciat"}"""));

        state = state.Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:01:00Z","event":"Docked","StationName":"{{Sign}}","StationType":"FleetCarrier","MarketID":{{Id}}}"""));

        Assert.Equal(Sign, state.CallSign);
        Assert.Null(state.Name);

        return state;
    }

    /// <summary>
    /// The safe primary: it carries a <c>MarketID</c>, so the name is taken by id rather than by
    /// the shape of the string.
    /// </summary>
    [Fact]
    public void DroppingAtItByIdLearnsTheName()
    {
        var state = Known().Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:02:00Z","event":"SupercruiseDestinationDrop","Type":"{{Called}} {{Sign}}","MarketID":{{Id}}}"""));

        Assert.Equal(Called, state.Name);
    }

    /// <summary>
    /// The same event for somebody else's carrier teaches nothing. This is the assertion #28 asks
    /// for: the Commander has a squadron carrier in these same journals and it must never be
    /// mistaken for their own.
    /// </summary>
    [Fact]
    public void ADropAtAnotherCarrierTeachesNothing()
    {
        var state = Known().Apply(Event(
            """{"timestamp":"2026-08-27T20:02:00Z","event":"SupercruiseDestinationDrop","Type":"Iron Duke XYZ-99Z","MarketID":9999999999}"""));

        Assert.Null(state.Name);
    }

    /// <summary>
    /// The secondary, which matters because these arrive on approach, before the dock. It carries
    /// no id at all, so what makes it safe is that the callsign it ends with was itself learned by
    /// id.
    /// </summary>
    [Fact]
    public void ChatterEndingInTheVouchedCallsignLearnsTheName()
    {
        var state = Known().Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:02:00Z","event":"ReceiveText","From":"{{Called}} {{Sign}}","Channel":"npc","Message":"$Docking_Granted;"}"""));

        Assert.Equal(Called, state.Name);
    }

    /// <summary>
    /// A stranger's carrier on the same channel yields nothing, because its string does not end in
    /// this carrier's callsign. Without this the 244 <c>ReceiveText</c> occurrences would be a way
    /// to name the Commander's carrier after somebody else's.
    /// </summary>
    [Fact]
    public void ChatterFromAnotherCarrierTeachesNothing()
    {
        var state = Known().Apply(Event(
            """{"timestamp":"2026-08-27T20:02:00Z","event":"ReceiveText","From":"Iron Duke XYZ-99Z","Channel":"npc","Message":"hello"}"""));

        Assert.Null(state.Name);
    }

    /// <summary>
    /// <b>Nothing is parsed speculatively.</b> With no callsign vouched yet there is nothing to
    /// strip, so a decorated string teaches nothing at all — the same rule that leaves the 19 corpus
    /// journals which dock before any carrier event silent.
    /// </summary>
    [Fact]
    public void WithNoCallsignKnownNothingIsGuessed()
    {
        var state = CarrierState.None.Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:02:00Z","event":"ReceiveText","From":"{{Called}} {{Sign}}","Channel":"npc","Message":"hello"}"""));

        Assert.Null(state.Name);
    }

    /// <summary>
    /// <c>CarrierStats</c> stays the authority: Frontier said the name outright, so a name it
    /// supplies overrides a derived one. The Commander renaming their carrier is the case that
    /// makes this matter rather than a nicety.
    /// </summary>
    [Fact]
    public void CarrierStatsStillWins()
    {
        var derived = Known().Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:02:00Z","event":"ReceiveText","From":"Old Name {{Sign}}","Channel":"npc","Message":"hello"}"""));

        Assert.Equal("Old Name", derived.Name);

        var stated = derived.Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:03:00Z","event":"CarrierStats","CarrierID":{{Id}},"CarrierType":"FleetCarrier","Callsign":"{{Sign}}","Name":"{{Called}}"}"""));

        Assert.Equal(Called, stated.Name);
    }

    /// <summary>
    /// The separating space is required, so a callsign that is a suffix of a longer token cannot
    /// strip one. Cheap to get wrong with <c>EndsWith</c> alone and silent when it is.
    /// </summary>
    [Fact]
    public void AnUnseparatedSuffixIsNotACallsign()
    {
        var state = Known().Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:02:00Z","event":"ReceiveText","From":"Sacred Fire X{{Sign}}","Channel":"npc","Message":"hello"}"""));

        Assert.Null(state.Name);
    }

    /// <summary>
    /// A string that is only the callsign leaves nothing behind, and an empty name is worse than
    /// none: every surface would then print nothing where it used to print the callsign.
    /// </summary>
    [Fact]
    public void ACallsignAloneIsNotAName()
    {
        var state = Known().Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:02:00Z","event":"ReceiveText","From":"{{Sign}}","Channel":"npc","Message":"hello"}"""));

        Assert.Null(state.Name);
    }

    /// <summary>
    /// <b>The event that carries it most often, and the reason it is read at all.</b> Measured on
    /// the Commander's corpus: of the 27 journals naming BNH-T2F with no <c>CarrierStats</c> in
    /// them, <b>22 name it in an <c>FSSSignalDiscovered</c></b>, against 14 apiece for the other
    /// two. It carries no id, so it rests on exactly the same vouched-callsign rule.
    /// </summary>
    [Fact]
    public void TheSystemScanLearnsTheName()
    {
        var state = Known().Apply(Event(
            $$"""{"timestamp":"2026-08-27T20:02:00Z","event":"FSSSignalDiscovered","SignalName":"{{Called}} {{Sign}}","IsStation":true}"""));

        Assert.Equal(Called, state.Name);
    }

    /// <summary>A scan of a stranger's carrier teaches nothing, for the same reason.</summary>
    [Fact]
    public void AScanOfAnotherCarrierTeachesNothing()
    {
        var state = Known().Apply(Event(
            """{"timestamp":"2026-08-27T20:02:00Z","event":"FSSSignalDiscovered","SignalName":"Iron Duke XYZ-99Z","IsStation":true}"""));

        Assert.Null(state.Name);
    }
}
