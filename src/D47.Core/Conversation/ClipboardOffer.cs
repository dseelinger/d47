namespace D47.Core.Conversation;

/// <summary>
/// The thing d47 last offered to put on the Commander's clipboard, so "copy that" has something to mean
/// (asked for 2026-08-21).
/// </summary>
public sealed class ClipboardOffer
{
    /// <summary>What would go on the clipboard — a system name, almost always.</summary>
    public string? Text { get; private set; }

    /// <summary>What it is, in the words the offer sentence used: "the system", "the trader's system".</summary>
    public string? Subject { get; private set; }

    public bool IsStanding => Text is { Length: > 0 };

    /// <summary>Records what is being offered and returns the sentence to say.</summary>
    public string? Offer(string? text, string subject)
    {
        if (text is not { Length: > 0 } wanted)
        {
            Text = null;
            Subject = null;
            return null;
        }

        Text = wanted;
        Subject = subject;

        return $"Say \"copy that\" and I will put {wanted} on your clipboard.";
    }

    /// <summary>
    /// An offer stands until the next answer replaces it, rather than being spent when it is taken up.
    /// </summary>
    public IEnumerable<DynamicCommand> Phrases()
    {
        if (Text is not { Length: > 0 } text)
        {
            yield break;
        }

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["text"] = text };

        foreach (var phrase in Taking)
        {
            yield return new DynamicCommand(
                phrase, Capabilities.Builtin.NavigationCapability.Id, "copy_to_clipboard", arguments);
        }
    }

    /// <summary>The phrases this can claim, whether or not an offer is standing right now.</summary>
    public static IReadOnlyList<string> EveryPhrase => Taking;

    private static readonly string[] Taking =
    [
        "copy that", "copy it", "copy that one", "copy the system", "copy that system",
        "put it on my clipboard", "put that on my clipboard", "copy to clipboard",
        "yes copy it", "yes please copy it", "copy that please",
    ];
}
