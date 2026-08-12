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
/// The API also has an <c>xhigh</c> level between High and Max, which the C# SDK's enum does
/// not expose yet. This set is the intersection of what the checklist asks for and what the
/// SDK can send.
/// </para>
/// </summary>
public enum ThinkingEffort
{
    Low,
    Medium,
    High,
    Max,
}
