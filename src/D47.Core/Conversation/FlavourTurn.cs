using System.Globalization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>One short in-character line from the model, off the conversation path.</summary>
public static class FlavourTurn
{
    /// <summary>Asks for one line.</summary>
    /// <param name="persona">The persona block, or null when personality is off.</param>
    /// <param name="aboutMe">
    /// Position 4 — the Commander's own account of themselves, already composed by <see
    /// cref="CommanderStory"/> to the depth the caller chose, or null when this line is not the ship's
    /// AI speaking to the Commander (Phase 43).
    /// </param>
    public const int AnswerBudget = 400;

    /// <summary>Room for a model to think before it answers (#97).</summary>
    public const int ReasoningHeadroom = 800;

    /// <summary><param name="instruction"> What to say and why, as a user turn.</summary>
    /// <param name="instruction">What to say and why, as a user turn.</param>
    /// <param name="scenario">The Commander's scenario, already gated by the brief and the audience, or null.</param>
    /// <param name="hiddenStory">The running stock story's hidden layer, or null.</param>
    /// <param name="gameState">Live state for the line to be about.</param>
    /// <param name="maxOutputTokens">
    /// The ceiling on the whole completion, which is <see cref="AnswerBudget"/> plus <see
    /// cref="ReasoningHeadroom"/> by default and is raised only by the adventure generator, whose
    /// answer is a whole story in JSON, and by <c>ShipPlanAdvisor</c>.
    /// </param>
    /// <param name="effort">
    /// Low for a remark; the generator asks for more, because a story is a reasoning problem.
    /// </param>
    /// <param name="sampling">How adventurous the sampler may be (#98).</param>
    /// <param name="webSearch">
    /// Whether the provider may search the web while writing this line (Phase 23, "Look it up, and say
    /// where the answer came from").
    /// </param>
    public static async Task<string?> AskAsync(
        ILlmProvider? provider,
        string? model,
        string? persona,
        string? aboutMe,
        string instruction,
        string? gameState,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        CancellationToken cancellationToken = default,
        bool webSearch = false,
        int? maxOutputTokens = null,
        ThinkingEffort effort = ThinkingEffort.Low,
        LlmSampling? sampling = null,
        bool canBeDirected = false,
        string? humor = null,
        string? scenario = null,
        string? hiddenStory = null,
        string? recentDelivery = null) =>
        (await AskForAsync(
            provider,
            model,
            persona,
            aboutMe,
            instruction,
            gameState,
            spend,
            prices,
            logger,
            cancellationToken,
            webSearch,
            maxOutputTokens,
            effort,
            sampling,
            canBeDirected,
            humor,
            scenario,
            hiddenStory,
            recentDelivery: recentDelivery).ConfigureAwait(false)).Line;

