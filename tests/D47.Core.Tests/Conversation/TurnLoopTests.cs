using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class TurnLoopTests
{
    private static CapabilityRegistry BuiltinRegistry(TempInstall install, GameStateStore? gameState = null) =>
        TestSurface.For(install, gameState).Registry;

    private static TurnLoop Build(
        CapabilityRegistry registry,
        ILlmProvider? provider,
        out LlmAvailabilityState availability,
        out SpendTracker spend)
    {
        availability = new LlmAvailabilityState(provider is not null);
        spend = new SpendTracker();

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            availability,
            spend,
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        // One attempt, so a provider call count in these tests means a turn rather than a
        // turn times the retry budget. Retry has its own tests, where it is the subject.
        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<(TurnResult Result, string Text)> RunAsync(TurnLoop loop, string input)
    {
        var text = new System.Text.StringBuilder();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, TestContext.Current.CancellationToken))
        {
            switch (turnEvent)
            {
                case TurnEvent.TextDelta delta:
                    text.Append(delta.Text);
                    break;
                case TurnEvent.Completed completed:
                    result = completed.Result;
                    break;
            }
        }

        Assert.NotNull(result);
        return (result, text.ToString());
    }

    [Fact]
    public async Task WithNoProviderTheKeywordRouterStillAnswers()
    {
        // The load-bearing claim: every input path is answerable with no capabilities at all.
        using var install = new TempInstall();
        var loop = Build(BuiltinRegistry(install), provider: null, out _, out _);

        var (result, text) = await RunAsync(loop, "what's your status");

        Assert.Equal(TurnRoute.KeywordRouter, result.Route);
        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Contains("1.0.0-test", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoProviderAndNoKeywordMatchTheTurnIsUnsureNotFailed()
    {
        using var install = new TempInstall();
        var loop = Build(BuiltinRegistry(install), provider: null, out _, out _);

        var (result, text) = await RunAsync(loop, "compose a sonnet about hyperspace");

        Assert.Equal(TurnOutcome.Unsure, result.Outcome);
        Assert.Equal(TurnRoute.NoCapability, result.Route);
        Assert.Contains("not sure", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheKeywordRouterGetsFirstRefusalEvenWhenAModelIsAvailable()
    {
        // Protected settings are reachable by voice only through this path, so it cannot be a
        // fallback that a configured model bypasses.
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("The model answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        var (result, _) = await RunAsync(loop, "what's your status");

        Assert.Equal(TurnRoute.KeywordRouter, result.Route);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task UnmatchedInputReachesTheModelAndStreams()
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Hyperspace "),
            new LlmStreamEvent.TextDelta("is fine."),
            new LlmStreamEvent.Completed(new LlmUsage(100, 20, 0, 0), LlmStopReason.Completed));
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        var (result, text) = await RunAsync(loop, "tell me about hyperspace physics");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Equal("Hyperspace is fine.", text);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ARefusalIsAnUnsureTurnRatherThanAFailure()
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Refusal));
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        var (result, _) = await RunAsync(loop, "tell me about hyperspace physics");

        Assert.Equal(TurnOutcome.Unsure, result.Outcome);
    }

    [Fact]
    public async Task ATransientFailureFlipsTheCapabilityOffAndTheNextTurnRoutesAround()
    {
        // "Capabilities as state, not guard": there is no failure handler, just a state the next
        // turn reads.
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.Failed("Rate limited.", Transient: true));
        var loop = Build(BuiltinRegistry(install), provider, out var availability, out _);

        var (first, _) = await RunAsync(loop, "tell me about hyperspace physics");
        Assert.Equal(TurnOutcome.Failed, first.Outcome);
        Assert.Equal(LlmAvailability.TemporarilyUnavailable, availability.Current);

        var (second, _) = await RunAsync(loop, "tell me about hyperspace physics");
        Assert.Equal(TurnRoute.NoCapability, second.Route);
        Assert.Equal(TurnOutcome.Unsure, second.Outcome);
    }

    [Fact]
    public async Task ATransientOutageIsProbedAgainRatherThanLatchingOffForever()
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.Failed("Overloaded.", Transient: true));
        var loop = Build(BuiltinRegistry(install), provider, out var availability, out _);

        await RunAsync(loop, "tell me about hyperspace physics");
        Assert.Equal(LlmAvailability.TemporarilyUnavailable, availability.Current);
        Assert.Equal(1, provider.CallCount);

        // Turns, not seconds — no Core component reads the clock, so recovery is counted rather
        // than timed. The intervening turns route around the model without contacting it.
        for (var i = 0; i < LlmAvailabilityState.ProbeAfterTurns - 1; i++)
        {
            await RunAsync(loop, "tell me about hyperspace physics");
        }

        Assert.Equal(1, provider.CallCount);

        // The probe turn contacts the provider again. It fails again here because the fake always
        // fails, which is the correct outcome — the property being asserted is that the state
        // never latches off permanently, not that a broken provider heals.
        await RunAsync(loop, "tell me about hyperspace physics");
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task ASuccessfulProbeRestoresTheCapability()
    {
        using var install = new TempInstall();
        var availability = new LlmAvailabilityState(providerConfigured: true);
        availability.MarkFailed("Overloaded.", transient: true);

        var provider = FakeLlmProvider.Answering("Recovered.");
        var loop = new TurnLoop(
            BuiltinRegistry(install),
            new KeywordRouter(BuiltinRegistry(install)),
            availability,
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider);

        for (var i = 0; i < LlmAvailabilityState.ProbeAfterTurns; i++)
        {
            await RunAsync(loop, "tell me about hyperspace physics");
        }

        Assert.Equal(LlmAvailability.Available, availability.Current);
        Assert.Null(availability.Reason);
    }

    [Fact]
    public async Task AConfigurationFailureDoesNotClearItself()
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.Failed("The API key was rejected.", Transient: false));
        var loop = Build(BuiltinRegistry(install), provider, out var availability, out _);

        await RunAsync(loop, "tell me about hyperspace physics");

        for (var i = 0; i < LlmAvailabilityState.ProbeAfterTurns + 2; i++)
        {
            await RunAsync(loop, "tell me about hyperspace physics");
        }

        // Retrying a rejected key forever would be noise; only a settings change should clear it.
        Assert.Equal(LlmAvailability.NotConfigured, availability.Current);
    }

    [Fact]
    public async Task TheTurnCarriesItsPriceAndAddsToTheRunningTotal()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering(
            "Answered.", new LlmUsage(InputTokens: 1_000_000, OutputTokens: 0, 0, 0));
        var loop = Build(BuiltinRegistry(install), provider, out _, out var spend);

        var (result, _) = await RunAsync(loop, "tell me about hyperspace physics");

        Assert.NotNull(result.Cost);
        Assert.True(result.Cost.Priced);

        // A million input tokens on claude-opus-5 at $5/MTok.
        Assert.Equal(5m, result.Cost.Dollars);
        Assert.Equal(5m, spend.RunningTotalDollars);
    }

    [Fact]
    public async Task AnUnexpectedColdPrefixIsCountedAsARegression()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering(
            "Answered.", new LlmUsage(0, 10, CacheCreationInputTokens: 5_000, 0));
        var loop = Build(BuiltinRegistry(install), provider, out _, out var spend);

        // The first turn writes cache legitimately — a cold prefix is expected there.
        await RunAsync(loop, "tell me about hyperspace physics");
        Assert.Equal(0, spend.UnexplainedColdPrefixes);

        // The second writes cache on an unchanged model, which is the regression signal.
        await RunAsync(loop, "tell me more about hyperspace physics");
        Assert.Equal(1, spend.UnexplainedColdPrefixes);
    }

    [Fact]
    public async Task EffortIsChosenPerTurnAndReportedOnTheResult()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        var (lookup, _) = await RunAsync(loop, "tell me about hyperspace");
        var (hard, _) = await RunAsync(loop, "carefully plan the cheapest route to Colonia");

        Assert.NotNull(hard.Effort);
        Assert.Equal(ThinkingEffort.Max, hard.Effort);
        Assert.True(hard.Effort > lookup.Effort, "A deliberate ask should outrank a plain one.");
    }

    [Fact]
    public async Task HistoryAccumulatesOnlyAnsweredTurns()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        await RunAsync(loop, "tell me about hyperspace physics");
        Assert.Equal(2, loop.History.Count);

        Assert.Equal(ConversationRole.User, loop.History[0].Role);
        Assert.Equal(ConversationRole.Assistant, loop.History[1].Role);

        // The model's own history is what it sees next turn; the request carries it plus the new
        // user turn.
        await RunAsync(loop, "and what about witchspace");
        Assert.Equal(3, provider.LastRequest!.Prompt.History.Count);
    }

    [Fact]
    public async Task TheGuardrailsReachTheProviderOnEveryTurn()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        await RunAsync(loop, "tell me about hyperspace physics");

        Assert.Contains(
            Guardrails.Text,
            provider.LastRequest!.Prompt.RenderCachedSystemBlock(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveGameStateIsPassedBelowTheBreakpointNotInsideIt()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);
        loop.LiveGameState = () => "Current system: Shinrarta Dezhra.";

        await RunAsync(loop, "tell me about hyperspace physics");

        var prompt = provider.LastRequest!.Prompt;
        Assert.Equal("Current system: Shinrarta Dezhra.", prompt.LiveGameState);
        Assert.DoesNotContain("Shinrarta", prompt.RenderCachedSystemBlock(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AJournalBackedQuestionIsAnsweredWithoutAModel()
    {
        // End to end across two phases: the journal spine feeds game state, the registry exposes
        // it, and the keyword router reaches it with no provider configured at all.
        using var install = new TempInstall();
        var gameState = new GameStateStore();
        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            NullLogger.Instance,
            out var commander));
        gameState.Apply(commander!);
        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Fixture Reach"}""",
            NullLogger.Instance,
            out var jump));
        gameState.Apply(jump!);

        var loop = Build(BuiltinRegistry(install, gameState), provider: null, out _, out _);

        var (result, text) = await RunAsync(loop, "where am I");

        Assert.Equal(TurnRoute.KeywordRouter, result.Route);
        Assert.Contains("Fixture Reach", text, StringComparison.Ordinal);
    }
}
