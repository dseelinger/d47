using System.Runtime.CompilerServices;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

public enum TurnRoute
{
    /// <summary>Answered by the model-free keyword router. No provider was contacted.</summary>
    KeywordRouter,

    /// <summary>
    /// A settings change made by the model-free router. Its own route because it is the one
    /// path by which a protected setting can be reached by voice (architecture.md §7), and a
    /// path like that should be legible in the transcript rather than filed under something else.
    /// </summary>
    SettingCommand,

    /// <summary>
    /// A game action performed by the model-free router from a declared phrase. Its own route
    /// for the same reason <see cref="SettingCommand"/> is: this is the path that presses a key
    /// in the Commander's ship without a model in it, and a path like that should be legible in
    /// the transcript rather than filed under something else.
    /// </summary>
    ActionCommand,

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

    /// <summary>
    /// About to try again after a transient failure. Carries the wait so a surface can say how
    /// long it is about to be quiet for, which is the whole point of the item: silence that
    /// nobody has accounted for is indistinguishable from a hang (list.md Phase 5).
    /// </summary>
    public sealed record Retrying(int Attempt, int Of, TimeSpan Wait, string Because) : TurnEvent;

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
    string? model = null,
    SettingsService? settings = null,
    ITurnClock? clock = null)
{
    private readonly ITurnClock _clock = clock ?? SystemTurnClock.Instance;

    private List<ConversationMessage> _history = [];

    private string? _lastModelUsed;

    /// <summary>
    /// How hard to try before saying so out loud. Settable so a settings change applies to the
    /// next turn without a restart (list.md Phase 4).
    /// </summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default;

    public IReadOnlyList<ConversationMessage> History => _history;

    /// <summary>
    /// Points the loop at a different transcript. This is how separate memory per persona works
    /// (guardian-personas.md): each core owns a list, the host hands over the incoming one, and
    /// nothing is copied — a copy that is one turn stale is a core remembering a conversation
    /// that did not happen.
    /// <para>
    /// The list is appended to in place, so the caller keeps hold of it and gets the turns back
    /// for free. Swapping mid-turn is not supported and does not need to be: a switch is a
    /// settings change, and settings changes are applied between turns.
    /// </para>
    /// </summary>
    public void UseTranscript(List<ConversationMessage> transcript) => _history = transcript;

    /// <summary>
    /// The provider answering turns, or null for none. Settable because a key or an endpoint
    /// can change mid-session and the next turn has to use it — "apply every setting without a
    /// restart" reaches in here (list.md Phase 4).
    /// </summary>
    public ILlmProvider? Provider { get; set; } = provider;

    /// <summary>The pinned model, or null for the provider's own default.</summary>
    public string? Model { get; set; } = model;

    /// <summary>The persona block, or null for "personality off". Never reaches the guardrails.</summary>
    public string? Persona { get; set; }

    public string? AboutMe { get; set; }

    /// <summary>
    /// Live game state for the turn about to run. A source rather than a value: the tick loop
    /// is folding journal events continuously, so anything assigned here would be as old as the
    /// last time somebody remembered to assign it. Asked once per turn, at the moment the
    /// prompt is built.
    /// </summary>
    public Func<string?>? LiveGameState { get; set; }

    /// <summary>
    /// The mode the Commander is in, for choosing the tool profile. A source rather than a
    /// value for the same reason <see cref="LiveGameState"/> is: it changes several times a
    /// minute underneath this.
    /// </summary>
    public Func<Input.ControlContext>? ToolContext { get; set; }

    /// <summary>
    /// Whether the Commander has allowed d47 to press keys. When they have not, no action tool
    /// ships in any mode — advertising a tool that will refuse every call is paying for a
    /// refusal on every turn.
    /// </summary>
    public Func<bool>? ActionsEnabled { get; set; }

    public async IAsyncEnumerable<TurnEvent> RunAsync(
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        availability.BeginTurn();

        // 1. A declared settings phrase is the most specific thing an input can be, and the one
        //    path allowed to reach a protected row without hands on the panel.
        if (settings is not null && keywordRouter.MatchSetting(input) is { } settingCommand)
        {
            yield return new TurnEvent.Routed(TurnRoute.SettingCommand, Effort: null);

            var applied = settings.Apply(settingCommand.Row.Key, settingCommand.Value, SettingsCaller.KeywordRouter);

            logger.LogInformation(
                "Keyword router applied {Key} from the phrase \"{Phrase}\": {Status}",
                settingCommand.Row.Key,
                settingCommand.Phrase,
                applied.Status);

            yield return new TurnEvent.TextDelta(applied.Message);
            yield return new TurnEvent.Completed(new TurnResult(
                applied.Ok ? TurnOutcome.Answered : TurnOutcome.Failed,
                TurnRoute.SettingCommand,
                applied.Message,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 2. A declared action phrase. Above the general router because it is more specific,
        //    and above the model because "gear down" should not cost a network round trip at
        //    the moment the Commander is landing.
        if (keywordRouter.MatchToolCommand(input) is { } toolCommand)
        {
            yield return new TurnEvent.Routed(TurnRoute.ActionCommand, Effort: null);

            var actioned = await capabilities
                .InvokeAsync(toolCommand.ToolName, toolCommand.Arguments, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Keyword router performed {Tool} from the phrase \"{Phrase}\"",
                toolCommand.ToolName,
                toolCommand.Phrase);

            yield return new TurnEvent.TextDelta(actioned.Content);
            yield return new TurnEvent.Completed(new TurnResult(
                actioned.IsError ? TurnOutcome.Failed : TurnOutcome.Answered,
                TurnRoute.ActionCommand,
                actioned.Content,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 3. The rest of the model-free path, before anything reaches a provider.
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

        // 4. The model, if there is one to ask.
        // Captured once: the property can be swapped by a settings change between turns, and a
        // turn should run against the provider it started with.
        var activeProvider = Provider;

        if (activeProvider is null || !availability.CanAttemptModelTurn)
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

        await foreach (var turnEvent in RunModelTurnAsync(input, activeProvider, cancellationToken)
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
        var chosenModel = Model ?? activeProvider.DefaultModel;
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
                // Which tools ship is a choice between pre-declared profiles, never between
                // individual tools (list.md Phase 10) — a per-turn set would rewrite position 1
                // and invalidate the whole cached prefix. The profile is quantized by mode, so
                // a Commander who stays in supercruise pays for one cache entry and reads it.
                //
                // Gated on the provider actually being able to execute a tool_use reply.
                // Advertising a tool the loop would silently drop is worse than not offering
                // it: the model then tells the Commander it has done something that never
                // happened. False everywhere today, so this ships nothing yet.
                Tools = activeProvider.CapabilitiesFor(chosenModel).SupportsToolCalls
                    ? ToolProfiles.For(capabilities, ToolContext?.Invoke() ?? Input.ControlContext.None,
                        ActionsEnabled?.Invoke() ?? false).Tools
                    : [],
                Persona = Persona,
                AboutMe = AboutMe,
                History = [.. _history, new ConversationMessage(ConversationRole.User, input)],
                LiveGameState = LiveGameState?.Invoke(),
            },
        };

        yield return new TurnEvent.Routed(TurnRoute.Model, effort);

        var reply = new System.Text.StringBuilder();
        var usage = LlmUsage.None;
        var stopReason = LlmStopReason.Completed;
        string? failure = null;
        var transient = false;

        // Retry lives here rather than around the whole turn because of one asymmetry: once a
        // word has been streamed it has probably already been spoken, and there is no such
        // thing as un-saying it. So an attempt that produced text is never retried, however
        // transient the failure that ended it looked (list.md Phase 5).
        for (var attempt = 1; attempt <= Math.Max(1, Retry.Attempts); attempt++)
        {
            if (attempt > 1)
            {
                var wait = Retry.WaitBefore(attempt);

                yield return new TurnEvent.Retrying(attempt, Retry.Attempts, wait, failure ?? "no answer");
                logger.LogInformation(
                    "Retrying the model turn, attempt {Attempt} of {Total}, after {Wait}",
                    attempt,
                    Retry.Attempts,
                    wait);

                await _clock.DelayAsync(wait, cancellationToken).ConfigureAwait(false);
            }

            reply.Clear();
            usage = LlmUsage.None;
            stopReason = LlmStopReason.Completed;
            failure = null;
            transient = false;
            var spokeThisAttempt = false;

            await foreach (var streamEvent in AttemptAsync(request, activeProvider, cancellationToken)
                               .ConfigureAwait(false))
            {
                switch (streamEvent)
                {
                    case LlmStreamEvent.TextDelta text:
                        reply.Append(text.Text);
                        spokeThisAttempt = true;
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
                        transient = failed.Transient;
                        availability.MarkFailed(failed.Message, failed.Transient);
                        logger.LogWarning(
                            "Model turn failed ({Kind}): {Message}",
                            failed.Transient ? "transient" : "configuration",
                            failed.Message);
                        break;
                }
            }

            // A configuration failure will fail identically next time, so retrying it only
            // spends the Commander's silence. Only transient failures are worth waiting on.
            if (failure is null || spokeThisAttempt || !transient)
            {
                break;
            }
        }

        if (failure is not null)
        {
            // Said out loud in the current voice, because the alternative is silence, and
            // silence here is indistinguishable from a model with nothing to say.
            var text = Retry.Attempts > 1
                ? $"I couldn't reach the model after {Retry.Attempts} tries. {failure}"
                : $"I couldn't reach the model just then. {failure}";

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

    /// <summary>
    /// One attempt, with a stall turned into an ordinary failure event.
    /// <para>
    /// A provider that hangs is the case this exists for. Left alone it produces no events at
    /// all — not an error, just a turn that never ends — which is the single worst thing this
    /// app can do, because the Commander has no way to tell it apart from having been ignored.
    /// The per-attempt timeout converts that into a <see cref="LlmStreamEvent.Failed"/> the
    /// retry loop can act on and the voice can report.
    /// </para>
    /// <para>
    /// Written with an explicit enumerator because a `yield` cannot sit inside a `try` that has
    /// a `catch`, and the whole point here is catching around the provider.
    /// </para>
    /// </summary>
    private async IAsyncEnumerable<LlmStreamEvent> AttemptAsync(
        LlmRequest request,
        ILlmProvider activeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var timeout = _clock.CreateTimeout(Retry.AttemptTimeout, cancellationToken);

        await using var events = activeProvider
            .StreamAsync(request, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        while (true)
        {
            LlmStreamEvent? current = null;
            LlmStreamEvent.Failed? failed = null;
            var ended = false;

            try
            {
                if (await events.MoveNextAsync().ConfigureAwait(false))
                {
                    current = events.Current;
                }
                else
                {
                    ended = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The Commander called the turn off. Propagated rather than turned into a
                // failure event, and this clause has to come first: without it the general
                // catch below claims the cancellation, reports it as a transient failure, and
                // d47 announces "I couldn't reach the model" about a turn that was working
                // fine until it was told to stop.
                throw;
            }
            catch (OperationCanceledException)
            {
                // Ours tripped, not the caller's: the attempt ran out of time.
                failed = new LlmStreamEvent.Failed(
                    $"it did not answer within {Retry.AttemptTimeout.TotalSeconds:0} seconds",
                    Transient: true);
            }
            catch (Exception ex)
            {
                // A provider throwing rather than reporting is still just a failed turn. It
                // must not take the app down, and it must not be swallowed into silence.
                failed = new LlmStreamEvent.Failed(ex.Message, Transient: true);
            }

            // Yielded out here because a `yield` cannot sit in a `catch` either — the value is
            // decided inside the guarded region and emitted outside it.
            if (failed is not null)
            {
                yield return failed;
                yield break;
            }

            if (ended)
            {
                yield break;
            }

            yield return current!;
        }
    }
}
