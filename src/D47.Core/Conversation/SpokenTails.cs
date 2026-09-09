namespace D47.Core.Conversation;

/// <summary>
/// The words a Commander puts on the end of a command without changing it — "again", "please", "now".
/// </summary>
public static class SpokenTails
{
    /// <summary>Every tail, each opening with the space that separates it from what comes before.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        " thank you",
        " right now",
        " if you would",
        " for me",
        " please",
        " thanks",
        " again",
        " now",
    ];

    /// <summary>The utterance with one trailing tail removed, or unchanged where it ends with none.</summary>
    public static string Strip(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return utterance;
        }

        foreach (var tail in All)
        {
            if (utterance.Length > tail.Length
                && utterance.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
            {
                return utterance[..^tail.Length];
            }
        }

        return utterance;
    }
}
