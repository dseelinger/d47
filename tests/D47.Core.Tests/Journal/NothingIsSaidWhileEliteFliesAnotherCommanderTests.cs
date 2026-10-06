using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>While a picked Commander is shown and Elite runs another, the journal moves state and says nothing (#893).</summary>
public class NothingIsSaidWhileEliteFliesAnotherCommanderTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly NavRoute Route = new()
    {
        Hops =
        [
            new RouteHop("Beta", "K") { Position = (0, 0, 0) },
            new RouteHop("Gamma", "K") { Position = (10, 0, 0) },
            new RouteHop("Delta", "K") { Position = (20, 0, 0) },
            new RouteHop("Epsilon", "K") { Position = (30, 0, 0) },
        ],
        ReadAt = Start,
    };

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static readonly JournalEvent[] TwoCommanders =
    [
        Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Alice"}"""),
        Event("""{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Alpha"}"""),
        Event("""{"timestamp":"2026-01-01T01:00:00Z","event":"LoadGame","FID":"F2","Commander":"Bob"}"""),
        Event("""{"timestamp":"2026-01-01T01:00:01Z","event":"Location","StarSystem":"Beta"}"""),
    ];

    private static readonly JournalEvent Jump =
        Event("""{"timestamp":"2026-01-01T01:05:00Z","event":"FSDJump","StarSystem":"Gamma","JumpDist":10}""");

    private static (GameStateStore Store, CalloutEngine Engine) Primed()
    {
        var store = new GameStateStore();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
        engine.Add(new RouteCallout { EveryNJumps = 1 });

        Tick(store, engine, TwoCommanders, isFirst: true);
        return (store, engine);
    }

    private static IReadOnlyList<Announcement> Tick(
        GameStateStore store,
        CalloutEngine engine,
        IReadOnlyList<JournalEvent> events,
        bool isFirst = false)
    {
        foreach (var journalEvent in events)
        {
            store.Apply(journalEvent, null, isFirst);
        }

        engine.Tick(CalloutContext.For(Start, isFirst, store, GameStatus.Unknown, Route, events));
        return engine.Drain();
    }

    [Fact]
    public void AJumpByTheCommanderInTheGameIsAnnouncedWhileTheyAreShown()
    {
        var (store, engine) = Primed();

        var said = Tick(store, engine, [Jump]);

        Assert.Contains(said, announcement => announcement.Key == "route.progress");
    }

    [Fact]
    public void AJumpByTheCommanderInTheGameIsNotAnnouncedWhileAnotherIsShown()
    {
        var (store, engine) = Primed();

        store.Pick(new CommanderIdentity("F1", "Alice"));
        var said = Tick(store, engine, [Jump]);

        Assert.Empty(said);
        Assert.Equal("Gamma", store.InGame!.Location.StarSystem);
        Assert.Equal("Alpha", store.Active!.Location.StarSystem);
    }

    [Fact]
    public void TheShownCommandersReactionsGetNoEventsWhileOffDuty()
    {
        var (store, _) = Primed();
        IReadOnlyList<JournalEvent> batch = [Jump];

        Assert.Same(batch, store.Shown(batch));

        store.Pick(new CommanderIdentity("F1", "Alice"));

        Assert.Empty(store.Shown(batch));
    }
}