    /// <summary>As <see cref="AskAsync"/>, saying why when no line came back.</summary>
    /// <param name="thinking">Whether the model may think first; see <see cref="LlmRequest.ThinkingAllowed"/>.</param>
    /// <param name="limits">When the call is stopped as <see cref="FlavourMiss.TimedOut"/>; null for no limit.</param>
    public static async Task<FlavourReply> AskForAsync(
        ILlmProvider? provider,
        string? model,
        string? persona,
        string? aboutMe,
        string instruction,
        string? gameState,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        CancellationToken cancellationToken = default,
        bool webSearch = false,
        int? maxOutputTokens = null,
        ThinkingEffort effort = ThinkingEffort.Low,
        LlmSampling? sampling = null,
        bool canBeDirected = false,
        string? humor = null,
        string? scenario = null,
        string? hiddenStory = null,
        bool thinking = true,
        string? recentDelivery = null,
        FlavourLimits? limits = null)
    {
        if (provider is null)
        {
            return new FlavourReply(null, FlavourMiss.NoModel);
        }

        var chosenModel = model ?? provider.DefaultModel;
        var ceiling = maxOutputTokens ?? (AnswerBudget + ReasoningHeadroom);

        var request = new LlmRequest
        {
            Model = chosenModel,
            // Low unless the caller says otherwise.
            Effort = effort,

            // In character unless the caller says otherwise (#98).
            Sampling = sampling ?? LlmSampling.InCharacter,

            // Short on purpose, and now with room to think first (<a
            // href=".com/dseelinger/d47/issues/97">#97</a>).
            MaxOutputTokens = ceiling,

            // Off for every line that came before Phase 23.
            WebSearch = webSearch,
            ThinkingAllowed = thinking,
            Prompt = new PromptAssembly
            {
                // No tools.
                Persona = persona,

                // Chatter, a carrier captain and an ambient remark are spoken by whichever provider that slot
                // uses, so the caller answers this rather than the setting for the ship's own voice (#291).
                CanBeDirected = canBeDirected,
                AboutMe = aboutMe,
                Scenario = scenario,
                HiddenStory = hiddenStory,
                History = [new ConversationMessage(ConversationRole.User, instruction)],
                LiveGameState = gameState,
                Humor = humor,
                RecentDelivery = canBeDirected ? recentDelivery : null,
            },
        };

        var reply = new System.Text.StringBuilder();
        var stopReason = LlmStopReason.Completed;

        // The request is billed from the moment the provider reports usage, whether or not it completes.
        LlmUsage? usage = null;

        void Record()
        {
            if (spend is null || usage is null)
            {
                return;
            }

            var price = prices?.For(provider.Id, chosenModel);
            spend.Record(
                price is null ? TurnCost.Unpriced(usage) : new TurnCost(usage, price.DollarsFor(usage), true),

                // Its prefix is not the conversation's prefix, so a cold one here is expected rather than the
                // regression an unexplained cache miss on the turn path is.
                coldPrefixExpected: true,
                provider.Id,
                chosenModel);
        }

        var time = limits?.Time ?? TimeProvider.System;
        var began = time.GetTimestamp();
        string? stopped = null;
        var finished = false;

        void Stop(string limit, TimeSpan after) =>
            Interlocked.CompareExchange(
                ref stopped,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{limit} {after.TotalSeconds:0} s; stopped after {time.GetElapsedTime(began).TotalSeconds:0.0} s"),
                null);

        var silenceLimit = limits?.Silence ?? Timeout.InfiniteTimeSpan;

        using var call = limits?.Call is { } callLimit ? new CancellationTokenSource(callLimit, time) : null;
        using var silence = limits is null ? null : new CancellationTokenSource(silenceLimit, time);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, call?.Token ?? default, silence?.Token ?? default);
        using var callStops = call?.Token.Register(() => Stop("a cloud call may run for", limits!.Call!.Value));
        using var silenceStops = silence?.Token.Register(() => Stop("no text or thinking arrived for", silenceLimit));

        // Null when the caller cancelled, which is not a limit.
        FlavourReply? TimedOut() =>
            stopped is { } why && !cancellationToken.IsCancellationRequested ? new FlavourReply(null, FlavourMiss.TimedOut, why) : null;

