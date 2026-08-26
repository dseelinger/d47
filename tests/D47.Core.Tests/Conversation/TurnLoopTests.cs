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

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
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

    /// <summary>
    /// Decision 3 of Phase 54, through the whole loop rather than at the clamp: a Commander who
    /// has set neither bound gets exactly what the router answered, on every shape of input.
    /// Asserted rather than assumed, because "identical to today" is the only promise an upgrade
    /// makes.
    /// </summary>
    [Theory]
    [InlineData("tell me about hyperspace")]
    [InlineData("what do you think about the Corvette")]
    [InlineData("what's the best route to Colonia")]
    [InlineData("carefully plan the cheapest route to Colonia")]
    public async Task NoBoundsIsExactlyWhatTheRouterAnswered(string input)
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        var (result, _) = await RunAsync(loop, input);

        Assert.Equal(EffortRouter.ChooseFor(input), result.Effort);
        Assert.Equal(EffortRouter.ChooseFor(input), provider.LastRequest!.Effort);
    }

    /// <summary>
    /// The floor lifts a turn the router priced below it — and the request carries the lifted
    /// rung, not merely the report. A clamp that only reached the report would be a setting that
    /// changed what d47 says it did and nothing about what it cost.
    /// </summary>
    [Fact]
    public async Task TheFloorLiftsAPlainTurnAndTheRequestCarriesIt()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        Assert.Equal(ThinkingEffort.Medium, EffortRouter.ChooseFor("tell me about hyperspace"));

        loop.EffortFloor = ThinkingEffort.Xhigh;

        var (result, _) = await RunAsync(loop, "tell me about hyperspace");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(ThinkingEffort.Xhigh, result.Effort);
        Assert.Equal(ThinkingEffort.Xhigh, provider.LastRequest!.Effort);
    }

    /// <summary>
    /// The ceiling earns its keep twice: it is a cost dial, and it is the guard against the
    /// router's own false positives. <c>EffortRouter</c> matches substrings with no word
    /// boundaries, so an idle "what do you think about" hits "think about" and is priced at Max.
    /// </summary>
    [Fact]
    public async Task TheCeilingCatchesTheRoutersOwnFalsePositive()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        Assert.Equal(ThinkingEffort.Max, EffortRouter.ChooseFor("what do you think about the Corvette"));

        loop.EffortCeiling = ThinkingEffort.Medium;

        var (result, _) = await RunAsync(loop, "what do you think about the Corvette");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(ThinkingEffort.Medium, result.Effort);
        Assert.Equal(ThinkingEffort.Medium, provider.LastRequest!.Effort);
    }

    /// <summary>
    /// A model with no effort dial says so rather than reporting one it never applied (list.md
    /// Phase 54). Haiku 4.5 predates the 4.6 generation and rejects the fields that carry it, so
    /// a turn on it thought at whatever it thinks at and no rung describes that.
    /// <para>
    /// <b>The request still carries the chosen rung, and that clause is the point of the test.</b>
    /// What the Commander asked for and what the provider can send are two different questions
    /// with two different owners: the router picks a rung from the words, and the provider is the
    /// only thing that knows whether the model will take it. A later simplification that
    /// short-circuited the request as well as the report would take the effort away from every
    /// model the moment one endpoint refused it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AModelWithNoEffortDialReportsNoEffortAndIsStillAskedForOne()
    {
        using var install = new TempInstall();

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Answered."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ThinkingEffort = false,
        };

        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        ThinkingEffort? routed = null;
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(
                           "carefully plan the cheapest route to Colonia",
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            switch (turnEvent)
            {
                case TurnEvent.Routed step:
                    routed = step.Effort;
                    break;
                case TurnEvent.Completed completed:
                    result = completed.Result;
                    break;
            }
        }

        Assert.NotNull(result);
        Assert.Equal(TurnRoute.Model, result.Route);

        Assert.Null(routed);
        Assert.Null(result.Effort);

        // Asked for all the same. The router's answer is unchanged by the provider's answer.
        Assert.Equal(ThinkingEffort.Max, provider.LastRequest!.Effort);
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

    /// <summary>
    /// What d47 says without being asked reaches the next turn (remediation.md 17, item 4).
    /// <para>
    /// Reported with the transcript: a route callout said *"Elvira Martuuk is one stop away"*, the
    /// Commander asked *"why would I care about that?"*, and d47 answered *"I have no record of
    /// what I said before this"* — which was exactly true. History was written in one place, the
    /// end of an answered model turn, so every callout, continuity line, habit remark, reminder
    /// and autonomous action went to the speaker and the panel and nowhere else.
    /// </para>
    /// <para>
    /// It rides into the following user turn rather than being appended as an assistant message,
    /// because an assistant message with nothing before it — or two in a row — is not a shape
    /// every endpoint accepts, and Phase 29 means more than one is in play. The assertion is
    /// therefore about what the provider is sent, and about it persisting afterwards.
    /// </para>
    /// </summary>
    [Fact]
    public async Task WhatItSaidUnpromptedReachesTheNextTurn()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        loop.Said("Commander. Elvira Martuuk is one stop away.");

        await RunAsync(loop, "why would I care about that?");

        var asked = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains("Elvira Martuuk is one stop away", asked, StringComparison.Ordinal);
        Assert.Contains("why would I care about that?", asked, StringComparison.Ordinal);

        // Labelled as d47's own, because it is arriving inside a user message and the Commander
        // did not say it.
        Assert.Contains("said-aloud", asked, StringComparison.Ordinal);

        // Said once. A line carried into every turn from here would be a callout repeating itself
        // silently, for ever.
        await RunAsync(loop, "and now?");

        Assert.DoesNotContain(
            "Elvira Martuuk is one stop away",
            Text(provider.LastRequest!.Prompt.History[^1]),
            StringComparison.Ordinal);

        // But it is still in the transcript, which is what makes the *next* question about it
        // answerable rather than only the first.
        Assert.Contains(
            loop.History,
            message => Text(message).Contains("Elvira Martuuk", StringComparison.Ordinal));
    }

    /// <summary>
    /// The keyword router's own answers are recorded too (remediation.md 17, item 4). *"Stop
    /// calling things out"* → *"done"* → *"why did you do that?"* reproduces the reported
    /// transcript by a second road, and none of the four model-free routes wrote a word.
    /// </summary>
    [Fact]
    public async Task TheRoutersOwnAnswerIsRecorded()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        var (routed, spoken) = await RunAsync(loop, "what are you watching for");

        Assert.Equal(TurnRoute.KeywordRouter, routed.Route);

        await RunAsync(loop, "tell me about hyperspace physics");

        Assert.Contains(
            spoken[..40],
            Text(provider.LastRequest!.Prompt.History[0]),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The transcript has a ceiling (remediation.md 17, item 4). It was unbounded, which was
    /// survivable while only answered turns accumulated and is not now that d47's own unprompted
    /// lines join them.
    /// </summary>
    [Fact]
    public async Task TheTranscriptIsBounded()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(BuiltinRegistry(install), provider, out _, out _);

        for (var turn = 0; turn < TurnLoop.TranscriptKept; turn++)
        {
            await RunAsync(loop, $"question {turn.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        Assert.Equal(TurnLoop.TranscriptKept, loop.History.Count);

        // The oldest went first, which is the ordinary meaning of a conversation you can follow.
        Assert.DoesNotContain(
            loop.History,
            message => Text(message).Contains("question 0", StringComparison.Ordinal));

        Assert.Contains(
            loop.History,
            message => Text(message).Contains(
                $"question {(TurnLoop.TranscriptKept - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                StringComparison.Ordinal));
    }

    private static string Text(ConversationMessage message) =>
        string.Join(
            ' ',
            message.Content.OfType<ConversationContent.Text>().Select(part => part.Value));

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
