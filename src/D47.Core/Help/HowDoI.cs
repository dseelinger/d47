using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;

namespace D47.Core.Help;

/// <summary>
/// Resolves "how do I ..." to a leaf of <see cref="HelpTaxonomy"/> by the content words the goal shares
/// with it, without a model (#170).
/// </summary>
public static class HowDoI
{
    private static readonly string[] Openers = ["how do i", "how can i", "how would i"];

    /// <summary>Words too common to tell one leaf's subject from another's.</summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "i", "im", "me", "my", "to", "for", "of", "in", "on", "at", "with",
        "do", "does", "did", "is", "are", "was", "were", "be", "been", "it", "its", "this",
        "that", "these", "those", "and", "or", "but", "so", "as", "from", "about", "into",
        "onto", "up", "out", "get", "got", "can", "could", "would", "should", "will", "how",
        "what", "where", "when", "which", "who", "you", "your", "please", "just", "some", "any",
        "one", "d47", "directive", "say", "says", "report", "reports", "keep",
    };

    /// <summary>How many content words a goal must share with a leaf to count as one it means.</summary>
    internal const int MinSharedWords = 2;

    /// <summary>
    /// The words after one of the three openers, or null when the utterance, with
    /// <see cref="SpokenOpeners"/> stripped, does not open with one.
    /// </summary>
    public static string? GoalFor(string utterance)
    {
        var said = SpokenOpeners.Strip(KeywordRouter.Utterance(utterance));

        foreach (var opener in Openers)
        {
            if (string.Equals(said, opener, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var prefix = opener + " ";

            if (said.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return said[prefix.Length..];
            }
        }

        return null;
    }

    /// <summary>
    /// The leaves the goal could mean, best first: at least two shared content words, or every content
    /// word of a goal that has only one.
    /// </summary>
    public static IReadOnlyList<HelpNode> Match(string goal, CapabilityRegistry registry)
    {
        var goalWords = ContentWords(goal);

        if (goalWords.Count == 0)
        {
            return [];
        }

        var threshold = Math.Min(goalWords.Count, MinSharedWords);

        return
        [
            .. HelpTaxonomy.Leaves()
                .Select(leaf => (Leaf: leaf, Shared: ContentWords(LeafText(leaf, registry)).Intersect(
                    goalWords, StringComparer.OrdinalIgnoreCase).Count()))
                .Where(scored => scored.Shared >= threshold)
                .OrderByDescending(scored => scored.Shared)
                .Select(scored => scored.Leaf),
        ];
    }

    /// <summary>A leaf's name, sentence, and everything the Commander could say to reach its capability.</summary>
    private static string LeafText(HelpNode leaf, CapabilityRegistry registry)
    {
        var descriptor = registry.Find(leaf.CapabilityId!)?.Descriptor;

        var phrases = descriptor is null
            ? []
            : HelpCapability.Phrases(descriptor);

        return string.Join(' ', new[] { leaf.Name, leaf.Sentence }.Concat(phrases));
    }

    /// <summary>Content words, stopwords out and a trailing plural folded off.</summary>
    internal static HashSet<string> ContentWords(string text) => new(
        KeywordRouter.Words(text)
            .Select(Fold)
            .Where(word => word.Length > 0 && !Stopwords.Contains(word)),
        StringComparer.OrdinalIgnoreCase);

    private static string Fold(string word) =>
        word.Length > 3 && word.EndsWith('s') ? word[..^1] : word;
}
