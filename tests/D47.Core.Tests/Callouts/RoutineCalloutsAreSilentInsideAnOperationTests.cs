using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Callouts;

public class RoutineCalloutsAreSilentInsideAnOperationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-25T12:00:00Z");

    private const string Enter =
        """{"timestamp":"2026-10-25T11:00:00Z","event":"GameModeChange","GameMode":"Operation"}""";

    private const string Leave =
        """{"timestamp":"2026-10-25T11:30:00Z","event":"GameModeChange","GameMode":"MainGame"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState State(params string[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        foreach (var json in events)
        {
            state.Apply(Event(json));
        }

        return state;
    }

    private static CalloutContext At(DateTimeOffset now, CommanderGameState state, GameStatus? status = null, params JournalEvent[] events) =>
        new(now, false, state, status ?? GameStatus.Unknown, NavRoute.None, events);

    private sealed class Says(string id, params Announcement[] lines) : ICallout
    {
        public string Id => id;

        public int Examined { get; private set; }

        public IEnumerable<Announcement> Examine(CalloutContext context)
        {
            Examined++;
            return lines;
        }
    }

    private static List<Announcement> Offered(CalloutEngine engine, CalloutContext context)
    {
        engine.Tick(context);
        return [.. engine.Drain()];
    }

    [Fact]
    public void ARoutineLineIsDroppedAndAnUrgentOneAndARelayedMessageAreQueued()
    {
        var routine = new Says("exploration", new Announcement("scan", "A body."));
        var urgent = new Says("danger", new Announcement("heat", "Heat.", CalloutUrgency.Urgent));
        var messages = new Says(IncomingMessages.CalloutId, new Announcement("msg", "Hello."));
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(routine).Add(urgent).Add(messages);

        var said = Offered(engine, At(Now, State(Enter)));

        Assert.Equal(["heat", "msg"], said.Select(line => line.Key));
        Assert.Equal(1, routine.Examined);
    }

    [Fact]
    public void ARefusedLineStampsNoCooldownSoItIsSaidOnceTheOperationEnds()
    {
        var routine = new Says("exploration", new Announcement("scan", "A body.") { Cooldown = TimeSpan.FromHours(1) });
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(routine);
        var state = State(Enter);

        Assert.Empty(Offered(engine, At(Now, state)));

        state.Apply(Event(Leave));

        Assert.Equal("scan", Assert.Single(Offered(engine, At(Now.AddSeconds(1), state))).Key);
    }

    [Fact]
    public void AStoryClueDueInsideIsSaidOnTheFirstEligibleTickAfter()
    {
        var callout = new StoryClueCallout(new NearbyFight()) { Due = _ => new StoryClueDue("s", 0) };
        var docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip, ReadAt = Now };
        var state = State(Enter);

        Assert.Empty(callout.Examine(At(Now, state, docked)));

        state.Apply(Event(Leave));

        Assert.Single(callout.Examine(At(Now.AddSeconds(1), state, docked)));
    }

    [Fact]
    public void AnAdventureBeatReachedInsideIsKeptAndSaidAfterWithNoAcknowledgement()
    {
        var store = new AdventureStore(
            Path.Combine(MemoryInstall.FakeRoot, "adventures.json"),
            new MemoryFileSystem(),
            NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(Accepted));
        book.CatchUp([]);
        var callout = new AdventureCallout(book) { Settle = TimeSpan.FromSeconds(20) };
        var state = State(Enter);
        var first = WholeRoute(Accepted)[0];

        Assert.Empty(callout.Examine(At(first.Timestamp, state, null, first)));
        Assert.Empty(callout.Examine(At(first.Timestamp.AddSeconds(60), state)));

        state.Apply(Event(Leave));

        var said = callout.Examine(At(first.Timestamp.AddSeconds(90), state)).ToList();

        Assert.Equal("adventure.the-lantern-route.0", Assert.Single(said).Key);
    }
}
