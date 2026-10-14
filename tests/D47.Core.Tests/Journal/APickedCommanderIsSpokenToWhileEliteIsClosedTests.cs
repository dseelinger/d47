using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>With Elite closed or nobody logged in nobody is flying, so a picked Commander is spoken to; the game's live files stay the last Commander's.</summary>
public class APickedCommanderIsSpokenToWhileEliteIsClosedTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static readonly JournalEvent AliceLogsIn =
        Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Alice"}""");

    private static readonly JournalEvent GameCloses =
        Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"Shutdown"}""");

    private static readonly JournalEvent BobLogsIn =
        Event("""{"timestamp":"2026-01-02T00:00:00Z","event":"LoadGame","FID":"F2","Commander":"Bob"}""");

    private static GameStateStore Played(params JournalEvent[] events)
    {
        var store = new GameStateStore();

        foreach (var journalEvent in events)
        {
            store.Apply(journalEvent, null, priming: true);
        }

        return store;
    }

    [Fact]
    public void APickWhileAnotherFliesIsSilenced()
    {
        var store = Played(AliceLogsIn);

        store.Pick(new CommanderIdentity("F2", "Bob"));

        Assert.True(store.IsOffDuty);
        Assert.True(store.IsAnotherFlying);
    }

    [Fact]
    public void APickAfterTheGameClosedIsNotSilenced()
    {
        var store = Played(AliceLogsIn, GameCloses);

        store.Pick(new CommanderIdentity("F2", "Bob"));

        Assert.False(store.IsAnotherFlying);
        Assert.False(CalloutContext.For(Start, false, store, GameStatus.Unknown, new NavRoute(), []).IsPriming);
    }

    [Fact]
    public void APickAfterTheGameCrashedIsNotSilenced()
    {
        var store = Played(AliceLogsIn);

        store.Pick(new CommanderIdentity("F2", "Bob"));
        store.GameRunning(false);

        Assert.False(store.IsAnotherFlying);
    }

    [Fact]
    public void TheClosedGamesLiveFilesStayTheLastCommanders()
    {
        var store = Played(AliceLogsIn, GameCloses);

        store.Pick(new CommanderIdentity("F2", "Bob"));

        Assert.True(store.IsOffDuty);
        Assert.Equal("F1", store.InGame!.Identity.FrontierId);
    }

    [Fact]
    public void AnotherCommanderLoggingInIsShownInPlaceOfThePick()
    {
        var store = Played(AliceLogsIn, GameCloses);
        store.Pick(new CommanderIdentity("F1", "Alice"));

        store.Apply(BobLogsIn);

        Assert.Equal("F2", store.Active!.Identity.FrontierId);
        Assert.False(store.IsAnotherFlying);
    }

    [Fact]
    public void APickBeforeAnybodyLogsInIsNotSilenced()
    {
        var store = new GameStateStore();

        store.Pick(new CommanderIdentity("F2", "Bob"));

        Assert.True(store.IsOffDuty);
        Assert.False(store.IsAnotherFlying);
        Assert.False(CalloutContext.For(Start, false, store, GameStatus.Unknown, new NavRoute(), []).IsPriming);
    }
}
