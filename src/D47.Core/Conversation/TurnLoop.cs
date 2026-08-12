using System.Runtime.CompilerServices;
using D47.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

public enum TurnRoute
{
    /// <summary>Answered by the model-free keyword router. No provider was contacted.</summary>
    KeywordRouter,

    /// <summary>Answered by the language model.</summary>
    Model,

    /// <summary>Nothing could answer it. Produces an unsure turn, not an error.</summary>
    NoCapability,
}

public enum TurnOutcome
{
    Answered,

    /// <summary>
    /// An explicit result, not a score. The checklist is specific about why: models produce
    /// confident-sounding confidence numbers that do not mean anything, so this is a state the
    /// turn can be in rather than a threshold someone has to pick.
    /// </summary>
    Unsure,

    Failed,
}

public sealed record TurnResult(
    TurnOutcome Outcome,
    TurnRoute Route,
    string Text,
    ThinkingEffort? Effort,
    TurnCost? Cost);

public abstract record TurnEvent
{
    private TurnEvent()
    {
    }

    /// <summary>Emitted as soon as routing is decided, before any work.</summary>
    public sealed record Routed(TurnRoute Route, ThinkingEffort? Effort) : TurnEvent;

    public sealed record TextDelta(string Text) : TurnEvent;

    public sealed record ThinkingDelta(string Text) : TurnEvent;

    public sealed record Completed(TurnResult Result) : TurnEvent;
}

