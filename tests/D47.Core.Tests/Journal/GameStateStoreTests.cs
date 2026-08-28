using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class GameStateStoreTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void CommanderEventEstablishesIdentityAndBecomesActive()
    {
        var store = new GameStateStore();

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));

        Assert.NotNull(store.Active);
        Assert.Equal("F1", store.Active!.Identity.FrontierId);
        Assert.Equal("Alice", store.Active!.Identity.Name);
    }

    [Fact]
    public void LocationEventsUpdateTheActiveCommandersLocation()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Alpha","Docked":false}"""));

        Assert.Equal("Alpha", store.Active!.Location.StarSystem);
        Assert.False(store.Active!.Location.Docked);
    }

    [Fact]
    public void EventsBeforeAnyIdentityAreDroppedRatherThanGuessedAt()
    {
        var store = new GameStateStore();

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Location","StarSystem":"Alpha"}"""));

        Assert.Null(store.Active);
    }

    [Fact]
    public void UnrecognisedEventKindsAreInertRatherThanClearingState()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Alpha"}"""));

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:02Z","event":"SomeFutureEvent","x":1}"""));

        Assert.Equal("Alpha", store.Active!.Location.StarSystem);
    }

    [Fact]
    public void ASecondCommandersEventsNeverBlendIntoTheFirstOnesLocation()
    {
        var store = new GameStateStore();

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Alpha"}"""));

        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"Commander","FID":"F2","Name":"Bob"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:01Z","event":"Location","StarSystem":"Beta"}"""));

        Assert.Equal(2, store.All.Count);
        Assert.Equal("Bob", store.Active!.Identity.Name);
        Assert.Equal("Beta", store.Active!.Location.StarSystem);

        var alice = store.All.Single(c => c.Identity.FrontierId == "F1");
        Assert.Equal("Alpha", alice.Location.StarSystem); // untouched by Bob's Location event
    }

    [Fact]
    public void DockedAndUndockedTrackStationState()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));

        store.Apply(Event(
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Docked","StationName":"Outpost","StarSystem":"Alpha"}"""));
        Assert.True(store.Active!.Location.Docked);
        Assert.Equal("Outpost", store.Active!.Location.StationName);

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:02Z","event":"Undocked","StationName":"Outpost"}"""));
        Assert.False(store.Active!.Location.Docked);
        Assert.Null(store.Active!.Location.StationName);
    }

    /// <summary>
    /// One switch signal (Phase 44). Nobody to somebody is an adoption; one to another is
    /// a switch; the same Commander again — the LoadGame that follows every Commander event, or
    /// a relog — is nothing.
    /// </summary>
    [Fact]
    public void TheCommanderChangingIsRaisedOnceWithWhoItWasAndWhoItIs()
    {
        var store = new GameStateStore();
        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:01Z","event":"LoadGame","FID":"F1","Commander":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:02Z","event":"Location","StarSystem":"Alpha"}"""));

        var adoption = Assert.Single(raised);
        Assert.True(adoption.IsAdoption);
        Assert.Null(adoption.Previous);
        Assert.Equal("Alice", adoption.Current.Name);
        Assert.False(adoption.Priming);

        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"Commander","FID":"F2","Name":"Bob"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:01Z","event":"LoadGame","FID":"F2","Commander":"Bob"}"""));

        Assert.Equal(2, raised.Count);
        var switched = raised[1];
        Assert.False(switched.IsAdoption);
        Assert.Equal("Alice", switched.Previous!.Name);
        Assert.Equal("Bob", switched.Current.Name);

        // Alice relogging is a switch back, and Bob again is not.
        store.Apply(Event("""{"timestamp":"2026-01-01T02:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        Assert.Equal(3, raised.Count);
        Assert.Equal("Bob", raised[2].Previous!.Name);
    }

    /// <summary>
    /// The trap. The backlog is folded through the same Apply, so a replay that crosses into
    /// somebody else's journal raises the signal too — and it says so, because every subscriber
    /// that discards anything must do nothing for one of these.
    /// </summary>
    [Fact]
    public void ASwitchMetDuringPrimingSaysSo()
    {
        var store = new GameStateStore();
        var raised = new List<CommanderSwitch>();
        store.CommanderChanged += raised.Add;

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""), null, priming: true);
        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"Commander","FID":"F2","Name":"Bob"}"""), null, priming: true);
        store.Apply(Event("""{"timestamp":"2026-01-01T02:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""), null, priming: false);

        Assert.Equal([true, true, false], raised.Select(change => change.Priming));

        // And the buckets are what they always were: the flag reaches the signal and nothing else.
        Assert.Equal(2, store.All.Count);
        Assert.Equal("Alice", store.Active!.Identity.Name);
    }

    /// <summary>
    /// Raised with the new Commander already active, so a subscriber can read who is flying from
    /// the store rather than only from the signal.
    /// </summary>
    [Fact]
    public void TheStoreAlreadyAnswersForTheNewCommanderWhenTheSignalIsRaised()
    {
        var store = new GameStateStore();
        string? activeWhenRaised = null;
        store.CommanderChanged += _ => activeWhenRaised = store.Active?.Identity.Name;

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"Commander","FID":"F2","Name":"Bob"}"""));

        Assert.Equal("Bob", activeWhenRaised);
    }
}
