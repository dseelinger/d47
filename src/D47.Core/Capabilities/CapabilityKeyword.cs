namespace D47.Core.Capabilities;

/// <summary>
/// One phrase in the model-free router's vocabulary, and — where the capability has more than one
/// answer to give — which of its tools that phrase means (#161).
/// <para>
/// <b>A keyword used to name a capability and nothing more</b>, and the router then took that
/// capability's first tool with no required parameters. The pick was positional: whichever tool
/// happened to be declared first. That is how <i>"what's the Cobra Mk III's jump range?"</i> was
/// answered with where the Commander was standing — <c>jump range</c> reached Journal and Journal
/// declares <c>get_location</c> first. It is also how <i>"where is my fleet carrier"</i> was
/// answered the same way in 2026-08-21, and the fix then was declared phrases on the tools, which
/// only ever covers the exact wordings somebody wrote down.
/// </para>
/// <para>
/// <b>Twelve capabilities had the same trapdoor waiting</b>, measured across the registry on
/// 2026-08-28, and one had already fallen through it: <c>which model</c> reached Conversation,
/// whose first eligible tool is <c>cancel_turn</c>, so asking which model was running cancelled
/// the turn instead of answering. Nothing in the declaration said so.
/// </para>
/// <para>
/// <b>So the keyword names the tool.</b> An entry written as a bare string still means the whole
/// capability, which costs nothing where the capability has one eligible tool — that is most of
/// them, and it keeps the common declaration a list of strings. Where there are several and none
/// was named, <see cref="Conversation.KeywordRouter"/> <em>declines</em> rather than guessing:
/// falling through to the model is the ordinary outcome for a phrasing nobody wrote down, and a
/// model answering slowly beats a router answering wrongly.
/// </para>
/// </summary>
/// <param name="Phrase">What the Commander says.</param>
/// <param name="ToolName">
/// The tool that phrase means, or null to mean the capability. Null is only answerable where the
/// capability offers exactly one tool the router could call.
/// </param>
public sealed record CapabilityKeyword(string Phrase, string? ToolName = null)
{
    /// <summary>
    /// So a capability with one answer still declares <c>["where am i", "what system"]</c> and
    /// nothing about this type appears in it.
    /// </summary>
    public static implicit operator CapabilityKeyword(string phrase) => new(phrase);

    public override string ToString() => Phrase;
}
