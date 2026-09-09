namespace D47.Core.Conversation;

/// <summary>
/// The words a Commander puts in front of the thing they actually want, and which mean nothing on their
/// own — show me the, go to, switch to, set.
/// </summary>
public static class SpokenOpeners
{
    /// <summary>Every opener, each ending in the space that separates it from what follows.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "take me to the ",
        "take me to ",
        "show me the ",
        "switch to the ",
        "show me ",
        "switch to ",
        "select the ",
        "go to the ",
        "show the ",
        "open the ",
        "set the ",
        "select ",
        "go to ",
        "show ",
        "open ",
        "set ",
    ];

    /// <summary>The utterance with one leading opener removed, or unchanged where it opens with none.</summary>
    public static string Strip(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return utterance;
        }

        foreach (var opener in All)
        {
            if (utterance.Length > opener.Length
                && utterance.StartsWith(opener, StringComparison.OrdinalIgnoreCase))
            {
                return utterance[opener.Length..];
            }
        }

        return utterance;
    }
}
