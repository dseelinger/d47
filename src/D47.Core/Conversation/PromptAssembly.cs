using System.Text;

namespace D47.Core.Conversation;

/// <summary>
/// One turn's prompt, ordered strictly by volatility (architecture.md §6):
/// <code>
/// 1  tool schemas          per mode, from a closed set
/// 2  guardrails            never
/// 3  persona               per persona selection
/// 4  Commander's About Me  per session
///    ---- cache breakpoint ----
/// 6  conversation history  per turn
/// 7  live game state       per turn
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

    /// <summary>Position 4. The Commander's own standing prompt about themselves.</summary>
    public string? AboutMe { get; init; }

    /// <summary>Position 6 — below the breakpoint, so it changes every turn for free.</summary>
    public IReadOnlyList<ConversationMessage> History { get; init; } = [];

    /// <summary>
    /// Position 7. Where the provider puts this depends on what the endpoint supports: a
    /// <c>{"role":"system"}</c> message carries operator authority and is the preferred path,
    /// with a <c>&lt;system-reminder&gt;</c> block in the user turn as the fallback. Either way
    /// it sits after the breakpoint. Journal-derived and therefore untrusted (§7).
    /// </summary>
    public string? LiveGameState { get; init; }

    /// <summary>
    /// Positions 2 through 4, in order. This is the text the cache breakpoint is placed on, so
    /// its bytes must not vary for reasons other than a persona or About Me change.
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
            block.Append("\n\nAbout the Commander, in their own words:\n").Append(AboutMe.Trim());
        }

        return block.ToString();
    }
}
