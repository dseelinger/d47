using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A turn about one engineer or one ship names that page, on the keyword route and the model route (#575).</summary>
public class AQuestionNamesThePageItIsAboutTests
{
    private const string Commander =
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private const string Loadout =
        """{"timestamp":"3311-01-01T00:02:00Z","event":"Loadout","Ship":"krait_mkii","ShipID":4,"ShipName":"Fixture Runner","Modules":[]}""";

    private static int Farseer => EngineerDirectory.ByName("Felicity Farseer")!.Id;

    private static GameStateStore Store()
    {
        var store = new GameStateStore();

        foreach (var line in new[] { Commander, Loadout })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static TurnLoop Build(ILlmProvider provider)
    {
        var store = Store();
        var registry = CapabilityRegistry.Build(
            [EngineerCapability.Create(() => store.Active), JournalCapability.Create(store)]);

        return new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    private static RoundScriptedLlmProvider Calling(string tool, string json) =>
        new(RoundScriptedLlmProvider.Calling("call_1", tool, json), RoundScriptedLlmProvider.Saying("There."));

    [Fact]
    public async Task WhereIsAnEngineerAskedOfTheModelNamesTheirPage()
    {
        var result = await RunAsync(
            Build(Calling("find_engineer", """{"engineer":"Felicity Farseer"}""")),
            "where is Felicity Farseer");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(PageRef.Engineer(Farseer), result.Page);
    }

    [Fact]
    public async Task WhatIsLeftForAnEngineerOnTheKeywordRouteNamesTheirPage()
    {
        var result = await RunAsync(Build(new RoundScriptedLlmProvider()), "what's left for Felicity Farseer");

        Assert.NotEqual(TurnRoute.Model, result.Route);
        Assert.Equal(PageRef.Engineer(Farseer), result.Page);
    }

    [Fact]
    public async Task TellMeAboutMyShipNamesItsPage()
    {
        var result = await RunAsync(
            Build(Calling("get_ship", """{"ship":"Fixture Runner"}""")),
            "tell me about my Fixture Runner");

        Assert.Equal(PageRef.Ship(4), result.Page);
    }

    [Fact]
    public async Task WhatAmIFlyingNamesTheShipBeingFlown()
    {
        var result = await RunAsync(Build(new RoundScriptedLlmProvider()), "what am i flying");

        Assert.NotEqual(TurnRoute.Model, result.Route);
        Assert.Equal(PageRef.Ship(4), result.Page);
    }

    [Fact]
    public async Task EveryEngineersProgressNamesNoPage()
    {
        var result = await RunAsync(Build(new RoundScriptedLlmProvider()), "my engineers");

        Assert.NotEqual(TurnRoute.Model, result.Route);
        Assert.Null(result.Page);
    }

    [Fact]
    public async Task AnUnknownEngineerNamesNoPage()
    {
        var result = await RunAsync(
            Build(Calling("find_engineer", """{"engineer":"Nobody Fixturesson"}""")),
            "where is Nobody Fixturesson");

        Assert.Null(result.Page);
    }

    [Fact]
    public async Task EveryEngineersUnlockChainNamesNoPage()
    {
        var result = await RunAsync(
            Build(Calling("get_engineer_unlock_requirements", "{}")),
            "which engineers want meta-alloys");

        Assert.Null(result.Page);
    }
}