/// <summary>
/// One turn, start to finish. Owns no thread and reads no clock: it is an async stream the
/// caller drives, so the UI, a test and a replay harness all drive it the same way.
/// <para>
/// Routing order is deliberate. The keyword router gets first refusal on every input because
/// some commands must never reach the model at all — protected settings are a property of the
/// caller, not the modality (architecture.md §7). Only unmatched input reaches the model.
/// </para>
/// </summary>
public sealed class TurnLoop(
    CapabilityRegistry capabilities,
    KeywordRouter keywordRouter,
    LlmAvailabilityState availability,
    SpendTracker spend,
    PriceTable prices,
    ILogger<TurnLoop> logger,
    ILlmProvider? provider = null,
    string? model = null)
{
    private readonly List<ConversationMessage> _history = [];

    private string? _lastModelUsed;

    public IReadOnlyList<ConversationMessage> History => _history;

    /// <summary>The persona block, or null for "personality off". Never reaches the guardrails.</summary>
    public string? Persona { get; set; }

    public string? AboutMe { get; set; }

    /// <summary>Live game state for the next turn, supplied by the caller from the journal.</summary>
    public string? LiveGameState { get; set; }

    public async IAsyncEnumerable<TurnEvent> RunAsync(
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        availability.BeginTurn();

        // 1. The model-free path first, always.
        if (keywordRouter.Match(input) is { } match)
        {
            yield return new TurnEvent.Routed(TurnRoute.KeywordRouter, Effort: null);

            var result = await capabilities
                .InvokeAsync(match.ToolName, ToolArguments.Empty, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Keyword router answered with {Capability}/{Tool}", match.CapabilityId, match.ToolName);

            yield return new TurnEvent.TextDelta(result.Content);
            yield return new TurnEvent.Completed(new TurnResult(
                result.IsError ? TurnOutcome.Failed : TurnOutcome.Answered,
                TurnRoute.KeywordRouter,
                result.Content,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 2. The model, if there is one to ask.
        if (provider is null || !availability.CanAttemptModelTurn)
        {
            var reason = availability.Reason ?? "No language model provider is configured.";
            logger.LogInformation("No model available for this turn: {Reason}", reason);

            var text =
                $"I'm not sure — I have no way to work that out right now. {reason} " +
                "Ask me something one of my own capabilities covers and I can still answer.";

            yield return new TurnEvent.Routed(TurnRoute.NoCapability, Effort: null);
            yield return new TurnEvent.TextDelta(text);
            yield return new TurnEvent.Completed(new TurnResult(
                TurnOutcome.Unsure, TurnRoute.NoCapability, text, Effort: null, Cost: null));
            yield break;
        }

        await foreach (var turnEvent in RunModelTurnAsync(input, provider, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return turnEvent;
        }
    }

    private async IAsyncEnumerable<TurnEvent> RunModelTurnAsync(
        string input,
        ILlmProvider activeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chosenModel = model ?? activeProvider.DefaultModel;
        var effort = EffortRouter.ChooseFor(input);

        // A cold prefix is only sanctioned on the first turn and after a model change. Anything
        // else writing cache is a regression the running total surfaces rather than hides.
        var coldPrefixExpected = _lastModelUsed != chosenModel;
        _lastModelUsed = chosenModel;

        var request = new LlmRequest
        {
            Model = chosenModel,
            Effort = effort,
            Prompt = new PromptAssembly
            {
                // Tools are not advertised yet: nothing here executes a tool_use block, and
                // advertising a tool the loop would silently drop is worse than not offering it.
                // The ordering is in place and tested so Phase 10 only has to fill position 1.
                Tools = [],
                Persona = Persona,
                AboutMe = AboutMe,
                History = [.. _history, new ConversationMessage(ConversationRole.User, input)],
                LiveGameState = LiveGameState,
            },
        };

        yield return new TurnEvent.Routed(TurnRoute.Model, effort);

        var reply = new System.Text.StringBuilder();
        var usage = LlmUsage.None;
        var stopReason = LlmStopReason.Completed;
        string? failure = null;

        await foreach (var streamEvent in activeProvider
                           .StreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            switch (streamEvent)
            {
                case LlmStreamEvent.TextDelta text:
                    reply.Append(text.Text);
                    yield return new TurnEvent.TextDelta(text.Text);
                    break;

                case LlmStreamEvent.ThinkingDelta thinking:
                    yield return new TurnEvent.ThinkingDelta(thinking.Text);
                    break;

                case LlmStreamEvent.Completed completed:
                    usage = completed.Usage;
                    stopReason = completed.StopReason;
                    break;

                case LlmStreamEvent.Failed failed:
                    failure = failed.Message;
                    availability.MarkFailed(failed.Message, failed.Transient);
                    logger.LogWarning(
                        "Model turn failed ({Kind}): {Message}",
                        failed.Transient ? "transient" : "configuration",
                        failed.Message);
                    break;
            }
        }

        if (failure is not null)
        {
            var text = $"I couldn't reach the model just then. {failure}";
            yield return new TurnEvent.TextDelta(text);
            yield return new TurnEvent.Completed(new TurnResult(
                TurnOutcome.Failed, TurnRoute.Model, text, effort, Cost: null));
            yield break;
        }

        availability.MarkAvailable();

        var price = prices.For(activeProvider.Id, chosenModel);
        var cost = price is null ? TurnCost.Unpriced(usage) : new TurnCost(usage, price.DollarsFor(usage), true);
        spend.Record(cost, coldPrefixExpected);

        var answer = reply.ToString().Trim();

        // A refusal is an unsure turn, not an error: the model declined, which is a real answer
        // about what it will do rather than a fault in the pipeline.
        var outcome = stopReason == LlmStopReason.Refusal || answer.Length == 0
            ? TurnOutcome.Unsure
            : TurnOutcome.Answered;

        if (outcome == TurnOutcome.Answered)
        {
            _history.Add(new ConversationMessage(ConversationRole.User, input));
            _history.Add(new ConversationMessage(ConversationRole.Assistant, answer));
        }

        logger.LogInformation(
            "Model turn {Outcome} at {Effort} effort; {Input} in ({CacheRead} cached), {Output} out, {Cost}",
            outcome,
            effort,
            usage.TotalInputTokens,
            usage.CacheReadInputTokens,
            usage.OutputTokens,
            cost.Priced ? cost.Dollars.ToString("C4") : "unpriced");

        yield return new TurnEvent.Completed(new TurnResult(outcome, TurnRoute.Model, answer, effort, cost));
    }
}
