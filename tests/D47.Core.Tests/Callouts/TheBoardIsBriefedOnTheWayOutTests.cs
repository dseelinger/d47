using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class TheBoardIsBriefedOnTheWayOutTests
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

    private static JournalEvent Accept(long id, string title, TimeSpan left) =>
        Event("MissionAccepted",
            ("MissionID", id),
            ("Name", "Mission_Courier"),
            ("LocalisedName", title),
            ("DestinationSystem", "Wadjuk"),
            ("DestinationStation", "Wadjuk Port"),
            ("Expiry", (Start + left).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

    private static readonly JournalEvent Dock = Event("Docked", ("StarSystem", "Sol"), ("StationName", "Jameson Memorial"));

    private static readonly JournalEvent Undock = Event("Undocked", ("StationName", "Jameson Memorial"));

    private static List<Announcement> Fly(MissionCallout callout, CommanderGameState state, params JournalEvent[] events)
    {
        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return [.. callout.Examine(new CalloutContext(Start, false, state, GameStatus.Unknown, NavRoute.None, events))];
    }

    private static CommanderGameState Commander() => new(new CommanderIdentity("F1", "Fixture"));

    [Fact]
    public void TwoAcceptsAndAnUndockNameTheSoonestMission()
    {
        var said = Fly(new MissionCallout(), Commander(),
            Dock,
            Accept(1, "Deliver Slowly", TimeSpan.FromHours(30)),
            Accept(2, "Deliver Polymers", TimeSpan.FromHours(13)),
            Undock);

        var slate = Assert.Single(said, a => a.Key == MissionCallout.SlateKey);
        Assert.Equal("Two missions on the board. The tightest is Deliver Polymers, to Wadjuk Port, 13 hours left.", slate.Text);
    }

    [Fact]
    public void OneMissionIsSaidAfterAColon()
    {
        var said = Fly(new MissionCallout(), Commander(), Dock, Accept(1, "Deliver Polymers", TimeSpan.FromHours(13)), Undock);

        Assert.Equal(
            "One mission on the board: Deliver Polymers, to Wadjuk Port, 13 hours left.",
            Assert.Single(said, a => a.Key == MissionCallout.SlateKey).Text);
    }

    [Fact]
    public void AnUndockWithNoAcceptSaysNothing()
    {
        var state = Commander();
        Fly(new MissionCallout(), state, Dock, Accept(1, "Deliver Polymers", TimeSpan.FromHours(13)));

        var callout = new MissionCallout();
        Assert.DoesNotContain(Fly(callout, state, Undock, Dock, Undock), a => a.Key == MissionCallout.SlateKey);
    }

    [Fact]
    public void AnExpiredMissionIsNeverTheOneNamed()
    {
        var said = Fly(new MissionCallout(), Commander(),
            Dock,
            Accept(1, "Too Late", TimeSpan.FromHours(-1)),
            Accept(2, "Deliver Polymers", TimeSpan.FromHours(13)),
            Undock);

        var slate = Assert.Single(said, a => a.Key == MissionCallout.SlateKey);
        Assert.Contains("Deliver Polymers", slate.Text);
        Assert.DoesNotContain("Too Late", slate.Text);
    }
}
