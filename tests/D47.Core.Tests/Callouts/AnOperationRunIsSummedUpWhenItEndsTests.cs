using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>An Operation run is summed up once, when it ends and its merc coins are known (#1795).</summary>
public class AnOperationRunIsSummedUpWhenItEndsTests
{
    private static readonly DateTimeOffset ClosedAt = DateTimeOffset.Parse("2026-10-20T18:30:10Z");

    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2026-10-20T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string Load(string time, long credits) =>
        Line(time, "LoadGame", $"\"FID\":\"F1\", \"Commander\":\"Fixture\", \"Credits\":{credits}");

    private static string Stats(string time, int held) =>
        Line(time, "Statistics", $"\"Bank_Account\":{{ \"MercCoins_Current\":{held}, \"MercCoins_Total_Spent\":5 }}");

    private static string Wing(string name) => Line("18:00:20", "WingAdd", $"\"Name\":\"{name}\"");

    private static string Mode(string time, string mode) => Line(time, "GameModeChange", $"\"GameMode\":\"{mode}\"");

    private static string Missions(string? name) =>
        Line("18:00:30", "Missions", name is null ? "\"Active\":[]" : $"\"Active\":[ {{ \"Name\":\"{name}\" }} ]");

    private static readonly string[] Preamble =
    [
        Line("17:00:00", "Commander", "\"FID\":\"F1\", \"Name\":\"Fixture\""),
        Stats("18:00:00", 10),
        Load("18:00:05", 1_000),
        Mode("18:00:10", "Operation"),
    ];

    private static CommanderGameState StateAfter(IEnumerable<string> lines)
    {
        var store = new GameStateStore();

        foreach (var marked in OperationMarker.Mark(Preamble.Concat(lines).Select(Parse)))
        {
            store.Apply(marked);
        }

        return store.Active!;
    }

    private static JournalEvent Parse(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException(json);

    private static string[] Ending(long credits) => [Load("18:30:00", credits), Mode("18:30:05", "MainGame")];

    private static CalloutContext At(CommanderGameState state, DateTimeOffset now, bool priming = false) =>
        new(now, priming, state, GameStatus.Unknown, NavRoute.None, []);

    private static string? Said(CommanderGameState state, DateTimeOffset? now = null) =>
        new OperationSummaryCallout().Examine(At(state, now ?? ClosedAt)).SingleOrDefault()?.Text;

    [Fact]
    public void AFinishedRunWithCoinsSaysEverything() =>
        Assert.Equal(
            "Terminal Prosecution finished in 30 minutes: 10 million credits and 20 merc coins. 2 kills. Flown with Alpha, Bravo and Charlie.",
            Said(StateAfter([
                Wing("Fixture"), Wing("Alpha"), Wing("Bravo"), Wing("Charlie"), Wing("Delta"), Missions("Terminal Prosecution"),
                Line("18:10:00", "Bounty"), Line("18:11:00", "Bounty"),
                .. Ending(10_001_000), Stats("18:30:10", 30)])));

    [Fact]
    public void ARunWaitsForItsCoinsThenGivesUpAfterSixtySeconds()
    {
        var state = StateAfter([Missions("Terminal Prosecution"), .. Ending(10_001_000), Line("18:30:10", "Music")]);

        Assert.Null(Said(state, ClosedAt + TimeSpan.FromSeconds(59)));
        Assert.Equal("Terminal Prosecution finished in 30 minutes: 10 million credits.", Said(state, ClosedAt + TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void ARunLeftEarlyIsUnfinished() =>
        Assert.Equal(
            "Left Reclamation Point after 30 minutes, unfinished. 1 kill.",
            Said(StateAfter([Missions("Reclamation Point"), Line("18:10:00", "Bounty"), .. Ending(1_000), Stats("18:30:10", 10)])));

    [Fact]
    public void ARunWithNoNameIsTheOperation() =>
        Assert.StartsWith("The Operation finished in", Said(StateAfter([.. Ending(51_000), Stats("18:30:10", 30)])));

    [Fact]
    public void ASoloRunWithNoKillsLeavesThoseOutAndSaysNoCoinsWhenNoneWereEarned() =>
        Assert.Equal(
            "The Operation finished in 30 minutes: 50,000 credits.",
            Said(StateAfter([.. Ending(51_000), Stats("18:30:10", 10)])));

    [Fact]
    public void ARunWithNoRewardSaysNeitherFinishedNorUnfinished()
    {
        var state = StateAfter([Line("18:20:00", "Shutdown"), Stats("18:30:10", 10)]);
        var said = Said(state, ClosedAt + TimeSpan.FromMinutes(5));

        Assert.NotNull(said);
        Assert.DoesNotContain("finished", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARunInTheBacklogIsNotRecapped()
    {
        var callout = new OperationSummaryCallout();
        var state = StateAfter([.. Ending(51_000), Stats("18:30:10", 30)]);

        Assert.Empty(callout.Examine(At(state, ClosedAt, priming: true)));
        Assert.Empty(callout.Examine(At(state, ClosedAt + TimeSpan.FromMinutes(5))));
    }

    [Fact]
    public void ARunIsSaidOnce()
    {
        var callout = new OperationSummaryCallout();
        var state = StateAfter([.. Ending(51_000), Stats("18:30:10", 30)]);

        Assert.Single(callout.Examine(At(state, ClosedAt)));
        Assert.Empty(callout.Examine(At(state, ClosedAt)));
    }
}
