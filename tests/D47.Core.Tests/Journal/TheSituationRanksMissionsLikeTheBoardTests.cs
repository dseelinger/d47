using System.Globalization;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class TheSituationRanksMissionsLikeTheBoardTests
{
    private static readonly DateTimeOffset Now = At("2026-09-30T10:00:00Z");

    [Fact]
    public void TheDigestAndTheSpokenBoardNameTheSameThreeInTheSameOrder()
    {
        var state = Board(
            Accept(1, "Six Hours Out", "Far Dock", "2026-09-30T16:00:00Z"),
            Accept(2, "Half Hour", "Near Dock", "2026-09-30T10:30:00Z"),
            Accept(3, "Handed In Here", "Home Port", "2026-09-30T16:00:00Z"),
            Accept(4, "Forty Minutes", "Mid Dock", "2026-09-30T10:40:00Z"),
            Accept(5, "Fifty Minutes", "Late Dock", "2026-09-30T10:50:00Z"),
            Accept(6, "Past Expiry", "Old Dock", "2026-09-30T09:45:00Z"));

        var digest = Situation.Describe(state, now: Now)!;
        var spoken = MissionsCapability.Describe(state, Now);

        string[] titles = ["Half Hour", "Forty Minutes", "Fifty Minutes", "Handed In Here", "Six Hours Out", "Past Expiry"];

        Assert.Equal(["Half Hour", "Forty Minutes", "Fifty Minutes"], Order(digest, titles));
        Assert.Equal(["Half Hour", "Forty Minutes", "Fifty Minutes"], Order(spoken, titles));
    }

    [Fact]
    public void TheMissionHandedInHereIsNamedAheadOfOnesExpiringSooner()
    {
        var state = Board(
            Accept(1, "Four Hours", "Far Dock", "2026-09-30T14:00:00Z"),
            Accept(2, "Five Hours", "Mid Dock", "2026-09-30T15:00:00Z"),
            Accept(3, "Six Hours Out", "Late Dock", "2026-09-30T16:00:00Z"),
            Accept(4, "Handed In Here", "Home Port", "2026-09-30T16:00:00Z"));

        var digest = Situation.Describe(state, now: Now)!;

        Assert.Equal(
            ["Handed In Here", "Four Hours", "Five Hours"],
            Order(digest, ["Handed In Here", "Four Hours", "Five Hours", "Six Hours Out"]));
    }

    [Fact]
    public void AnExpiredMissionIsNamedAfterTheLiveOnesWhenTheBoardIsShort()
    {
        var state = Board(
            Accept(1, "Past Expiry", "Old Dock", "2026-09-30T09:45:00Z"),
            Accept(2, "Four Hours", "Far Dock", "2026-09-30T14:00:00Z"));

        var digest = Situation.Describe(state, now: Now)!;

        Assert.Contains("Missions: 2 active (1 past expiry)", digest, StringComparison.Ordinal);
        Assert.Equal(["Four Hours", "Past Expiry"], Order(digest, ["Four Hours", "Past Expiry"]));
        Assert.Contains("Mission: Past Expiry, to Old Dock, Old System, expired", digest, StringComparison.Ordinal);
    }

    [Fact]
    public void TwentyMissionsGiveTheCountLineAndThreeMissionLines()
    {
        var state = Board([.. Enumerable.Range(1, 20).Select(id => Accept(id, $"Job {id}", "Far Dock", $"2026-09-30T{10 + (id % 12):00}:30:00Z"))]);

        var lines = Situation.Describe(state, now: Now)!.Split('\n');

        Assert.Single(lines, line => line.StartsWith("Missions: 20 active", StringComparison.Ordinal));
        Assert.Equal(3, lines.Count(line => line.StartsWith("Mission: ", StringComparison.Ordinal)));
    }

    [Fact]
    public void ADeliveryCarriesItsProgressAndOthersDoNot()
    {
        var state = Board(
            Accept(1, "Haul", "Far Dock", "2026-09-30T14:00:00Z"),
            Accept(2, "Empty Haul", "Mid Dock", "2026-09-30T15:00:00Z"),
            Accept(3, "Courier", "Late Dock", "2026-09-30T16:00:00Z"),
            Depot(1, 12, 20),
            Depot(2, 0, 0));

        var lines = Situation.Describe(state, now: Now)!.Split('\n');

        Assert.Contains(lines, line => line.StartsWith("Mission: Haul, to Far Dock, Far System, 4h 0m left, 12 of 20 delivered", StringComparison.Ordinal));
        Assert.DoesNotContain("delivered", lines.Single(line => line.StartsWith("Mission: Empty Haul", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain("delivered", lines.Single(line => line.StartsWith("Mission: Courier", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    private static string[] Order(string text, string[] titles) =>
        [.. titles
            .Where(title => text.Contains(title, StringComparison.Ordinal))
            .OrderBy(title => text.IndexOf(title, StringComparison.Ordinal))];

    private static CommanderGameState Board(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{ "timestamp":"2026-09-30T09:00:00Z", "event":"LoadGame", "FID":"F100", "Commander":"Fixture" }"""));
        store.Apply(Event(
            """{ "timestamp":"2026-09-30T09:00:05Z", "event":"Location", "StarSystem":"Home System", "StationName":"Home Port", "Docked":true }"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static string Accept(long id, string title, string station, string expiry) =>
        $$"""{ "timestamp":"2026-09-30T09:30:00Z", "event":"MissionAccepted", "Faction":"Party of Yoru", "Name":"Mission_Courier", "LocalisedName":"{{title}}", "DestinationSystem":"{{station.Split(' ')[0]}} System", "DestinationStation":"{{station}}", "Expiry":"{{expiry}}", "Reward":1000, "MissionID":{{id}} }""";

    private static string Depot(long id, int delivered, int total) =>
        $$"""{ "timestamp":"2026-09-30T09:40:00Z", "event":"CargoDepot", "MissionID":{{id}}, "UpdateType":"Deliver", "CargoType":"Polymers", "ItemsCollected":0, "ItemsDelivered":{{delivered}}, "TotalItemsToDeliver":{{total}} }""";

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
        return journalEvent!;
    }

    private static DateTimeOffset At(string timestamp) =>
        DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture);
}
