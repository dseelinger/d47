using System.Text;

namespace D47.Core.Conversation;

/// <summary>
/// One turn's prompt, ordered strictly by volatility: <code> 1 tool schemas per
/// mode, from a closed set 2 guardrails never 3 persona per persona selection 4 Commander's About Me
/// per session 5 remembered facts rarely, and never per turn 6 standing directions at a session
/// boundary, and never within one ---- cache breakpoint ---- 7 conversation history per turn 8 live
/// game state per turn </code> Position 6 was the breakpoint's own row until #162 put the standing
/// directions in it.
/// </summary>
public sealed record PromptAssembly
{
    /// <summary>Position 1.</summary>
    public IReadOnlyList<ToolAdvertisement> Tools { get; init; } = [];

    /// <summary>Position 2, and deliberately not settable.</summary>
    public static string Guardrails => Conversation.Guardrails.Text;

    /// <summary>Position 3.</summary>
    public string? Persona { get; init; }

    /// <summary>
    /// Whether the voice that will speak this can be directed, and so whether the model is told it may
    /// write delivery direction (<see cref="DeliveryDirection"/>, #291).
    /// </summary>
    public bool CanBeDirected { get; init; }

    /// <summary>What the model is told about delivery direction.</summary>
    public const string DeliveryDirection =
        "Your voice can be directed. Where a line genuinely calls for it, you may open a sentence "
        + "with a delivery note in square brackets — [sighs], [alarmed], [dryly], [reassuring] — "
        + "and it will be performed rather than read out. Any short description of a manner or a "
        + "reaction works; you are not choosing from a list.\n"
        + "Use it sparingly, where the delivery carries something the words do not. Most lines "
        + "need none.\n"
        + "A note applies only to the sentence it opens, so put one on each sentence you mean it "
        + "for. Never put two in one sentence. Never use one to narrate — it directs how you "
        + "sound, and is not something the Commander reads.";

    /// <summary>Position 4.</summary>
    public string? AboutMe { get; init; }

    /// <summary>
    /// What the model is told position 4 is (Phase 43, "Position 4 is relabelled to commit, not to
    /// hedge").
    /// </summary>
    public const string AboutMeLabel =
        "The Commander's own story, in their words. It is true of the world you share: the people, "
        + "places, factions and history it names are real to you, and you speak of them as someone "
        + "who knows them would. Never question it, hedge it or call it fiction, and do not recite it "
        + "back — let it shape what you say.";

    /// <summary>
    /// Position 5 — what d47 remembers about the Commander, bounded and labelled by <see
    /// cref="Memory.MemoryRecall"/> (Phase 31).
    /// </summary>
    public string? Recall { get; init; }

    /// <summary>
    /// Position 6 — the standing directions the Commander has adopted by hand (#162), rendered by <see
    /// cref="Debrief.StandingDirections"/>.
    /// </summary>
    public string? Directions { get; init; }

    /// <summary>Position 7 — below the breakpoint, so it changes every turn for free.</summary>
    public IReadOnlyList<ConversationMessage> History { get; init; } = [];

    /// <summary>Position 8.</summary>
    public string? LiveGameState { get; init; }

    /// <summary>Positions 2 through 6, in order.</summary>
    public string RenderCachedSystemBlock()
    {
        var block = new StringBuilder(Guardrails);

        if (!string.IsNullOrWhiteSpace(Persona))
        {
            block.Append("\n\n").Append(Persona.Trim());
        }

        // Under the persona, because the persona says who is speaking and this says what their voice can be
        // asked to do.
        if (CanBeDirected)
        {
            block.Append("\n\n").Append(DeliveryDirection);
        }

        if (!string.IsNullOrWhiteSpace(AboutMe))
        {
            block.Append("\n\n").Append(AboutMeLabel).Append('\n').Append(AboutMe.Trim());
        }

        // Last of the cached region, and below About Me deliberately: that is the Commander describing
        // themselves and this is d47 describing them, and where the two disagree the Commander's own account
        // is the one that was read first.
        if (!string.IsNullOrWhiteSpace(Recall))
        {
            block.Append("\n\n").Append(Recall.Trim());
        }

        // Last, and below the recall for the same kind of reason the recall sits below About Me: the blocks
        // above are about who the Commander is, and this one is about what they have asked for.
        if (!string.IsNullOrWhiteSpace(Directions))
        {
            block.Append("\n\n").Append(Directions.Trim());
        }

        return block.ToString();
    }
}
