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
    /// <param name="gameState">Live state for the line to be about.</param>
    /// <param name="maxOutputTokens">
    /// The ceiling on the whole completion, which is <see cref="AnswerBudget"/> plus <see
    /// cref="ReasoningHeadroom"/> by default and is raised only by the adventure generator, whose
    /// answer is a whole story in JSON.
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
        bool canBeDirected = false)
    {
        if (provider is null)
        {
            return null;
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
            Prompt = new PromptAssembly
            {
                // No tools.
                Persona = persona,

                // Chatter, a carrier captain and an ambient remark are spoken by whichever provider that slot
                // uses, so the caller answers this rather than the setting for the ship's own voice (#291).
                CanBeDirected = canBeDirected,
                AboutMe = aboutMe,
                History = [new ConversationMessage(ConversationRole.User, instruction)],
                LiveGameState = gameState,
            },
        };

        var reply = new System.Text.StringBuilder();
        var usage = LlmUsage.None;
        var stopReason = LlmStopReason.Completed;

        try
        {
            await foreach (var streamEvent in provider
                               .StreamAsync(request, cancellationToken)
                               .ConfigureAwait(false))
            {
                switch (streamEvent)
                {
                    case LlmStreamEvent.TextDelta text:
                        reply.Append(text.Text);
                        break;

                    case LlmStreamEvent.Completed completed:
                        usage = completed.Usage;
                        stopReason = completed.StopReason;
                        break;

                    case LlmStreamEvent.Failed failed:
                        logger?.LogDebug("A flavour line was not generated: {Message}", failed.Message);
                        return null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            // A provider throwing where it should have reported is still just a line that did not happen.
            logger?.LogDebug(ex, "A flavour line was not generated");
            return null;
        }

        if (spend is not null)
        {
            var price = prices?.For(provider.Id, chosenModel);
            spend.Record(
                price is null ? TurnCost.Unpriced(usage) : new TurnCost(usage, price.DollarsFor(usage), true),

                // Its prefix is not the conversation's prefix, so a cold one here is expected rather than the
                // regression an unexplained cache miss on the turn path is.
                coldPrefixExpected: true,
                provider.Id,
                chosenModel);
        }

        var line = reply.ToString().Trim();

        // **A truncated turn is not a declined one, and used to be indistinguishable from it** (#97).
        if (stopReason == LlmStopReason.MaxTokens)
        {
            logger?.LogWarning(
                "{Model} spent the whole {Ceiling}-token ceiling before finishing; "
                + "{Wrote} characters were written. If it is a reasoning model, the thinking is "
                + "being charged to the same ceiling as the answer.",
                chosenModel,
                ceiling,
                line.Length);
        }

        return stopReason == LlmStopReason.Refusal || line.Length == 0 ? null : line;
    }
}
