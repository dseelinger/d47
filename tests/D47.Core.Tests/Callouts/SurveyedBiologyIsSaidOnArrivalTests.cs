using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>On arrival, the bodies Spansh has surveyed biology on that reach the threshold.</summary>
public class SurveyedBiologyIsSaidOnArrivalTests
{
    private const long Here = 1_000_200;
    private const long Elsewhere = 2_000_300;
    private const string System = "Fixture";

    private sealed class FakeGalaxy : IGalaxyService
    {
        public List<long> Asked { get; } = [];

        public Exception? Throws { get; init; }

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Asked.Add(systemAddress);

            if (Throws is not null)
            {
                return Task.FromException<SystemBiology>(Throws);
            }

            return Task.FromResult(systemAddress == Here
                ? new SystemBiology(
                    Here,
                    [Body("Fixture 3 c", 24, 12_000_000), Body("Fixture 4 a", 30, 2_000_000), Body("Fixture 3 b", 23, 15_000_000)])
                : new SystemBiology(systemAddress, []));
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private static SurveyedBody Body(string name, int bodyId, long value) => new(name, bodyId, value, []);
    }

    private static (SurveyedBiologyCallout Callout, FakeGalaxy Galaxy, List<Func<Task>> Dispatched) Build(
        bool galaxySearch = true,
        Exception? throws = null)
    {
        var galaxy = new FakeGalaxy { Throws = throws };
        var dispatched = new List<Func<Task>>();

        var callout = new SurveyedBiologyCallout(NullLogger<SurveyedBiologyCallout>.Instance)
        {
            Galaxy = () => galaxySearch ? galaxy : null,
            Dispatch = dispatched.Add,
        };

        return (callout, galaxy, dispatched);
    }

    private static async Task RunDispatched(List<Func<Task>> dispatched)
    {
        foreach (var work in dispatched)
        {
            await work();
        }

        dispatched.Clear();
    }

