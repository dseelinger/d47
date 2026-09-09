using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The Commander's fleet carrier, answering for itself.</summary>
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

    /// <summary>A Commander who owns one.</summary>
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
    /// Dropping out of supercruise at the Commander's own carrier is an exchange between two people, in
 /// that order.
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

        // The tower speaks first, and to the captain rather than to the Commander — which is the whole of
        // what makes it an exchange rather than a greeting said twice.
        Assert.Equal(CarrierCallout.InboundKey, said[0].Key);
        Assert.Equal(VoiceRole.TowerControl, said[0].Voice);
        Assert.Contains("Captain,", said[0].Text, StringComparison.Ordinal);
        Assert.Contains("inbound", said[0].Text, StringComparison.Ordinal);

        Assert.Equal(CarrierCallout.WelcomeKey, said[1].Key);
        Assert.Equal(VoiceRole.CarrierCaptain, said[1].Voice);
        Assert.Contains("Tower Control", said[1].Text, StringComparison.Ordinal);
        Assert.Contains("Welcome home, Commander Fixture", said[1].Text, StringComparison.Ordinal);
    }

    /// <summary>Somebody else's carrier gets nothing.</summary>
    [Fact]
    public void DroppingAtSomebodyElsesCarrierIsSilent()
    {
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(Context(
            WithCarrier(),
            priming: false,
            Event("SupercruiseDestinationDrop", ("Type", "ETERNAL FLAME BNH-T2F"), ("Threat", 0)))));
    }

    /// <summary>And an ordinary station drop is not a carrier at all.</summary>
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
    /// Priming replays what already happened, so it must not greet the Commander for an arrival that is
    /// minutes or hours old.
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
        // It never guesses. "No carrier seen" is the honest state and produces silence rather than a captain
        // talking about a ship that does not exist.
        var callout = new CarrierCallout();
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        Assert.Empty(callout.Examine(Context(state, priming: false, Event("Docked", ("StationName", "Jameson Memorial")))));
    }

    [Fact]
    public void DockingAtYourOwnCarrierIsTheTowerAndThenTheCaptain()
    {
        var callout = new CarrierCallout();

        var spoken = callout
            .Examine(Context(WithCarrier(), priming: false, Event("Docked", ("StationName", CallSign))))
            .ToArray();

 // Two people, two keys: the tower acknowledges the moment Docked actually is, and the captain
        // makes it a welcome.
        Assert.Equal(2, spoken.Length);
        Assert.Equal(CarrierCallout.SecuredKey, spoken[0].Key);
        Assert.Equal(VoiceRole.TowerControl, spoken[0].Voice);
        Assert.Equal(CarrierCallout.HomeKey, spoken[1].Key);
        Assert.Equal(VoiceRole.CarrierCaptain, spoken[1].Voice);

        // Docked is the ship down and made fast.
        Assert.DoesNotContain("docking granted", spoken[0].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secured", spoken[0].Text, StringComparison.OrdinalIgnoreCase);

        // The name the Commander gave it, not the callsign, when there is one.
        Assert.Contains("Long Way Home", spoken[0].Text, StringComparison.Ordinal);
    }

    /// <summary>The words wait for the ship to be down.</summary>
    [Fact]
    public void DockingGrantedItselfSaysNothing()
    {
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(Context(
            WithCarrier(),
            priming: false,
            Event("DockingGranted", ("StationName", CallSign)))));
    }

    /// <summary>The crew speak without the management panel ever being opened: the carrier's id is known before its name is, and ownership was made of the name.</summary>
    [Fact]
    public void TheCrewSpeakWithoutTheManagementPanelEverBeingOpened()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        // What the day this was reported actually contained: the carrier's position and id, and no stats
        // anywhere.
        state.Apply(Event("CarrierLocation",
            ("CarrierType", "FleetCarrier"),
            ("CarrierID", 3700000000L),
            ("StarSystem", "Laksak")));

        var callout = new CarrierCallout();

        var docked = Event(
            "Docked",
            ("StationName", CallSign),
            ("StationType", "FleetCarrier"),
            ("MarketID", 3700000000L));

        // State folds before the callouts examine, exactly as the tick does it — so the docking that teaches
        // d47 the callsign is the same docking the tower answers.
        state.Apply(docked);

        var spoken = callout.Examine(Context(state, priming: false, docked)).ToArray();

        Assert.Equal(2, spoken.Length);
        Assert.Equal(CarrierCallout.SecuredKey, spoken[0].Key);
        Assert.Equal(VoiceRole.TowerControl, spoken[0].Voice);

        // The callsign, because no CarrierStats ever named it.
        Assert.Contains(CallSign, spoken[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The id is what makes the name safe to take: another Commander's carrier writes its own callsign
    /// as a station name too, and a welcome home from somebody else's crew is worse than silence.
    /// </summary>
    [Fact]
    public void SomebodyElsesCarrierDoesNotTeachUsOurOwnName()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        state.Apply(Event("CarrierLocation",
            ("CarrierType", "FleetCarrier"),
            ("CarrierID", 3700000000L),
            ("StarSystem", "Laksak")));

        var theirs = Event(
            "Docked",
            ("StationName", "V2X-99Z"),
            ("StationType", "FleetCarrier"),
            ("MarketID", 3799999999L));

        state.Apply(theirs);

        Assert.False(state.Carrier.Owned);
        Assert.Empty(new CarrierCallout().Examine(Context(state, priming: false, theirs)));
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
        // Two people.
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
        // Starting d47 after docking should not produce a welcome for a docking that happened an hour ago.
        var callout = new CarrierCallout();

        Assert.Empty(callout.Examine(
            Context(WithCarrier(), priming: true, Event("Docked", ("StationName", CallSign)))));
    }

    [Fact]
    public void LeavingTheCarrierIsAcknowledgedEvenThoughUndockedNamesNoStation()
    {
        // Elite's Undocked does not always carry the station name, so the callout remembers where it last saw
        // the Commander dock.
        var callout = new CarrierCallout();

        callout.Examine(Context(WithCarrier(), priming: false, Event("Docked", ("StationName", CallSign)))).ToArray();

        var spoken = callout
            .Examine(Context(WithCarrier(), priming: false, Event("Undocked")))
            .ToArray();

        var departure = Assert.Single(spoken);
        Assert.Equal(CarrierCallout.DepartureKey, departure.Key);
        Assert.Equal(VoiceRole.TowerControl, departure.Voice);
    }

    /// <summary> The carrier's crew address the person who owns it by name. </summary>
    [Fact]
    public void TheTowerAndTheCaptainNameTheOwner()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var docked = Event("Docked", ("StationName", CallSign));
        state.Apply(docked);

        // Both halves of the docked exchange address the owner by name (#220 kept this).
        var docking = callout.Examine(Context(state, priming: false, docked)).ToArray();
        Assert.Equal(2, docking.Length);
        Assert.All(docking, line => Assert.Contains("Commander Fixture", line.Text, StringComparison.Ordinal));

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
 /// Rank and surname: the crew's owner is "Commander DeParagon", never "Commander John
    /// DeParagon" — rank plus full name is how a form letter talks.
    /// </summary>
    [Fact]
    public void TheCrewAddressTheOwnerBySurname()
    {
        var callout = new CarrierCallout();
        var state = new CommanderGameState(new CommanderIdentity("F1", "John DeParagon"));

        state.Apply(Event("CarrierStats",
            ("Callsign", CallSign),
            ("Name", "Long Way Home"),
            ("CarrierID", 3700000000L),
            ("FuelLevel", 900)));

        var docked = Event("Docked", ("StationName", CallSign));
        state.Apply(docked);

        var spoken = callout.Examine(Context(state, priming: false, docked)).ToArray();

        Assert.Equal(2, spoken.Length);
        Assert.All(spoken, line =>
        {
            Assert.Contains("Commander DeParagon", line.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("John", line.Text, StringComparison.Ordinal);
        });
    }

    /// <summary>And with no name to use it is the bare rank rather than an invented one.</summary>
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

        var spoken = callout.Examine(Context(state, priming: false, docked)).ToArray();

        Assert.Equal(2, spoken.Length);
        Assert.All(spoken, line =>
        {
            Assert.Contains("Commander", line.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Commander ,", line.Text, StringComparison.Ordinal);
        });
    }
}
