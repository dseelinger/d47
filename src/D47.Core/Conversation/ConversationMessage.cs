namespace D47.Core.Conversation;

public enum ConversationRole
{
    User,
    Assistant,
}

/// <summary>One piece of a message.</summary>
public abstract record ConversationContent
{
    private ConversationContent()
    {
    }

    public sealed record Text(string Value) : ConversationContent;

    /// <summary>The model asking for a tool to be run.</summary>
    public sealed record ToolUse(string Id, string Name, string InputJson) : ConversationContent;

    /// <summary>The answer to one <see cref="ToolUse"/>, matched by <paramref name="ToolUseId"/>.</summary>
    public sealed record ToolResult(string ToolUseId, string Content, bool IsError) : ConversationContent
    {
        /// <summary>A picture sent with the content. Never stored past the turn that produced it.</summary>
        public ImageAttachment? Image { get; init; }
    }

    /// <summary>
    /// A block Core does not read, sent back verbatim and in place to the provider named by
    /// <paramref name="ProviderId"/> and to no other. Never drawn or spoken.
    /// </summary>
    public sealed record Opaque(string ProviderId, string Json) : ConversationContent;

    /// <summary>
    /// A reasoning block, sent back verbatim and in place to <paramref name="ProviderId"/> on the later rounds
    /// of the turn that produced it. Never stored past that turn.
    /// </summary>
    public sealed record ThinkingBlock(string ProviderId, string Json) : ConversationContent;

    /// <summary>
    /// <see cref="PromptAssembly.TrailingState"/> as an earlier round of this turn sent it, rendered after the
    /// rest of its message so later rounds send the same prefix. Never stored past that turn.
    /// </summary>
    public sealed record TrailingState(string Value) : ConversationContent;
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
    /// transcript, the panel, the flavour turn.
    /// </summary>
    public string Text => string.Concat(Content.OfType<ConversationContent.Text>().Select(part => part.Value));
}

/// <summary>
/// A tool as described to the model — no handler, no delegate. A <paramref name="Deferred"/> tool is loaded
/// only when the provider's tool search finds it; a <paramref name="ReturnsImage"/> tool is advertised only
/// where the model reads pictures.
/// </summary>
public sealed record ToolAdvertisement(
    string Name,
    string Description,
    string InputSchemaJson,
    bool Deferred = false,
    bool ReturnsImage = false);
