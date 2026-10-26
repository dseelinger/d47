using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AnOperationIsHeldUntilTheCommanderLeavesTests
{
    private const string LoadGame = """{ "timestamp":"2026-10-20T18:00:00Z", "event":"LoadGame", "Commander":"Tester", "Ship":"adder" }""";
    private const string SessionMainGame = """{ "timestamp":"2026-10-20T18:00:00Z", "event":"GameModeChange", "GameMode":"MainGame" }""";
    private const string Entered = """{ "timestamp":"2026-10-20T18:05:00Z", "event":"GameModeChange", "GameMode":"Operation" }""";
    private const string EnteredLoadGame = """{ "timestamp":"2026-10-20T18:05:00Z", "event":"LoadGame", "Commander":"Tester", "Ship":"adder" }""";
    private const string Left = """{ "timestamp":"2026-10-20T18:40:00Z", "event":"GameModeChange", "GameMode":"MainGame" }""";
    private const string Shutdown = """{ "timestamp":"2026-10-20T18:45:00Z", "event":"Shutdown" }""";

    private static readonly DateTimeOffset EnteredAt = DateTimeOffset.Parse("2026-10-20T18:05:00Z");

    private static CommanderGameState Fold(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    [Fact]
    public void EnteringAnOperationRecordsWhenItStarted()
    {
        Assert.Equal(EnteredAt, Fold(Entered).InOperationSince);
    }

    [Fact]
    public void LeavingItClearsTheTime()
    {
        Assert.Null(Fold(Entered, Left).InOperationSince);
    }

    [Fact]
    public void AnyOtherGameModeClearsTheTime()
    {
        const string Other = """{ "timestamp":"2026-10-20T18:10:00Z", "event":"GameModeChange", "GameMode":"Open" }""";

        Assert.Null(Fold(Entered, Other).InOperationSince);
    }

    [Fact]
    public void ALoadGameClearsTheTime()
    {
        Assert.Null(Fold(Entered, LoadGame).InOperationSince);
    }

    [Fact]
    public void AShutdownClearsTheTime()
    {
        Assert.Null(Fold(Entered, Shutdown).InOperationSince);
    }

    [Fact]
    public void ASessionStartLeavesItClear()
    {
        Assert.Null(Fold(LoadGame, SessionMainGame).InOperationSince);
    }

    [Fact]
    public void ALoadGameThenOperationAtTheSameTimeLeavesItSet()
    {
        Assert.Equal(EnteredAt, Fold(EnteredLoadGame, Entered).InOperationSince);
    }

    [Fact]
    public void ALoadGameThenMainGameAtTheSameTimeLeavesItClear()
    {
        const string LeftLoadGame = """{ "timestamp":"2026-10-20T18:40:00Z", "event":"LoadGame", "Commander":"Tester", "Ship":"adder" }""";

        Assert.Null(Fold(Entered, LeftLoadGame, Left).InOperationSince);
    }

    [Fact]
    public void AReceiptNamesTheChange()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));
        Assert.True(JournalEvent.TryParse(Entered, NullLogger.Instance, out var parsed));

        Assert.Equal("whether you are in an Operation", CommanderGameState.PhraseFor(nameof(CommanderGameState.InOperationSince)));
        Assert.NotEqual(FoldReceipt.Nothing, state.Apply(parsed!));
    }
}
