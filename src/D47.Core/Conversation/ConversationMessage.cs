namespace D47.Core.Conversation;

public enum ConversationRole
{
    User,
    Assistant,
}

/// <summary>
/// One piece of a message. A closed hierarchy — the private constructor means no other assembly
/// can add a case the provider seam has not been taught to render.
/// <para>
/// History stopped being "text only" when the model gained the ability to call a tool. An
/// agentic turn is not one question and one answer: it is an assistant message carrying
/// <see cref="ToolUse"/> blocks, a user message carrying the matching <see cref="ToolResult"/>
/// blocks, and then the real answer — and the pairing has to survive into the next turn's
/// history or the model is shown a call it never got an answer to.
/// </para>
/// </summary>
public abstract record ConversationContent
{
    private ConversationContent()
    {
    }

    public sealed record Text(string Value) : ConversationContent;

    /// <summary>
    /// The model asking for a tool to be run. <paramref name="InputJson"/> is the raw JSON object
    /// the model produced, kept verbatim rather than as parsed arguments: what goes back to the
    /// provider next turn has to be what it sent, and a round trip through
    /// <see cref="Capabilities.ToolArguments"/> would flatten every value to a string.
    /// </summary>
    public sealed record ToolUse(string Id, string Name, string InputJson) : ConversationContent;

    /// <summary>
    /// The answer to one <see cref="ToolUse"/>, matched by <paramref name="ToolUseId"/>.
    /// <para>
    /// <b>Untrusted.</b> A tool result is the far side of the trust boundary — a station name from
    /// Spansh, a web search result, a system name another Commander chose (architecture.md §7).
    /// It reaches the model as data, and nothing in it may be treated as instruction.
    /// </para>
    /// </summary>
    public sealed record ToolResult(string ToolUseId, string Content, bool IsError) : ConversationContent;
}

/// <summary>One turn of conversation history.</summary>
public sealed record ConversationMessage(ConversationRole Role, IReadOnlyList<ConversationContent> Content)
{
    /// <summary>The ordinary case: a message that is only prose.</summary>
    public ConversationMessage(ConversationRole role, string text)
        : this(role, [new ConversationContent.Text(text)])
    {
    }

    /// <summary>
    /// The prose of this message, for readers that only care about what was said — the persona
    /// transcript, the panel, the flavour turn. Tool blocks are deliberately invisible here:
    /// they are how an answer was reached, not part of it.
    /// </summary>
    public string Text => string.Concat(Content.OfType<ConversationContent.Text>().Select(part => part.Value));
}

/// <summary>
/// A tool as described <em>to the model</em> — no handler, no delegate. The registry owns
/// execution; the provider seam only ever sees the advertisement, which is also what keeps
/// the canonical schema bytes the single thing prompt caching depends on
/// (architecture.md §6).
/// </summary>
public sealed record ToolAdvertisement(string Name, string Description, string InputSchemaJson);
