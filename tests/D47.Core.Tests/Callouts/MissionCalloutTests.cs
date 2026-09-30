using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class MissionCalloutTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?> { ["timestamp"] = "2026-09-30T12:00:00Z", ["event"] = kind };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Accept(long id, string station, long reward, DateTimeOffset expiry) =>
        Event("MissionAccepted",
            ("MissionID", id),
            ("Name", "Mission_Courier"),
            ("LocalisedName", $"Courier {id}"),
            ("DestinationSystem", "Sol"),
            ("DestinationStation", station),
            ("Reward", reward),
            ("Expiry", expiry.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

    private static CommanderGameState Commander(params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static List<Announcement> Run(MissionCallout callout, CommanderGameState state, DateTimeOffset now, bool priming = false, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(now, priming, state, GameStatus.Unknown, NavRoute.None, events))];

    [Fact]
    public void DockingWhereTwoMissionsHandInSaysTheCountAndTheReward()
    {
        var state = Commander(
            Accept(1, "Jameson Memorial", 1_400_000, Start.AddDays(2)),
            Accept(2, "jameson memorial", 1_500_000, Start.AddDays(2)),
            Accept(3, "Elsewhere", 900_000, Start.AddDays(2)));

        var said = Run(new MissionCallout(), state, Start, false, Event("Docked", ("StationName", "Jameson Memorial")));

        Assert.Equal("Two missions conclude here. 2.9 million waiting.", Assert.Single(said).Text);
    }

    [Fact]
    public void DockingSomewhereNothingHandsInSaysNothing()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_400_000, Start.AddDays(2)));

        Assert.Empty(Run(new MissionCallout(), state, Start, false, Event("Docked", ("StationName", "Elsewhere"))));
    }

    [Fact]
    public void LeavingWithAHandInUnclaimedSaysSoUntilItIsCompleted()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_400_000, Start.AddDays(2)));
        var callout = new MissionCallout();

        var said = Run(callout, state, Start, false, Event("Undocked", ("StationName", "Jameson Memorial")));

        Assert.Equal("You're leaving with a hand-in unclaimed at Jameson Memorial.", Assert.Single(said).Text);

        state.Apply(Event("MissionCompleted", ("MissionID", 1L)));

        Assert.Empty(Run(callout, state, Start, false, Event("Undocked", ("StationName", "Jameson Memorial"))));
    }

    [Fact]
    public void EachExpiryWarningIsSpokenOnceAcrossAReplay()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_000_000, Start.AddHours(2)));
        var callout = new MissionCallout();
        var said = new List<string>();

        Run(callout, state, Start, true);

        for (var minute = 0; minute <= 125; minute++)
        {
            said.AddRange(Run(callout, state, Start.AddMinutes(minute)).Select(a => a.Text));
        }

        Assert.Equal(["Courier 1 expires in an hour.", "Courier 1 expires in ten minutes."], said);
    }

    [Fact]
    public void ARestartInsideTheHourDoesNotRepeatTheHourWarning()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_000_000, Start.AddMinutes(30)));
        var callout = new MissionCallout();

        Run(callout, state, Start, true);

        Assert.Empty(Run(callout, state, Start.AddSeconds(1)));
        Assert.Equal("Courier 1 expires in ten minutes.", Assert.Single(Run(callout, state, Start.AddMinutes(20))).Text);
    }

    [Fact]
    public void AMissionPastTheTenMinuteMarkByMoreThanAMinuteGetsNoWarningAtStart()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_000_000, Start.AddMinutes(8)));
        var callout = new MissionCallout();

        Run(callout, state, Start, true);

        Assert.Empty(Run(callout, state, Start.AddSeconds(1)));
    }

    [Fact]
    public void ARowTurnsMissionCalloutsOffAndDefaultsOn()
    {
        Assert.True(new CalloutSettings().Missions);
        Assert.Equal("missions", new MissionCallout().Id);
        Assert.Equal("callouts.missions", CalloutCapability.MissionsKey);
    }
}
