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
    /// transcript, the panel, the flavour turn.
    /// </summary>
    public string Text => string.Concat(Content.OfType<ConversationContent.Text>().Select(part => part.Value));
}

/// <summary>A tool as described to the model — no handler, no delegate.</summary>
public sealed record ToolAdvertisement(string Name, string Description, string InputSchemaJson);
