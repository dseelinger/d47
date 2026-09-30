using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ATripThatOutlastsTheExpiryIsFlaggedTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeGalaxy(double? distance, Task? hold = null) : IGalaxyService
    {
        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            hold is null ? Task.FromResult(distance) : hold.ContinueWith(_ => distance, cancellationToken);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?> { ["timestamp"] = "2026-09-30T12:00:00Z", ["event"] = kind };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Accept(TimeSpan left) =>
        Event("MissionAccepted",
            ("MissionID", 7L),
            ("Name", "Mission_Courier"),
            ("LocalisedName", "Courier 7"),
            ("DestinationSystem", "Wadjuk"),
            ("Expiry", (Start + left).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

    private static CommanderGameState Commander(JournalEvent accept)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(Event("Loadout", ("Ship", "python"), ("ShipID", 1), ("MaxJumpRange", 10.0)));
        state.Apply(Event("Location", ("StarSystem", "Sol")));
        state.Apply(accept);
        return state;
    }

    private static (MissionCallout Callout, List<Func<Task>> Dispatched) Build(double? distance, Task? hold = null, bool searchOn = true)
    {
        var galaxy = new FakeGalaxy(distance, hold);
        var dispatched = new List<Func<Task>>();
        return (new MissionCallout { Galaxy = () => searchOn ? galaxy : null, Dispatch = dispatched.Add }, dispatched);
    }

    private static List<Announcement> Tick(MissionCallout callout, CommanderGameState state, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(Start, false, state, GameStatus.Unknown, NavRoute.None, events))];

    private static async Task<List<Announcement>> Accepted(double? distance, TimeSpan left, bool searchOn = true)
    {
        var accept = Accept(left);
        var state = Commander(accept);
        var (callout, dispatched) = Build(distance, searchOn: searchOn);

        Assert.Empty(Tick(callout, state, accept));

        foreach (var work in dispatched)
        {
            await work();
        }

        return Tick(callout, state);
    }

    [Fact]
    public async Task ATripLongerThanTheTimeLeftSaysTheJumpsAndTheDeadline()
    {
        var said = await Accepted(400, TimeSpan.FromHours(2));

        Assert.Equal("Wadjuk is about 40 jumps. That's tight for a 2-hour deadline.", Assert.Single(said).Text);
    }

    [Fact]
    public async Task ATripThatFitsSaysNothing()
    {
        Assert.Empty(await Accepted(400, TimeSpan.FromHours(12)));
    }

    [Fact]
    public async Task ADestinationWhosePositionIsUnknownSaysNothing()
    {
        Assert.Empty(await Accepted(null, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task WithGalaxySearchOffNothingIsAsked()
    {
        Assert.Empty(await Accepted(400, TimeSpan.FromHours(2), searchOn: false));
    }

    [Fact]
    public void ATimePerJumpIsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), MissionCallout.TimePerJump);
    }

    [Fact]
    public async Task ASlowLookupDoesNotHoldUpTheTick()
    {
        var accept = Accept(TimeSpan.FromHours(2));
        var state = Commander(accept);
        var gate = new TaskCompletionSource();
        var (callout, dispatched) = Build(400, gate.Task);

        var started = System.Diagnostics.Stopwatch.StartNew();
        Assert.Empty(Tick(callout, state, accept));
        var work = Assert.Single(dispatched);
        var running = work();

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));
        Assert.False(running.IsCompleted);
        Assert.Empty(Tick(callout, state));

        gate.SetResult();
        await running;
        Assert.Single(Tick(callout, state));
    }
}
