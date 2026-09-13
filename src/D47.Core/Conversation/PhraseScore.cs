namespace D47.Core.Conversation;

/// <summary>How closely an utterance matches a declared phrase.</summary>
public enum PhraseMatch
{
    /// <summary>The same words once folded through the equivalence table.</summary>
    Equivalent,

    /// <summary>One word away, against a phrase of three words or more.</summary>
    Near,
}

/// <summary>One phrase a scoring pass matched, in rank order.</summary>
public sealed record PhraseRank(string Phrase, PhraseMatch Match);

/// <summary>
/// Scores an utterance against a declared phrase, over strings alone — nothing here routes on the
/// result (#164).
/// </summary>
public static class PhraseScore
{
    /// <summary>
    /// Words or short phrases a Commander says interchangeably. The first entry of a group is what
    /// every member folds to.
    /// </summary>
    private static readonly string[][] EquivalenceClasses =
    [
        ["to", "on", "into", "onto"],
        ["elite", "the game", "game"],
        ["set", "put"],
    ];

    private static readonly IReadOnlyList<(string Canonical, string[] Words)> Foldings =
        [.. EquivalenceClasses
            .SelectMany(group => group.Select(member => (Canonical: group[0], Words: member.Split(' '))))
            .OrderByDescending(entry => entry.Words.Length)];

    /// <summary>The equivalence table itself, for tests that walk it rather than score against it.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> EquivalenceTable => EquivalenceClasses;

    /// <summary>Scores one utterance against one phrase.</summary>
    public static PhraseMatch? Score(string utterance, string phrase)
    {
        var utteranceWords = KeywordRouter.Words(KeywordRouter.Utterance(utterance));
        var phraseWords = KeywordRouter.Words(KeywordRouter.Utterance(phrase));

        var foldedUtterance = Fold(utteranceWords);
        var foldedPhrase = Fold(phraseWords);

        if (foldedUtterance.SequenceEqual(foldedPhrase, StringComparer.OrdinalIgnoreCase))
        {
            return PhraseMatch.Equivalent;
        }

        if (utteranceWords.Length <= 1 || phraseWords.Length < 3)
        {
            return null;
        }

        return WordEditDistance(foldedUtterance, foldedPhrase) == 1 ? PhraseMatch.Near : null;
    }

    /// <summary>
    /// Every phrase that scores against the utterance, Equivalent before Near and in declaration
    /// order within a tier.
    /// </summary>
    public static IReadOnlyList<PhraseRank> Rank(string utterance, IEnumerable<string> phrases) =>
        [.. phrases
            .Select(phrase => (Phrase: phrase, Match: Score(utterance, phrase)))
            .Where(scored => scored.Match is not null)
            .OrderBy(scored => scored.Match == PhraseMatch.Equivalent ? 0 : 1)
            .Select(scored => new PhraseRank(scored.Phrase, scored.Match!.Value))];

    /// <summary>How many words apart two utterances are as said, before any folding.</summary>
    internal static int Distance(string utterance, string phrase) =>
        WordEditDistance(
            KeywordRouter.Words(KeywordRouter.Utterance(utterance)),
            KeywordRouter.Words(KeywordRouter.Utterance(phrase)));

    /// <summary>
    /// The word sequence with every run matching an equivalence class member replaced by that
    /// class's canonical word, and a trailing plural "s" dropped from the last word.
    /// </summary>
    private static string[] Fold(IReadOnlyList<string> words)
    {
        var folded = new List<string>(words.Count);

        var i = 0;

        while (i < words.Count)
        {
            var folding = Foldings.FirstOrDefault(entry =>
                i + entry.Words.Length <= words.Count
                && entry.Words.Select((w, j) => (w, j))
                    .All(pair => string.Equals(pair.w, words[i + pair.j], StringComparison.OrdinalIgnoreCase)));

            if (folding.Words is not null)
            {
                folded.Add(folding.Canonical);
                i += folding.Words.Length;
            }
            else
            {
                folded.Add(words[i]);
                i += 1;
            }
        }

        if (folded.Count > 0 && folded[^1].Length > 1 && folded[^1].EndsWith('s'))
        {
            folded[^1] = folded[^1][..^1];
        }

        return [.. folded];
    }

    /// <summary>The number of single-word insertions, deletions or substitutions between two sequences.</summary>
    private static int WordEditDistance(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var previous = new int[b.Count + 1];
        var current = new int[b.Count + 1];

        for (var j = 0; j <= b.Count; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Count; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Count; j++)
            {
                var cost = string.Equals(a[i - 1], b[j - 1], StringComparison.OrdinalIgnoreCase) ? 0 : 1;

                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Count];
    }
}
