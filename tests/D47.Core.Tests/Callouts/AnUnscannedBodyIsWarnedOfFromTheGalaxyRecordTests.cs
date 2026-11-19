using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Journal;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>An approach to a body with no scan reads its gravity and heat from the galaxy record (#2594).</summary>
public class AnUnscannedBodyIsWarnedOfFromTheGalaxyRecordTests
{
    private const long SystemAddress = 1_000_100;
    private const int BodyId = 4;
    private const string BodyName = "Col 285 Sector AB-C d1-2 3 a";

    private const string Location = """
        {"timestamp":"3311-04-02T08:00:00Z","event":"Location","StarSystem":"Col 285 Sector AB-C d1-2","SystemAddress":1000100}
        """;

    private const string Approach = """
        {"timestamp":"3311-04-02T09:10:00Z","event":"ApproachBody","StarSystem":"Col 285 Sector AB-C d1-2",
         "SystemAddress":1000100,"Body":"Col 285 Sector AB-C d1-2 3 a","BodyID":4}
        """;

    private const string Scan = """
        {"timestamp":"3311-04-02T09:00:00Z","event":"Scan","ScanType":"Detailed","SystemAddress":1000100,
         "BodyID":4,"BodyName":"Col 285 Sector AB-C d1-2 3 a","Landable":true,"SurfaceGravity":5,"SurfaceTemperature":300}
        """;

    private sealed class FakeSystems(double gravity, double kelvin) : IStarSystemService
    {
        public int Calls { get; private set; }

        public Exception? Throws { get; init; }

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Calls++;
            if (Throws is { } ex)
            {
                throw ex;
            }

            return Task.FromResult<StarSystemProfile?>(new StarSystemProfile
            {
                Name = "Col 285 Sector AB-C d1-2",
                SystemAddress = systemAddress,
                Bodies =
                [
                    new BodyProfile { BodyId = BodyId, Name = BodyName, Type = "Planet", Gravity = gravity, SurfaceTemperature = kelvin },
                ],
            });
        }

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ListLogger : ILogger<ApproachHazardCallout>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }

    private static CommanderGameState Commander(params string[] events) => JournalScript.State(
    [
        """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
        Location,
        .. events,
    ]);

    private static ApproachHazardCallout Callout(IStarSystemService? systems, ILogger<ApproachHazardCallout>? logger = null) =>
        new(logger ?? new ListLogger()) { StarSystems = () => systems, Dispatch = work => work().GetAwaiter().GetResult() };

    private static List<string> Tick(ApproachHazardCallout callout, CommanderGameState state, params string[] events) =>
        [.. callout.Examine(new CalloutContext(
            DateTimeOffset.UnixEpoch, false, state, GameStatus.Unknown, NavRoute.None, [.. events.Select(JournalScript.Parse)]))
            .Select(a => a.Text)];

    [Fact]
    public async Task AnUnscannedHeavyBodyIsSaidOnTheTickAfterTheLookup()
    {
        var systems = new FakeSystems(1.3, 300);
        var callout = Callout(systems);
        var queued = new List<Func<Task>>();
        callout.Dispatch = queued.Add;
        var state = Commander(Approach);

        Assert.Empty(Tick(callout, state, Approach));
        await queued[0]();
        Assert.Equal([$"High gravity on {BodyName}: 1.30 g."], Tick(callout, state));
        Assert.Empty(Tick(callout, state));
    }

    [Fact]
    public async Task ARecordedHotBodyIsSaid()
    {
        var callout = Callout(new FakeSystems(0.5, 812));
        var queued = new List<Func<Task>>();
        callout.Dispatch = queued.Add;
        var state = Commander(Approach);

        Tick(callout, state, Approach);
        await queued[0]();
        Assert.Equal([$"Hot surface on {BodyName}: 812 K."], Tick(callout, state));
    }

    [Fact]
    public void AScannedBodyIsAnsweredFromTheScanAndTheRecordIsNotAsked()
    {
        var systems = new FakeSystems(1.3, 300);
        var callout = Callout(systems);

        Assert.Empty(Tick(callout, Commander(Scan, Approach), Approach));
        Assert.Equal(0, systems.Calls);
    }

    [Fact]
    public async Task ASecondApproachInTheSameSystemMakesNoSecondCall()
    {
        var systems = new FakeSystems(1.3, 300);
        var callout = Callout(systems);
        var queued = new List<Func<Task>>();
        callout.Dispatch = queued.Add;
        var state = Commander(Approach);

        Tick(callout, state, Approach);
        await queued[0]();
        Tick(callout, state);

        Assert.Equal([$"High gravity on {BodyName}: 1.30 g."], Tick(callout, state, Approach));
        Assert.Single(queued);
        Assert.Equal(1, systems.Calls);
    }

    [Fact]
    public async Task AnAnswerAfterTouchdownIsNotSaid()
    {
        var callout = Callout(new FakeSystems(1.3, 300));
        var queued = new List<Func<Task>>();
        callout.Dispatch = queued.Add;
        var state = Commander(Approach);
        const string touchdown = """{"timestamp":"3311-04-02T09:20:00Z","event":"Touchdown","PlayerControlled":true,"BodyID":4}""";

        Tick(callout, state, Approach);
        Tick(callout, state, touchdown);
        await queued[0]();

        Assert.Empty(Tick(callout, state));
    }

    [Fact]
    public async Task AnAnswerAfterLeavingTheSystemIsNotSaid()
    {
        var callout = Callout(new FakeSystems(1.3, 300));
        var queued = new List<Func<Task>>();
        callout.Dispatch = queued.Add;

        Tick(callout, Commander(Approach), Approach);
        await queued[0]();
        const string jump = """{"timestamp":"3311-04-02T09:30:00Z","event":"FSDJump","StarSystem":"Elsewhere","SystemAddress":2000200}""";

        Assert.Empty(Tick(callout, Commander(Approach, jump)));
    }

    [Fact]
    public void ALookupThatThrowsSaysNothingAndLogsAWarning()
    {
        var logger = new ListLogger();
        var callout = Callout(new FakeSystems(1.3, 300) { Throws = new HttpRequestException("down") }, logger);
        var state = Commander(Approach);

        Tick(callout, state, Approach);

        Assert.Empty(Tick(callout, state));
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    [Fact]
    public void WithoutAStarSystemServiceNothingIsSaid()
    {
        var callout = Callout(null);

        Assert.Empty(Tick(callout, Commander(Approach), Approach));
        Assert.Empty(Tick(callout, Commander(Approach)));
    }
}
