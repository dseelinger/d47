using System.Globalization;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The spoken mission board: which three are named, in what order, and what is said of the rest.</summary>
public sealed class TheMissionBoardIsReadAloudTests
{
    private static readonly DateTimeOffset Now = At("2026-09-30T10:00:00Z");

    [Fact]
    public void FiveMissionsNameThreeInOrderAndCountTheOtherTwo()
    {
        var state = Board(
            Accept(1, "Six Hours Out", "Far Dock", "2026-09-30T16:00:00Z", 1000),
            Accept(2, "Half Hour", "Near Dock", "2026-09-30T10:30:00Z", 2000),
            Accept(3, "Handed In Here", "Home Port", "2026-09-30T18:00:00Z", 3000),
            Accept(4, "Two Hours", "Mid Dock", "2026-09-30T12:00:00Z", 4000),
            Accept(5, "Next Day", "Late Dock", "2026-10-01T10:00:00Z", 5000));

        var said = MissionsCapability.Describe(state, Now);

        var order = new[] { "Half Hour", "Handed In Here", "Two Hours" }.Select(name => said.IndexOf(name, StringComparison.Ordinal)).ToList();

        Assert.All(order, index => Assert.True(index >= 0, said));
        Assert.Equal(order.Order().ToList(), order);
        Assert.Contains("And two more.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Six Hours Out", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Next Day", said, StringComparison.Ordinal);
        Assert.Contains("15,000 credits in rewards", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissionHandedInHereRanksAboveOneExpiringInSixHours()
    {
        var state = Board(
            Accept(1, "Six Hours Out", "Far Dock", "2026-09-30T16:00:00Z", 1000),
            Accept(2, "Handed In Here", "Home Port", "2026-09-30T20:00:00Z", 1000));

        var said = MissionsCapability.Describe(state, Now);

        Assert.True(
            said.IndexOf("Handed In Here", StringComparison.Ordinal) < said.IndexOf("Six Hours Out", StringComparison.Ordinal),
            said);
    }

    [Fact]
    public void AMissionExpiringWithinTheHourRanksAboveOneHandedInHere()
    {
        var state = Board(
            Accept(1, "Handed In Here", "Home Port", "2026-09-30T13:00:00Z", 1000),
            Accept(2, "Half Hour", "Near Dock", "2026-09-30T10:30:00Z", 1000));

        var said = MissionsCapability.Describe(state, Now);

        Assert.True(
            said.IndexOf("Half Hour", StringComparison.Ordinal) < said.IndexOf("Handed In Here", StringComparison.Ordinal),
            said);
    }

    [Fact]
    public void EachNamedMissionGivesItsDestinationAndTimeLeft()
    {
        var said = MissionsCapability.Describe(
            Board(Accept(1, "Half Hour", "Near Dock", "2026-09-30T10:30:00Z", 2000)),
            Now);

        Assert.Equal(
            "You have one mission. First, Half Hour, to Near Dock, Near System, 30 minutes left. 2,000 credits in rewards.",
            said);
    }

    [Fact]
    public void AnEmptyBoardSaysSo()
    {
        Assert.Equal(
            "There are no missions on your board.",
            MissionsCapability.Describe(Board(), Now));
    }

    [Fact]
    public void AMissionWithNoDetailIsNamedAndNothingMore()
    {
        var store = new GameStateStore();
        store.Apply(Event(LoadGame));
        store.Apply(Event(
            """{ "timestamp":"2026-09-30T09:59:00Z", "event":"Missions", "Active":[ { "MissionID":9, "Name":"Mission_Courier", "PassengerMission":false, "Expires":3600 } ], "Failed":[], "Complete":[] }"""));

        var said = MissionsCapability.Describe(store.Active, Now);

        Assert.Equal("You have one mission. First, Mission_Courier.", said);
    }

    [Fact]
    public void AskingWhatToTakeSaysOnceThatOffersAreNotInTheJournal()
    {
        var said = MissionsCapability.Describe(Board(), Now, offered: true);

        Assert.StartsWith("I only see missions after you accept them.", said, StringComparison.Ordinal);
        Assert.Equal(1, said.Split("I only see missions").Length - 1);
        Assert.DoesNotContain("I only see missions", MissionsCapability.Describe(Board(), Now), StringComparison.Ordinal);
    }

    private static CommanderGameState Board(params string[] accepts)
    {
        var store = new GameStateStore();
        store.Apply(Event(LoadGame));
        store.Apply(Event(
            """{ "timestamp":"2026-09-30T09:00:05Z", "event":"Location", "StarSystem":"Home System", "StationName":"Home Port", "Docked":true }"""));

        foreach (var line in accepts)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static string LoadGame =>
        """{ "timestamp":"2026-09-30T09:00:00Z", "event":"LoadGame", "FID":"F100", "Commander":"Fixture" }""";

    private static string Accept(long id, string title, string station, string expiry, long reward) =>
        $$"""{ "timestamp":"2026-09-30T09:30:00Z", "event":"MissionAccepted", "Faction":"Party of Yoru", "Name":"Mission_Courier", "LocalisedName":"{{title}}", "DestinationSystem":"{{station.Split(' ')[0]}} System", "DestinationStation":"{{station}}", "Expiry":"{{expiry}}", "Reward":{{reward}}, "MissionID":{{id}} }""";

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
        return journalEvent!;
    }

    private static DateTimeOffset At(string timestamp) =>
        DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture);
}
