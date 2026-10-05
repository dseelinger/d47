namespace D47.Core.Journal;

/// <summary>One part of a Commander's state that an event changed.</summary>
/// <param name="Part">The <see cref="CommanderGameState"/> property that changed.</param>
public sealed record FoldChange(string Part, string Phrase, string? Detail = null)
{
    /// <summary>The phrase, with the detail in parentheses where there is one.</summary>
    public string Said => Detail is { Length: > 0 } ? $"{Phrase} ({Detail})" : Phrase;
}

/// <summary>What one event changed in a Commander's state, in the order the fold applies the parts.</summary>
public sealed class FoldReceipt(IReadOnlyList<FoldChange> changes)
{
    /// <summary>An event that changed nothing, or that reached no Commander.</summary>
    public static readonly FoldReceipt Nothing = new([]);

    public IReadOnlyList<FoldChange> Changes { get; } = changes;

    /// <summary>The receipt as one line of words.</summary>
    public string Said => Changes.Count == 0
        ? "Nothing in d47's picture changed."
        : "This event updated: " + string.Join(" · ", Changes.Select(change => change.Said));
}
