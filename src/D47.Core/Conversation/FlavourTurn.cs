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
    /// <param name="aboutMe">
    /// Position 4 — the Commander's own account of themselves, already composed by
    /// <see cref="CommanderStory"/> to the depth the caller chose, or null when this line is not
    /// the ship's AI speaking to the Commander (list.md Phase 43). Before this parameter existed
    /// every ambient remark, opening line and introduction was written by a model that had never
    /// heard of the person flying, which is the real reason those remarks felt generic.
    /// </param>
    /// <summary>
    /// What the answer itself is allowed to be: a sentence or two spoken over a cockpit. This is
    /// the number the budget has always meant, and the only one that describes what a Commander
    /// hears.
    /// </summary>
    public const int AnswerBudget = 400;

    /// <summary>
    /// Room for a model to think before it answers
    /// (<a href="https://github.com/dseelinger/d47/issues/97">#97</a>).
    /// <para>
    /// <b>Reasoning tokens are spent from the same ceiling as the answer</b>, so a reasoning model
    /// asked for 400 total spends them deliberating, is truncated before it writes a word, and
    /// returns empty content. Every caller here reads that as "use the authored line", so the
    /// generated lines simply stop appearing — no error, no banner, indistinguishable from a model
    /// that is merely dull. Measured: <c>qwen3:4b</c> spent 524 tokens thinking before a ten-word
    /// answer, and a repeated run ranged from 306 to 573.
    /// </para>
    /// <para>
    /// <b>Headroom rather than switching thinking off, and that is a measurement rather than a
    /// preference.</b> Three ways of asking an OpenAI-compatible endpoint not to reason were tried
    /// on 2026-08-26 and all three failed: <c>chat_template_kwargs.enable_thinking</c> and a
    /// <c>/no_think</c> system message are both ignored through the shim, and the native
    /// <c>think: false</c> moves the reasoning into <c>content</c>, which is worse. A ceiling is
    /// the one lever every endpoint honours.
    /// </para>
    /// <para>
    /// <b>It costs nothing when unused.</b> This is a ceiling, not a purchase — a model that
    /// answers in forty tokens is billed for forty whether the ceiling is 400 or 1200.
    /// </para>
    /// </summary>
    public const int ReasoningHeadroom = 800;

    /// <param name="instruction">
    /// What to say and why, as a user turn. Authored by d47 and never assembled from journal
    /// or comms text — untrusted content reaches these prompts as game state, below the
    /// breakpoint, never as the instruction itself (architecture.md §7).
    /// </param>
    /// <param name="gameState">Live state for the line to be about. Untrusted, and positioned as such.</param>
    /// <param name="maxOutputTokens">
    /// The ceiling on the whole completion, which is <see cref="AnswerBudget"/> plus
    /// <see cref="ReasoningHeadroom"/> by default and is raised only by the adventure generator,
    /// whose answer is a whole story in JSON.
    /// </param>
    /// <param name="effort">Low for a remark; the generator asks for more, because a story is a reasoning problem.</param>
    /// <param name="webSearch">
    /// Whether the provider may search the web while writing this line (list.md Phase 23, "Look
    /// it up, and say where the answer came from").
    /// <para>
    /// The caller decides, and has to have checked both halves first —
    /// <see cref="LlmCapabilities.SupportsWebSearch"/> and the Commander's own setting — exactly
    /// as <see cref="TurnLoop"/> does. A request declaring a tool the endpoint does not offer
    /// fails outright rather than degrading.
    /// </para>
    /// <para>
    /// <b>What comes back is a search result and is never anything else.</b> It is prose in a
    /// turn: no row is written from it, and there is no code path here that could — which is what
    /// keeps the standing rule a property of the design rather than a policy somebody remembers.
    /// It is also untrusted, in the ordinary way that everything a model returns is.
    /// </para>
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
        ThinkingEffort effort = ThinkingEffort.Low)
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
            // Low unless the caller says otherwise. A one-line remark in character is not a
            // reasoning problem, and spending Max effort on ambient chatter would cost more than
            // the turns the Commander actually asked for; a whole story is the one exception.
            Effort = effort,

            // Short on purpose, and now with room to think first
            // (<a href="https://github.com/dseelinger/d47/issues/97">#97</a>). See AnswerBudget.
            MaxOutputTokens = ceiling,

            // Off for every line that came before Phase 23. A remark about being in supercruise
            // has nothing to look up, and a search declared on a prompt that never needs one is
            // a different cached prefix for no gain.
            WebSearch = webSearch,
            Prompt = new PromptAssembly
            {
                // No tools. There is nothing here for the model to do except speak, and an
                // empty position 1 is also what keeps this prompt's prefix distinct from the
                // conversation's rather than fighting it for the same cache entry.
                Persona = persona,
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
                coldPrefixExpected: true,
                provider.Id,
                chosenModel);
        }

        var line = reply.ToString().Trim();

        // **A truncated turn is not a declined one, and used to be indistinguishable from it**
        // (#97). Both ended as `null` and nothing was written down, so a model that ran out of
        // budget mid-thought looked exactly like a model with nothing to say — and the authored
        // fallback played either way. The stop reason has always been in hand here and was read
        // only for a refusal.
        //
        // Warning rather than Debug: the line the Commander was meant to hear did not happen, the
        // cause is a number in this file, and nothing else anywhere will mention it.
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