    private static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    private static CommanderGameState Commander(params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        return store.Active!;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Jump(long address, string system, string at = "10:00:00") => $$"""
        {"timestamp":"2026-08-16T{{at}}Z","event":"FSDJump","StarSystem":"{{system}}","SystemAddress":{{address}}}
        """;

    private static string Signals() => $$"""
        {"timestamp":"2026-08-16T10:01:00Z","event":"FSSBodySignals","BodyName":"Fixture 3 b",
         "SystemAddress":{{Here}},"BodyID":23,
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":2}]}
        """;

    private static string Scan() => $$"""
        {"timestamp":"2026-08-16T10:02:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"{{System}}",
         "SystemAddress":{{Here}},"BodyID":23,"BodyName":"Fixture 3 b",
         "PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere","Volcanism":"",
         "SurfaceGravity":{{0.5 * 9.80665}},"SurfaceTemperature":200,"SurfacePressure":{{0.02 * 101325}},
         "Landable":true}
        """;

    [Fact]
    public async Task AJumpAsksOnceAndSaysTheBodiesAtOrAboveTheThresholdHighestFirst()
    {
        var (callout, galaxy, dispatched) = Build();
        var jump = Jump(Here, System);
        var state = Commander(jump);

        Assert.Empty(callout.Examine(Context(state, false, jump)));
        await RunDispatched(dispatched);

        Assert.Equal(Here, Assert.Single(galaxy.Asked));

        var said = Assert.Single(callout.Examine(Context(state, false)));

        Assert.Equal("Spansh has surveyed biology here: 3 b at 15 million, 3 c at 12 million.", said.Text);
    }

    [Fact]
    public async Task NoBodyAtTheThresholdSaysNothing()
    {
        var (callout, _, dispatched) = Build();
        callout.Threshold = () => 20_000_000;
        var jump = Jump(Here, System);
        var state = Commander(jump);

        Assert.Empty(callout.Examine(Context(state, false, jump)));
        await RunDispatched(dispatched);

        Assert.Empty(callout.Examine(Context(state, false)));
    }

    [Fact]
    public void WithGalaxySearchOffNoCallIsMade()
    {
        var (callout, galaxy, dispatched) = Build(galaxySearch: false);
        var jump = Jump(Here, System);

        Assert.Empty(callout.Examine(Context(Commander(jump), false, jump)));

        Assert.Empty(dispatched);
        Assert.Empty(galaxy.Asked);
    }

    [Fact]
    public void WhilePrimingNoCallIsMade()
    {
        var (callout, _, dispatched) = Build();
        var jump = Jump(Here, System);

        Assert.Empty(callout.Examine(Context(Commander(jump), true, jump)));

        Assert.Empty(dispatched);
    }

    [Fact]
    public void WithTheToggleOffNoCallIsMade()
    {
        var (callout, _, dispatched) = Build();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(callout);
        var jump = Jump(Here, System);

        engine.SetEnabled("surveyed-biology", false);
        engine.Tick(Context(Commander(jump), false, jump));

        Assert.Empty(dispatched);
    }

    [Fact]
    public async Task AResultThatArrivesAfterASecondJumpIsNotSaid()
    {
        var (callout, _, dispatched) = Build();
        var first = Jump(Here, System);
        var second = Jump(Elsewhere, "Elsewhere", at: "10:01:00");

        Assert.Empty(callout.Examine(Context(Commander(first), false, first)));

        var moved = Commander(first, second);

        Assert.Empty(callout.Examine(Context(moved, false, second)));
        await RunDispatched(dispatched);

        Assert.Empty(callout.Examine(Context(moved, false)));
    }

    [Fact]
    public async Task AFailedLookupIsNotSpoken()
    {
        var (callout, _, dispatched) = Build(throws: new GalaxyUnavailableException("Spansh is down."));
        var jump = Jump(Here, System);
        var state = Commander(jump);

        Assert.Empty(callout.Examine(Context(state, false, jump)));
        await RunDispatched(dispatched);

        Assert.Empty(callout.Examine(Context(state, false)));
    }

    [Fact]
    public async Task ABodyNamedOnArrivalIsNotSaidAgainWhenScanned()
    {
        var (callout, _, dispatched) = Build();
        var jump = Jump(Here, System);

        Assert.Empty(callout.Examine(Context(Commander(jump), false, jump)));
        await RunDispatched(dispatched);
        Assert.Single(callout.Examine(Context(Commander(jump), false)));

        var scan = Scan();
        var scanned = Commander(jump, Signals(), scan);

        Assert.Single(new BiologyCallout().Examine(Context(scanned, false, scan)));
        Assert.Empty(new BiologyCallout { AlreadySaid = callout.Reported }.Examine(Context(scanned, false, scan)));
    }

    [Fact]
    public async Task ABodyNamedOnScanBeforeTheLookupReturnsIsLeftOutOfTheArrivalLine()
    {
        var (callout, _, dispatched) = Build();
        var biology = new BiologyCallout { AlreadySaid = callout.Reported };
        callout.AlreadySaid = biology.Named;
        var jump = Jump(Here, System);

        Assert.Empty(callout.Examine(Context(Commander(jump), false, jump)));

        var scan = Scan();
        var scanned = Commander(jump, Signals(), scan);

        Assert.Single(biology.Examine(Context(scanned, false, scan)));
        await RunDispatched(dispatched);

        var said = Assert.Single(callout.Examine(Context(scanned, false)));

        Assert.Equal("Spansh has surveyed biology here: 3 c at 12 million.", said.Text);
    }

    [Fact]
    public void TheGalaxySearchDisclosureNamesTheArrivalLookup()
    {
        var settings = new D47Settings { Knowledge = new KnowledgeSettings { GalaxySearch = true } };
        var quiet = settings with { Callouts = settings.Callouts with { SurveyedBiology = false } };

        var on = EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, settings, llmKeyPresent: false);
        var off = EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, quiet, llmKeyPresent: false);

        Assert.Contains("Arriving in a system sends that system's address to spansh.co.uk", on.What);
        Assert.DoesNotContain("Arriving in a system", off.What);
    }
}
