using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Mapping a body with biology names the variants other Commanders have earlier finds of there.</summary>
public class EarlierFindsAreNamedWhenABodyIsMappedTests
{
    private const long Here = 1_000_200;
    private const string System = "Fixture";

    private sealed class FakeCommunity(params EarlierFind[] finds) : ICommunityBiology
    {
        public List<string> Asked { get; } = [];

        public Task<IReadOnlyList<EarlierFind>> SystemFindsAsync(string systemName, CancellationToken cancellationToken)
        {
            Asked.Add(systemName);
            return Task.FromResult<IReadOnlyList<EarlierFind>>(finds);
        }
    }

    private static EarlierFind Find(string body, string name) => new($"{System} {body}", 1, name, 0, 0);

    private static (EarlierFindsCallout Callout, List<Func<Task>> Dispatched) Build(FakeCommunity community, bool on = true)
    {
        var dispatched = new List<Func<Task>>();

        var callout = new EarlierFindsCallout(NullLogger<EarlierFindsCallout>.Instance)
        {
            Community = () => on ? community : null,
            Dispatch = dispatched.Add,
        };

        return (callout, dispatched);
    }

    private static async Task RunDispatched(List<Func<Task>> dispatched)
    {
        foreach (var work in dispatched)
        {
            await work();
        }

        dispatched.Clear();
    }

    private static CommanderGameState Commander(long address = Here)
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        store.Apply(Parse($$"""{"timestamp":"2026-08-16T09:30:00Z","event":"FSDJump","StarSystem":"{{System}}","SystemAddress":{{address}}}"""));
        return store.Active!;
    }

    private static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Mapped(string body, int biological = 2) => $$"""
        {"timestamp":"2026-08-16T10:01:00Z","event":"SAASignalsFound","BodyName":"{{System}} {{body}}",
         "SystemAddress":{{Here}},"BodyID":23,
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{biological}}},
                    {"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":1}]}
        """;

    private static string MappedNoBiology(string body) => $$"""
        {"timestamp":"2026-08-16T10:01:00Z","event":"SAASignalsFound","BodyName":"{{System}} {{body}}",
         "SystemAddress":{{Here}},"BodyID":24,
         "Signals":[{"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":3}]}
        """;

    private static List<string> Said(IEnumerable<Announcement> announcements) => [.. announcements.Select(a => a.Text)];

    [Fact]
    public async Task ThreeVariantsOrFewerAreNamedInFullMostFindsFirst()
    {
        var community = new FakeCommunity(
            Find("3 b", "Osseus Fractus - Indigo"),
            Find("3 b", "Bacterium Aurasus - Green"),
            Find("3 b", "Bacterium Aurasus - Green"),
            Find("3 b", "Stratum Tectonicas - Lime"),
            Find("3 c", "Fungoida Setisis - Amethyst"));
        var (callout, dispatched) = Build(community);
        var state = Commander();

        Assert.Empty(callout.Examine(Context(state, false, Mapped("3 b"))));
        await RunDispatched(dispatched);

        Assert.Equal(
            ["Commanders have logged earlier finds on this body: Bacterium Aurasus - Green, Osseus Fractus - Indigo and Stratum Tectonicas - Lime."],
            Said(callout.Examine(Context(state, false))));
        Assert.Equal([System], community.Asked);
    }

    [Fact]
    public async Task VariantsBeyondThreeAreACount()
    {
        var community = new FakeCommunity(
            Find("3 b", "A - Green"), Find("3 b", "A - Green"), Find("3 b", "B - Lime"),
            Find("3 b", "C - Indigo"), Find("3 b", "D - Red"), Find("3 b", "E - Blue"));
        var (callout, dispatched) = Build(community);
        var state = Commander();

        _ = callout.Examine(Context(state, false, Mapped("3 b"))).ToList();
        await RunDispatched(dispatched);

        Assert.Equal(
            ["Commanders have logged earlier finds on this body: A - Green, B - Lime, C - Indigo and two more."],
            Said(callout.Examine(Context(state, false))));
    }

    [Fact]
    public void ABodyWithoutBiologySendsNoRequest()
    {
        var community = new FakeCommunity(Find("3 b", "A - Green"));
        var (callout, dispatched) = Build(community);

        Assert.Empty(callout.Examine(Context(Commander(), false, MappedNoBiology("3 b"), Mapped("3 d", biological: 0))));
        Assert.Empty(dispatched);
        Assert.Empty(community.Asked);
    }

    [Fact]
    public async Task ASecondBodyInTheSameSystemSendsNoSecondRequest()
    {
        var community = new FakeCommunity(Find("3 b", "A - Green"), Find("3 c", "B - Lime"));
        var (callout, dispatched) = Build(community);
        var state = Commander();

        _ = callout.Examine(Context(state, false, Mapped("3 b"), Mapped("3 c"))).ToList();
        await RunDispatched(dispatched);

        Assert.Equal(
            [
                "Commanders have logged earlier finds on this body: A - Green.",
                "Commanders have logged earlier finds on this body: B - Lime.",
            ],
            Said(callout.Examine(Context(state, false))).Order().ToList());

        _ = callout.Examine(Context(state, false, Mapped("3 d"))).ToList();
        Assert.Empty(dispatched);
        Assert.Single(community.Asked);
    }

    [Fact]
    public async Task ABodyIsNamedOnceAndABodyWithNoFindsSaysNothing()
    {
        var community = new FakeCommunity(Find("3 b", "A - Green"));
        var (callout, dispatched) = Build(community);
        var state = Commander();

        _ = callout.Examine(Context(state, false, Mapped("3 b"), Mapped("3 c"))).ToList();
        await RunDispatched(dispatched);

        Assert.Single(callout.Examine(Context(state, false)));
        Assert.Empty(callout.Examine(Context(state, false, Mapped("3 b"), Mapped("3 c"))));
    }

    [Fact]
    public void WithTheSettingOffNothingIsRequested()
    {
        var community = new FakeCommunity(Find("3 b", "A - Green"));
        var (callout, dispatched) = Build(community, on: false);

        Assert.Empty(callout.Examine(Context(Commander(), false, Mapped("3 b"))));
        Assert.Empty(dispatched);
        Assert.Empty(community.Asked);
    }

    [Fact]
    public void NothingIsRequestedWhilePriming()
    {
        var community = new FakeCommunity(Find("3 b", "A - Green"));
        var (callout, dispatched) = Build(community);

        Assert.Empty(callout.Examine(Context(Commander(), true, Mapped("3 b"))));
        Assert.Empty(dispatched);
    }

    [Fact]
    public async Task AnAnswerForASystemTheCommanderHasLeftIsNotSaid()
    {
        var community = new FakeCommunity(Find("3 b", "A - Green"));
        var (callout, dispatched) = Build(community);

        _ = callout.Examine(Context(Commander(), false, Mapped("3 b"))).ToList();
        await RunDispatched(dispatched);

        Assert.Empty(callout.Examine(Context(Commander(2_000_300), false)));
    }
}
