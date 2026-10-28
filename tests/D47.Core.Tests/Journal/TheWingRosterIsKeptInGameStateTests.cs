using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheWingRosterIsKeptInGameStateTests
{
    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2026-10-20T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static readonly string Commander = Line("17:00:00", "Commander", "\"FID\":\"F1\", \"Name\":\"Fixture\"");

    private static CommanderGameState StateAfter(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[] { Commander }.Concat(lines))
        {
            store.Apply(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException(line));
        }

        return store.Active!;
    }

    private static string Join(string time, params string[] others) =>
        Line(time, "WingJoin", "\"Others\":[" + string.Join(",", others.Select(other => $"\"{other}\"")) + "]");

    private static string Add(string time, string name) => Line(time, "WingAdd", $"\"Name\":\"{name}\"");

    private static string[] Names(CommanderGameState state) => [.. state.Wing.Members.Select(member => member.Name)];

    [Fact]
    public void AJoinSetsTheRosterToTheMembersAlreadyThere()
    {
        var state = StateAfter(Add("18:00:00", "Old"), Join("18:01:00", "Arcturus", "Vega"));

        Assert.Equal(["Arcturus", "Vega"], Names(state));
        Assert.All(state.Wing.Members, member => Assert.Null(member.LastHeard));
    }

    [Fact]
    public void AnAddJoinsOnceWhateverTheCaseAndNeverTheCommander()
    {
        var state = StateAfter(Join("18:00:00", "Arcturus"), Add("18:01:00", "ARCTURUS"), Add("18:01:05", "Vega"), Add("18:01:10", "Fixture"));

        Assert.Equal(["Arcturus", "Vega"], Names(state));
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:01:05Z"), state.Wing.Members[1].JoinedAt);
    }

    [Fact]
    public void AReloadRefillsTheRosterFromTheAddsWithNoJoin()
    {
        var state = StateAfter(
            Join("18:00:00", "Arcturus"),
            Line("18:10:00", "WingLeave"),
            Line("18:10:05", "LoadGame", "\"FID\":\"F1\", \"Commander\":\"Fixture\""),
            Add("18:10:30", "Arcturus"),
            Add("18:10:31", "Vega"));

        Assert.Equal(["Arcturus", "Vega"], Names(state));
    }

    [Fact]
    public void AWingLeaveEmptiesTheRoster()
    {
        var state = StateAfter(Join("18:00:00", "Arcturus", "Vega"), Line("18:30:00", "WingLeave"));

        Assert.True(state.Wing.IsEmpty);
    }

    [Fact]
    public void AMemberIsHeardFromTheirWingAndPlayerMessages()
    {
        var state = StateAfter(
            Join("18:00:00", "Arcturus", "Vega"),
            Line("18:05:00", "ReceiveText", "\"From\":\"CMDR Arcturus\", \"Channel\":\"wing\", \"Message\":\"hi\""),
            Line("18:06:00", "ReceiveText", "\"From\":\"Vega\", \"Channel\":\"player\", \"Message\":\"hi\""),
            Line("18:07:00", "ReceiveText", "\"From\":\"Vega\", \"Channel\":\"local\", \"Message\":\"hi\""));

        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:05:00Z"), state.Wing.Members[0].LastHeard);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:06:00Z"), state.Wing.Members[1].LastHeard);
    }

    [Fact]
    public void AMemberIsHeardWhenTheirShipIsTargeted()
    {
        var state = StateAfter(
            Join("18:00:00", "Arcturus"),
            Line("18:09:00", "ShipTargeted", "\"TargetLocked\":true, \"PilotName\":\"$cmdr_decorate:#name=Arcturus;\""),
            Line("18:09:30", "ShipTargeted", "\"TargetLocked\":true, \"PilotName\":\"$cmdr_decorate:#name=Stranger;\""));

        Assert.Equal(["Arcturus"], Names(state));
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:09:00Z"), state.Wing.Members[0].LastHeard);
    }

    [Fact]
    public void ASituationLineNamesTheWingAndSaysLeavingIsNotRecorded()
    {
        var state = StateAfter(
            Join("18:00:00", "Arcturus", "Vega"),
            Line("18:38:00", "ReceiveText", "\"From\":\"Arcturus\", \"Channel\":\"wing\", \"Message\":\"hi\""));

        var situation = Situation.Describe(state, now: DateTimeOffset.Parse("2026-10-20T18:40:00Z"));

        Assert.Contains(
            "Wing: Arcturus (heard 2 minutes ago), Vega (joined 40 minutes ago, not heard since). Elite records members joining, not leaving.",
            situation);
    }

    [Fact]
    public void AnEmptyWingAddsNoSituationLine()
    {
        var state = StateAfter(Join("18:00:00", "Arcturus"), Line("18:30:00", "WingLeave"));

        Assert.DoesNotContain("Wing:", Situation.Describe(state, now: DateTimeOffset.Parse("2026-10-20T18:40:00Z")) ?? "");
    }
}
