using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AnOperationRunIsFoldedIntoGameStateTests
{
    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2026-10-20T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string Load(string time, long credits) =>
        Line(time, "LoadGame", $"\"FID\":\"F1\", \"Commander\":\"Fixture\", \"Credits\":{credits}");

    private static string Stats(string time, int held, int spent) =>
        Line(time, "Statistics", $"\"Bank_Account\":{{ \"MercCoins_Current\":{held}, \"MercCoins_Total_Spent\":{spent} }}");

    private static string Wing(string time, string name) => Line(time, "WingAdd", $"\"Name\":\"{name}\"");

    private static string Mode(string time, string mode) => Line(time, "GameModeChange", $"\"GameMode\":\"{mode}\"");

    private static readonly string Commander = Line("17:00:00", "Commander", "\"FID\":\"F1\", \"Name\":\"Fixture\"");

    private static OperationRuns RunsFrom(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var marked in OperationMarker.Mark(lines.Select(Parse)))
        {
            store.Apply(marked);
        }

        return store.Active!.Operations;
    }

    private static JournalEvent Parse(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException(json);

    private static string[] FinishedRun(long creditsAfter) =>
    [
        Commander,
        Stats("18:00:00", 10, 5),
        Load("18:00:05", 1_000),
        Mode("18:00:10", "Operation"),
        Wing("18:00:20", "Fixture"),
        Wing("18:00:21", "Alpha"),
        Wing("18:00:22", "Bravo"),
        Wing("18:00:23", "Alpha"),
        Line("18:00:30", "Missions", "\"Active\":[ { \"MissionID\":1, \"Name\":\"Mission_Fixture_Name\" } ]"),
        Line("18:10:00", "Bounty", "\"TotalReward\":100"),
        Line("18:11:00", "Bounty", "\"TotalReward\":100"),
        Line("18:11:30", "CollectItems", "\"Name\":\"x\", \"Count\":1"),
        Load("18:30:00", creditsAfter),
        Mode("18:30:05", "MainGame"),
        Stats("18:30:10", 30, 5),
    ];

    [Fact]
    public void AFinishedRunHoldsWhatHappenedInIt()
    {
        var operations = RunsFrom(FinishedRun(51_000));
        var run = operations.Last!;

        Assert.Null(operations.Open);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:00:05Z"), run.OpenedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:30:10Z"), run.ClosedAt);
        Assert.Equal("Mission_Fixture_Name", run.Name);
        Assert.Equal(["Alpha", "Bravo"], run.Squad);
        Assert.Equal(2, run.Kills);
        Assert.Equal(1, run.ItemsCollected);
        Assert.Equal(1_000, run.CreditsBefore);
        Assert.Equal(51_000, run.CreditsAfter);
        Assert.Equal(50_000, run.Reward);
        Assert.True(run.Finished);
        Assert.Equal(15, run.MercCoinsBefore);
        Assert.Equal(20, run.MercCoinsEarned);
    }

    [Fact]
    public void ARunLeftEarlyIsNotFinished()
    {
        var run = RunsFrom(FinishedRun(1_000)).Last!;

        Assert.Equal(0, run.Reward);
        Assert.False(run.Finished);
    }

    [Fact]
    public void ARunIsOpenWhileItsLinesArrive()
    {
        var operations = RunsFrom(Commander, Load("18:00:05", 1_000), Mode("18:00:10", "Operation"), Line("18:10:00", "Bounty", "\"TotalReward\":100"));

        Assert.Null(operations.Last);
        Assert.Equal(1, operations.Open!.Kills);
        Assert.Null(operations.Open.ClosedAt);
    }

    [Fact]
    public void ARunThatEndsOnShutdownIsClosedWithNoReward()
    {
        var operations = RunsFrom(
            Commander,
            Load("18:00:05", 1_000),
            Mode("18:00:10", "Operation"),
            Line("18:10:00", "Bounty", "\"TotalReward\":100"),
            Line("18:20:00", "Shutdown"));

        Assert.Null(operations.Open);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:20:00Z"), operations.Last!.ClosedAt);
        Assert.Null(operations.Last.Reward);
        Assert.False(operations.Last.Finished);
    }

    [Fact]
    public void ARunOnGame440OpensAtTheRunnerAndClosesOnTheNextLoadGame()
    {
        var operations = RunsFrom(
            Commander,
            Load("18:00:00", 1_000),
            Line("18:05:10", "Location", "\"Docked\":true, \"StationName\":\"$Operations_Runner_Name:#index=1;\", \"StarSystem\":\"Fixture\""),
            Line("18:10:00", "Bounty", "\"TotalReward\":100"),
            Load("18:40:00", 9_000));

        var run = operations.Last!;

        Assert.Null(operations.Open);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:05:10Z"), run.OpenedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T18:40:00Z"), run.ClosedAt);
        Assert.Equal(1_000, run.CreditsBefore);
        Assert.Equal(9_000, run.CreditsAfter);
        Assert.True(run.Finished);
        Assert.Null(run.MercCoinsEarned);
    }

    [Fact]
    public void AMissionListWithTwoActiveMissionsNamesNoRun()
    {
        var operations = RunsFrom(
            Commander,
            Load("18:00:05", 1_000),
            Mode("18:00:10", "Operation"),
            Line("18:00:20", "Missions", "\"Active\":[ { \"Name\":\"A\" }, { \"Name\":\"B\" } ]"));

        Assert.Null(operations.Open!.Name);
    }
}
