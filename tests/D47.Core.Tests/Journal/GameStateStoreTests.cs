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
}
