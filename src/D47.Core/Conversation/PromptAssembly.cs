using System.Text;

namespace D47.Core.Conversation;

/// <summary>
/// One turn's prompt, ordered strictly by volatility (architecture.md §6):
/// <code>
/// 1  tool schemas          per mode, from a closed set
/// 2  guardrails            never
/// 3  persona               per persona selection
/// 4  Commander's About Me  per session
/// 5  remembered facts      rarely, and never per turn
///    ---- cache breakpoint ----
/// 7  conversation history  per turn
/// 8  live game state       per turn
/// </code>
/// The order is the type's contract, not a convention a caller has to remember. Everything at
/// or above the breakpoint is rendered by <see cref="RenderCachedSystemBlock"/>; everything
/// below it is a separate property the provider attaches after the breakpoint.
/// </summary>
public sealed record PromptAssembly
{
    /// <summary>
    /// Position 1. Serialized first by the API, so a per-turn change here invalidates the
    /// <em>entire</em> prefix rather than a tail of it — which is why the checklist quantizes
    /// these into a closed set of profiles instead of choosing tools per turn.
    /// </summary>
    public IReadOnlyList<ToolAdvertisement> Tools { get; init; } = [];

    /// <summary>
    /// Position 2, and deliberately not settable. Exposing a setter would be exposing a way to
    /// strip the guardrails; there is no code path that can vary this text.
    /// </summary>
    public static string Guardrails => Conversation.Guardrails.Text;

    /// <summary>Position 3. Null is "personality off" — which cannot reach position 2.</summary>
    public string? Persona { get; init; }

    /// <summary>
    /// Position 4. The Commander's own account of themselves — the character sheet and, when
    /// the caller chose to carry it, the story, composed by <see cref="CommanderStory"/>.
    /// </summary>
    public string? AboutMe { get; init; }

    /// <summary>
    /// What the model is told position 4 is (Phase 43, "Position 4 is relabelled to
    /// commit, not to hedge").
    /// <para>
    /// A Commander's biography asserts things about named in-game figures that are their own
    /// invention rather than Frontier's lore, and <b>the answer is not a disclaimer</b>: it is their
    /// character in their game and the field takes whatever they write. So the label says this is
    /// true of the world the two of them share, and the ship's AI inhabits it — an AI that speaks
    /// of a Power with suspicion because its Commander has reason to is the feature. Operational
    /// answers are unaffected: those come from tools and tables, not from a prompt block.
    /// </para>
    /// </summary>
    public const string AboutMeLabel =
        "The Commander's own story, in their words. It is true of the world you share: the people, "
        + "places, factions and history it names are real to you, and you speak of them as someone "
        + "who knows them would. Never question it, hedge it or call it fiction, and do not recite it "
        + "back — let it shape what you say.";

    /// <summary>
    /// Position 5 — what d47 remembers about the Commander, bounded and labelled by
    /// <see cref="Memory.MemoryRecall"/> (Phase 31).
    /// <para>
    /// <b>Above the breakpoint, which is a decision rather than a default.</b> The obvious placement
    /// is game state, because that is where changing things go — and it is wrong twice over.
    /// Memories change rarely, so paying for them once and reading them cached is strictly cheaper;
    /// and a block that moved every turn would invalidate the whole prefix rather than a tail of it,
    /// taking 39,000-odd bytes of tool schema cold with it every time. It goes beside
    /// <see cref="AboutMe"/> because it is the same kind of thing: standing truth about the person
    /// flying.
    /// </para>
    /// <para>
    /// The caller is responsible for not reassigning this with text that has not changed — see
    /// <see cref="Memory.MemoryRecall"/>, which is where that obligation and the three things that
    /// make it keepable are written down.
    /// </para>
    /// </summary>
    public string? Recall { get; init; }

    /// <summary>Position 7 — below the breakpoint, so it changes every turn for free.</summary>
    public IReadOnlyList<ConversationMessage> History { get; init; } = [];

    /// <summary>
    /// Position 8. Where the provider puts this depends on what the endpoint supports: a
    /// <c>{"role":"system"}</c> message carries operator authority and is the preferred path,
    /// with a <c>&lt;system-reminder&gt;</c> block in the user turn as the fallback. Either way
    /// it sits after the breakpoint. Journal-derived and therefore untrusted (§7).
    /// </summary>
    public string? LiveGameState { get; init; }

    /// <summary>
    /// Positions 2 through 5, in order. This is the text the cache breakpoint is placed on, so
    /// its bytes must not vary for reasons other than a persona, an About Me or a recall change.
    /// </summary>
    public string RenderCachedSystemBlock()
    {
        var block = new StringBuilder(Guardrails);

        if (!string.IsNullOrWhiteSpace(Persona))
        {
            block.Append("\n\n").Append(Persona.Trim());
        }

        if (!string.IsNullOrWhiteSpace(AboutMe))
        {
            block.Append("\n\n").Append(AboutMeLabel).Append('\n').Append(AboutMe.Trim());
        }

        // Last of the cached region, and below About Me deliberately: that is the Commander
        // describing themselves and this is d47 describing them, and where the two disagree the
        // Commander's own account is the one that was read first.
        if (!string.IsNullOrWhiteSpace(Recall))
        {
            block.Append("\n\n").Append(Recall.Trim());
        }

        return block.ToString();
    }
}
