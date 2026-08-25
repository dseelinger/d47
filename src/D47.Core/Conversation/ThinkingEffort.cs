namespace D47.Core.Conversation;

/// <summary>
/// Per-turn reasoning effort (list.md Phase 3, "Model Level and Thinking").
/// <para>
/// There is deliberately no <c>Off</c> or <c>None</c> member. The checklist allows "low
/// through max, but no 'off' unless the LLM is set to none" — and the LLM being set to none
/// means there is no provider to ask, so there is no effort to choose. Omitting the member is
/// how that is enforced rather than remembered.
/// </para>
/// <para>
/// <b>Declaration order is the ladder</b> (list.md Phase 54). The clamp that a floor and a
/// ceiling apply compares these as they are written, so inserting a member in the wrong place
/// silently reorders the rungs. Ordinal churn is otherwise safe and was checked before
/// <c>Xhigh</c> went in: settings serialise the enum as camelCase strings, the spend ledger
/// records no effort at all, and there is no <c>(int)</c> cast on it anywhere in <c>src/</c>.
/// </para>
/// <para>
/// <b><c>Xhigh</c> sits between High and Max, and the note that used to stand here was wrong.</b>
/// It claimed the C# SDK did not expose the level. On the pinned <c>Anthropic 12.40.0</c> its
/// <c>Effort</c> enum is <c>{ Low, Medium, High, Xhigh, Max }</c>, so the intersection this set
/// was described as has not been the binding constraint for some time. Anthropic translates it
/// straight through; both OpenAI providers map it down to <c>"high"</c> beside <c>Max</c>, which
/// is the safe direction and can be raised against a real 200 rather than against a guess.
/// </para>
/// <para>
/// <b>No <c>EffortRouter</c> case chooses it.</b> The router keeps its four outputs, so this
/// member is reachable only by a Commander setting a bound — which is what makes the change
/// provably additive and leaves the router's own tests untouched.
/// </para>
/// </summary>
public enum ThinkingEffort
{
    Low,
    Medium,
    High,
    Xhigh,
    Max,
}
