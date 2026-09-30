namespace D47.Core.Messages;

/// <summary>One written message from a speaker to the Commander.</summary>
public sealed record D47Message
{
    public string Key { get; init; } = string.Empty;

    public DateTimeOffset Sent { get; init; }

    /// <summary>A persona id, an NPC name, or <see cref="MessageStore.Narrator"/>.</summary>
    public string From { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    /// <summary>The adventure this message belongs to, when it is a beat.</summary>
    public string? AdventureKey { get; init; }

    public bool Read { get; init; }
}
