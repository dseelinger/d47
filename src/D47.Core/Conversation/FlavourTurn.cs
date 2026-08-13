using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>
/// One short in-character line from the model, off the conversation path.
/// <para>
/// Phase 11 has three things that want the persona's voice without being a turn: a core's
/// reaction to the time it was switched off, an ambient remark motivated by game state, and a
/// carrier's tower answering an arrival. None of them belongs in
/// <see cref="TurnLoop"/> — the Commander did not ask a question, so there is nothing to route,
/// nothing to retry into their silence, and nothing that should land in the transcript as a
/// question they never asked.
/// </para>
/// <para>
/// <b>It still costs money, so it is still recorded.</b> An ambient line the running total
/// cannot see is the exact thing "LLM Turn Price" exists to prevent, and unattributed spend is
/// worse here than in a turn because nobody pressed anything to cause it.
/// </para>
/// <para>
/// Never throws and never retries. A flavour line that fails is a flavour line that does not
/// happen, and every caller has an authored fallback — which is the whole reason these are
/// allowed to be model-generated at all.
/// </para>
/// </summary>
public static class FlavourTurn
{
    /// <summary>
    /// Asks for one line. Returns null if there is no provider, the call failed, the model
    /// declined, or it came back empty — all four are the same thing to a caller: use the
    /// authored line instead.
    /// </summary>
    /// <param name="persona">
    /// The persona block, or null when personality is off. Guardrails ride along regardless:
    /// this builds a <see cref="PromptAssembly"/> like any other prompt, and position 2 has no
    /// setter (architecture.md §6).
    /// </param>
    /// <param name="instruction">
    /// What to say and why, as a user turn. Authored by d47 and never assembled from journal
    /// or comms text — untrusted content reaches these prompts as game state, below the
    /// breakpoint, never as the instruction itself (architecture.md §7).
    /// </param>
    /// <param name="gameState">Live state for the line to be about. Untrusted, and positioned as such.</param>
    public static async Task<string?> AskAsync(
        ILlmProvider? provider,
        string? model,
        string? persona,
        string instruction,
        string? gameState,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        if (provider is null)
        {
            return null;
        }

        var chosenModel = model ?? provider.DefaultModel;

        var request = new LlmRequest
        {
            Model = chosenModel,
            // Always Low. A one-line remark in character is not a reasoning problem, and
            // spending Max effort on ambient chatter would cost more than the turns the
            // Commander actually asked for.
            Effort = ThinkingEffort.Low,

            // Short on purpose. This is a sentence or two spoken over a cockpit, and a budget
            // is a cheaper guarantee of that than an instruction the model may talk past.
            MaxOutputTokens = 400,
            Prompt = new PromptAssembly
            {
                // No tools. There is nothing here for the model to do except speak, and an
                // empty position 1 is also what keeps this prompt's prefix distinct from the
                // conversation's rather than fighting it for the same cache entry.
                Persona = persona,
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
            // A provider throwing where it should have reported is still just a line that did
            // not happen. It must never reach a caller who was only decorating something.
            logger?.LogDebug(ex, "A flavour line was not generated");
            return null;
        }

        if (spend is not null)
        {
            var price = prices?.For(provider.Id, chosenModel);
            spend.Record(
                price is null ? TurnCost.Unpriced(usage) : new TurnCost(usage, price.DollarsFor(usage), true),

                // Its prefix is not the conversation's prefix, so a cold one here is expected
                // rather than the regression an unexplained cache miss on the turn path is.
                coldPrefixExpected: true);
        }

        var line = reply.ToString().Trim();

        return stopReason == LlmStopReason.Refusal || line.Length == 0 ? null : line;
    }
}
