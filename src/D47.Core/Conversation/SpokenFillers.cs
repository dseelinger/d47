namespace D47.Core.Conversation;

/// <summary>
/// Single words a transcript can put in front of a command that the Commander did not say — so, again, um.
/// </summary>
public static class SpokenFillers
{
    /// <summary>Every filler, each ending in the space that separates it from what follows.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "again ",
        "okay ",
        "well ",
        "so ",
        "ok ",
        "and ",
        "um ",
        "uh ",
    ];

    /// <summary>The utterance with one leading filler removed, or unchanged where it opens with none.</summary>
    public static string Strip(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return utterance;
        }

        foreach (var filler in All)
        {
            if (utterance.Length > filler.Length
                && utterance.StartsWith(filler, StringComparison.OrdinalIgnoreCase))
            {
                return utterance[filler.Length..];
            }
        }

        return utterance;
    }
}
