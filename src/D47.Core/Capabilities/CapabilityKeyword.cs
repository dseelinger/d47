using System.Collections.ObjectModel;

namespace D47.Core.Capabilities;

/// <summary>
/// One phrase in the model-free router's vocabulary, and — where the capability has more than one
/// answer to give — which of its tools that phrase means (#161).
/// </summary>
/// <param name="Phrase">What the Commander says.</param>
/// <param name="ToolName">The tool that phrase means, or null to mean the capability.</param>
public sealed record CapabilityKeyword(string Phrase, string? ToolName = null)
{
    /// <summary>
    /// The arguments this phrase means, where the named tool answers more than one question (#406).
    /// </summary>
    public IReadOnlyDictionary<string, string> Arguments { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;

    /// <summary>
    /// So a capability with one answer still declares <c>["where am i", "what system"]</c> and nothing
    /// about this type appears in it.
    /// </summary>
    public static implicit operator CapabilityKeyword(string phrase) => new(phrase);

    public override string ToString() => Phrase;
}
