namespace D47.Core.Conversation;

public enum ConversationRole
{
    User,
    Assistant,
}

/// <summary>One turn of conversation history. Text only until a phase needs more.</summary>
public sealed record ConversationMessage(ConversationRole Role, string Text);

/// <summary>
/// A tool as described <em>to the model</em> — no handler, no delegate. The registry owns
/// execution; the provider seam only ever sees the advertisement, which is also what keeps
/// the canonical schema bytes the single thing prompt caching depends on
/// (architecture.md §6).
/// </summary>
public sealed record ToolAdvertisement(string Name, string Description, string InputSchemaJson);
