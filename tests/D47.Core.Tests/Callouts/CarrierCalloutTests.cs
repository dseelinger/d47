using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The Commander's fleet carrier, answering for itself (list.md Phase 11, "Carrier Captain").
/// </summary>
public class CarrierCalloutTests
{
    private const string CallSign = "K7Q-B4X";

    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-02-10T09:00:00Z",
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// A Commander who owns one. Ownership is established by events only an owner receives, so
    /// it is folded in rather than asserted.
    /// </summary>
    private static CommanderGameState WithCarrier()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        state.Apply(Event("CarrierStats",
            ("Callsign", CallSign),
            ("Name", "Long Way Home"),
            ("CarrierID", 3700000000L),
            ("FuelLevel", 900)));

        return state;
    }

    private static CalloutContext Context(CommanderGameState? state, bool priming, params JournalEvent[] events) =>
        new(DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, events);

    /// <summary>
    /// Dropping out of supercruise at the Commander's own carrier is an exchange between two
    /// people, in that order (asked for 2026-08-21).
    /// </summary>
    [Fact]
    public void DroppingAtYourOwnCarrierIsATowerAndCaptainExchange()
    {
        var callout = new CarrierCallout();

        var said = callout.Examine(Context(
            WithCarrier(),
            priming: false,
            Event("SupercruiseDestinationDrop", ("Type", $"Long Way Home {CallSign}"), ("Threat", 0))))
            .ToList();

        Assert.Equal(2, said.Count);

        // The tower speaks first, and to the captain rather than to the Commander — which is the
        // whole of what makes it an exchange rather than a greeting said twice.
        Assert.Equal(CarrierCallout.InboundKey, said[0].Key);
        Assert.Equal(VoiceRole.TowerControl, said[0].Voice);
        Assert.Contains("Captain,", said[0].Text, StringComparison.Ordinal);
        Assert.Contains("inbound", said[0].Text, StringComparison.Ordinal);

        Assert.Equal(CarrierCallout.WelcomeKey, said[1].Key);
        Assert.Equal(VoiceRole.CarrierCaptain, said[1].Voice);
        Assert.Contains("Tower Control", said[1].Text, StringComparison.Ordinal);
        Assert.Contains("Welcome home, Commander Fixture", said[1].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Somebody else's carrier gets nothing.</b> The corpus's 804 carrier drops include other
    /// people's, and a welcome home from a stranger's crew is worse than silence.
    /// </summary>
    [Fact]
    public void DroppingAtSomebodyElsesCarrierIsSilent()
    {
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(Context(
            WithCarrier(),
            priming: false,
            Event("SupercruiseDestinationDrop", ("Type", "ETERNAL FLAME BNH-T2F"), ("Threat", 0)))));
    }

    /// <summary>
    /// And an ordinary station drop is not a carrier at all. Most of the corpus's 3,819 drops are
    /// this, so a target that merely fails to match must cost nothing.
    /// </summary>
    [Fact]
    public void DroppingAtAStationIsSilent()
    {
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(Context(
            WithCarrier(),
            priming: false,
            Event("SupercruiseDestinationDrop", ("Type", "Evans Port"), ("Threat", 0)))));
    }

    /// <summary>
    /// Priming replays what already happened, so it must not greet the Commander for an arrival
    /// that is minutes or hours old.
    /// </summary>
    [Fact]
    public void PrimingSaysNothingAboutAnArrival()
    {
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(Context(
            WithCarrier(),
            priming: true,
            Event("SupercruiseDestinationDrop", ("Type", $"Long Way Home {CallSign}"), ("Threat", 0)))));
    }

    [Fact]
    public void ACommanderWithNoCarrierHearsNothing()
    {
        // It never guesses. "No carrier seen" is the honest state and produces silence rather
        // than a captain talking about a ship that does not exist.
        var callout = new CarrierCallout();
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        Assert.Empty(callout.Examine(Context(state, priming: false, Event("Docked", ("StationName", "Jameson Memorial")))));
    }

    [Fact]
    public void DockingAtYourOwnCarrierIsTheTowerSpeaking()
    {
        var callout = new CarrierCallout();

        var spoken = callout
            .Examine(Context(WithCarrier(), priming: false, Event("Docked", ("StationName", CallSign))))
            .ToArray();

        var arrival = Assert.Single(spoken);
        Assert.Equal(CarrierCallout.ArrivalKey, arrival.Key);
        Assert.Equal(VoiceRole.TowerControl, arrival.Voice);

        // The name the Commander gave it, not the callsign, when there is one. Both come from
        // the journal and neither is invented.
        Assert.Contains("Long Way Home", arrival.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void DockingAnywhereElseIsNotTheCarriersBusiness()
    {
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(
            Context(WithCarrier(), priming: false, Event("Docked", ("StationName", "Jameson Memorial")))));
    }

    [Fact]
    public void APlottedCarrierJumpIsTheCaptainRatherThanTheTower()
    {
        // Two people. The tower handles the Commander arriving; the captain speaks about the
        // carrier itself moving.
        var callout = new CarrierCallout();

        var spoken = callout
            .Examine(Context(WithCarrier(), priming: false, Event("CarrierJumpRequest", ("SystemName", "Colonia"))))
            .ToArray();

        var jump = Assert.Single(spoken);
        Assert.Equal(VoiceRole.CarrierCaptain, jump.Voice);
        Assert.Contains("Colonia", jump.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSpokenFromTheBacklog()
    {
        // Starting d47 after docking should not produce a welcome for a docking that happened
        // an hour ago.
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(
            Context(WithCarrier(), priming: true, Event("Docked", ("StationName", CallSign)))));
    }

    [Fact]
    public void LeavingTheCarrierIsAcknowledgedEvenThoughUndockedNamesNoStation()
    {
        // Elite's Undocked does not always carry the station name, so the callout remembers
        // where it last saw the Commander dock. Without that, departure is silent.
        var callout = new CarrierCallout();

        callout.Examine(Context(WithCarrier(), priming: false, Event("Docked", ("StationName", CallSign)))).ToArray();

        var spoken = callout
            .Examine(Context(WithCarrier(), priming: false, Event("Undocked")))
            .ToArray();

        var departure = Assert.Single(spoken);
        Assert.Equal(CarrierCallout.DepartureKey, departure.Key);
        Assert.Equal(VoiceRole.TowerControl, departure.Voice);
    }

    /// <summary>
    /// The carrier's crew address the person who owns it by name
    /// (remediation.md 9, "the Carrier Captain and Control Tower should give the Commander the
    /// respect he deserves as carrier owner").
    /// <para>
    /// They are the Commander's own crew on the Commander's own ship. "Welcome back, Commander"
    /// is how a stranger at a starport talks.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTowerAndTheCaptainNameTheOwner()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var docked = Event("Docked", ("StationName", CallSign));
        state.Apply(docked);

        var welcome = Assert.Single(callout.Examine(Context(state, priming: false, docked)));
        Assert.Contains("Commander Fixture", welcome.Text, StringComparison.Ordinal);

        var undocked = Event("Undocked", ("StationName", CallSign));
        state.Apply(undocked);

        var farewell = Assert.Single(callout.Examine(Context(state, priming: false, undocked)));
        Assert.Contains("Commander Fixture", farewell.Text, StringComparison.Ordinal);

        var jump = Event("CarrierJumpRequest", ("SystemName", "Deciat"));
        state.Apply(jump);

        var plotted = Assert.Single(callout.Examine(Context(state, priming: false, jump)));
        Assert.Contains("Commander Fixture", plotted.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// And with no name to use it is the bare rank rather than an invented one. The journal
    /// header is the only source, and a crew calling their owner by a guessed name is worse than
    /// one calling them by their rank.
    /// </summary>
    [Fact]
    public void WithNoNameItIsStillTheRank()
    {
        var callout = new CarrierCallout();
        var state = new CommanderGameState(new CommanderIdentity("F1", string.Empty));

        state.Apply(Event("CarrierStats",
            ("Callsign", CallSign),
            ("Name", "Long Way Home"),
            ("CarrierID", 3700000000L),
            ("FuelLevel", 900)));

        var docked = Event("Docked", ("StationName", CallSign));
        state.Apply(docked);

        var welcome = Assert.Single(callout.Examine(Context(state, priming: false, docked)));

        Assert.Contains("Commander", welcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Commander ,", welcome.Text, StringComparison.Ordinal);
    }
}
