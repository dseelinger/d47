using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A picked Commander stays shown until another pick or a login by a different Commander (#891).</summary>
public class APickHoldsUntilAnotherCommanderLogsInTests
{
    private static readonly CommanderIdentity Bob = new("F2", "Bob");

    private static readonly CommanderIdentity Alice = new("F1", "Alice");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static GameStateStore AliceInGame()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Alpha"}"""));
        return store;
    }

    [Fact]
    public void TheJournalKeepsFoldingIntoTheCommanderInTheGame()
    {
        var store = AliceInGame();

        store.Pick(Bob);
        store.Apply(Event("""{"timestamp":"2026-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Gamma"}"""));

        Assert.Equal("Bob", store.Active!.Identity.Name);
        Assert.Equal("Alice", store.InGame!.Identity.Name);
        Assert.Equal("Gamma", store.InGame!.Location.StarSystem);
        Assert.Null(store.Active!.Location.StarSystem);
    }

    [Fact]
    public void ADifferentCommanderLoggingInReplacesThePick()
    {
        var store = AliceInGame();
        store.Pick(Bob);

        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"LoadGame","FID":"F3","Commander":"Carol"}"""));

        Assert.Equal("Carol", store.Active!.Identity.Name);
        Assert.Equal("Carol", store.InGame!.Identity.Name);
        Assert.False(store.IsOffDuty);
    }

    [Fact]
    public void ALoginThatReplacesAPickSaysItCameFromTheJournalOnce()
    {
        var store = AliceInGame();
        store.Pick(Bob);

        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"Commander","FID":"F3","Name":"Carol"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:01Z","event":"LoadGame","FID":"F3","Commander":"Carol"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T02:00:00Z","event":"LoadGame","FID":"F3","Commander":"Carol"}"""));

        var change = Assert.Single(raised);
        Assert.Equal(CommanderSwitchCause.Journal, change.Cause);
        Assert.Equal("Bob", change.Previous!.Name);
        Assert.Equal("Carol", change.Current.Name);
    }

    [Fact]
    public void TheCommanderInTheGameLoggingInAgainReplacesThePick()
    {
        var store = AliceInGame();
        store.Pick(Bob);

        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"LoadGame","FID":"F1","Commander":"Alice"}"""));

        Assert.Equal("Alice", store.Active!.Identity.Name);
        Assert.False(store.IsOffDuty);
    }

    [Fact]
    public void PickingTheCommanderInTheGameRaisesNothing()
    {
        var store = AliceInGame();
        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Pick(Alice);

        Assert.Empty(raised);
        Assert.Equal("Alice", store.Active!.Identity.Name);
    }

    [Fact]
    public void PickingBackTheCommanderInTheGameLeavesTheirNextLoginNothingToChange()
    {
        var store = AliceInGame();
        store.Pick(Bob);
        store.Pick(Alice);

        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"LoadGame","FID":"F1","Commander":"Alice"}"""));

        Assert.Empty(raised);
        Assert.Equal("Alice", store.Active!.Identity.Name);
        Assert.False(store.IsOffDuty);
    }

    [Fact]
    public void APickSaysItWasPicked()
    {
        var store = AliceInGame();
        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Pick(Bob);

        var change = Assert.Single(raised);
        Assert.Equal(CommanderSwitchCause.Picked, change.Cause);
        Assert.Equal("Alice", change.Previous!.Name);
        Assert.Equal("Bob", change.Current.Name);
        Assert.False(change.Priming);
    }

    [Fact]
    public void ALoginBeforeAnyPickSaysItCameFromTheJournal()
    {
        var store = new GameStateStore();
        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Alice"}"""));

        Assert.Equal(CommanderSwitchCause.Journal, Assert.Single(raised).Cause);
    }

    [Fact]
    public void OffDutyIsTheShownCommanderNotBeingTheOneInTheGame()
    {
        var store = AliceInGame();
        Assert.False(store.IsOffDuty);

        store.Pick(Bob);
        Assert.True(store.IsOffDuty);

        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"LoadGame","FID":"F2","Commander":"Bob"}"""));
        Assert.False(store.IsOffDuty);

        store.Pick(new CommanderIdentity("F3", "Carol"));
        Assert.True(store.IsOffDuty);
    }

    [Fact]
    public void APickBeforeTheJournalNamesAnybodyIsOffDuty()
    {
        var store = new GameStateStore();
        Assert.False(store.IsOffDuty);

        store.Pick(Bob);

        Assert.True(store.IsOffDuty);
        Assert.Null(store.InGame);
    }

    [Fact]
    public void TheInGameCommandersJumpsRaiseNoSystemChangeWhileOffDuty()
    {
        var store = AliceInGame();
        store.Pick(Bob);

        var systemChanges = 0;
        store.SystemChanged += () => systemChanges++;

        store.Apply(Event("""{"timestamp":"2026-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Gamma"}"""));

        Assert.Equal(0, systemChanges);
    }

    [Fact]
    public void PickingACommanderStandingElsewhereIsASystemChange()
    {
        var store = AliceInGame();
        var systemChanges = 0;
        store.SystemChanged += () => systemChanges++;

        store.Pick(Bob);

        Assert.Equal(1, systemChanges);
    }

    [Fact]
    public void PickingAnotherCommanderAgainMovesTheShownOne()
    {
        var store = AliceInGame();
        store.Pick(Bob);

        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Pick(Alice);

        Assert.Equal("Alice", store.Active!.Identity.Name);
        Assert.Equal("Bob", Assert.Single(raised).Previous!.Name);
        Assert.False(store.IsOffDuty);
    }
}