        try
        {
            await foreach (var streamEvent in provider
                               .StreamAsync(request, limited.Token)
                               .ConfigureAwait(false))
            {
                switch (streamEvent)
                {
                    case LlmStreamEvent.TextDelta text:
                        reply.Append(text.Text);
                        silence?.CancelAfter(silenceLimit);
                        break;

                    case LlmStreamEvent.ThinkingDelta:
                        silence?.CancelAfter(silenceLimit);
                        break;

                    case LlmStreamEvent.Started started:
                        usage = started.Usage;
                        break;

                    case LlmStreamEvent.Completed completed:
                        finished = true;
                        usage = completed.Usage;
                        stopReason = completed.StopReason;
                        break;

                    case LlmStreamEvent.Failed failed:
                        logger?.LogDebug("A flavour line was not generated: {Message}", failed.Message);
                        Record();
                        return TimedOut() ?? new FlavourReply(null, FlavourMiss.Failed, failed.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Record();
            return TimedOut() ?? new FlavourReply(null, FlavourMiss.Cancelled);
        }
        catch (Exception ex)
        {
            // A provider throwing where it should have reported is still just a line that did not happen.
            logger?.LogDebug(ex, "A flavour line was not generated");
            Record();
            return TimedOut() ?? new FlavourReply(null, FlavourMiss.Failed, ex.Message);
        }

        // A provider that ends its stream without throwing when cancelled has still been stopped.
        if (!finished && TimedOut() is { } timedOut)
        {
            Record();
            return timedOut;
        }

        // A provider that completes without reporting usage is still recorded, as an empty turn.
        usage ??= LlmUsage.None;
        Record();

        var line = reply.ToString().Trim();
        FlavourCutOff? cutOff = null;

        // **A truncated turn is not a declined one, and used to be indistinguishable from it** (#97).
        if (stopReason == LlmStopReason.MaxTokens)
        {
            cutOff = new FlavourCutOff(usage.Reported ? usage.TotalInputTokens : null, usage.Reported ? usage.OutputTokens : null, ceiling);

            if (cutOff.AtContext)
            {
                logger?.LogWarning(
                    "{Model} stopped after {Prompt} prompt and {Reply} reply tokens, short of the {Ceiling}-token "
                    + "ceiling: the endpoint's context ran out. {Wrote} characters were written.",
                    chosenModel,
                    cutOff.PromptTokens,
                    cutOff.ReplyTokens,
                    ceiling,
                    line.Length);
            }
            else
            {
                logger?.LogWarning(
                    "{Model} spent the whole {Ceiling}-token ceiling before finishing; "
                    + "{Wrote} characters were written. If it is a reasoning model, the thinking is "
                    + "being charged to the same ceiling as the answer.",
                    chosenModel,
                    ceiling,
                    line.Length);
            }
        }

        return stopReason == LlmStopReason.Refusal
            ? new FlavourReply(null, FlavourMiss.Failed, "the model refused")
            : line.Length == 0
                ? new FlavourReply(null, FlavourMiss.Failed, "the reply was empty") { CutOff = cutOff }
                : new FlavourReply(line) { CutOff = cutOff };
    }
}

/// <summary>Why <see cref="FlavourTurn.AskForAsync"/> returned no line.</summary>
public enum FlavourMiss
{
    /// <summary>A line came back.</summary>
    None,

    /// <summary>There was no provider to ask.</summary>
    NoModel,

    /// <summary>The provider reported a failure, threw, refused or returned nothing.</summary>
    Failed,

    /// <summary>The caller's token was cancelled first; for a budgeted line, it timed out.</summary>
    Cancelled,

    /// <summary>A <see cref="FlavourLimits"/> limit stopped the call; the message says which and when.</summary>
    TimedOut,
}

/// <summary>
/// How long one call may run: <see cref="Call"/> in all, when set, and <see cref="Silence"/> with no text or
/// thinking arriving.
/// </summary>
public sealed record FlavourLimits(TimeSpan? Call, TimeSpan Silence)
{
    /// <summary>The clock both limits run on.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}

/// <summary>One flavour line, or why there is none.</summary>
/// <param name="Message">The provider's own message for a <see cref="FlavourMiss.Failed"/> reply.</param>
public sealed record FlavourReply(string? Line, FlavourMiss Miss = FlavourMiss.None, string? Message = null)
{
    /// <summary>Where the reply was cut off, or null when it finished.</summary>
    public FlavourCutOff? CutOff { get; init; }
}

/// <summary>A reply that stopped at a token limit, with the provider's counts where it reported them.</summary>
public sealed record FlavourCutOff(int? PromptTokens, int? ReplyTokens, int Ceiling)
{
    /// <summary>Whether the reply stopped short of the ceiling, so the endpoint's context was the limit.</summary>
    public bool AtContext => ReplyTokens is { } reply && reply > 0 && reply < Ceiling;

    public string Describe() => AtContext
        ? $"at the endpoint's context after {PromptTokens} prompt and {ReplyTokens} reply tokens"
        : $"at the {Ceiling}-token output ceiling";
}
