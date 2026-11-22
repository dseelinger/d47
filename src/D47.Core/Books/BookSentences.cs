namespace D47.Core.Books;

/// <summary>Splits a paragraph into sentences for speaking. Titles such as "Mr." and initials such as "H. G." end no sentence.</summary>
public static class BookSentences
{
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr", "mrs", "ms", "messrs", "dr", "st", "jr", "sr", "prof", "rev", "hon", "gen", "col", "capt", "lt",
        "sgt", "maj", "cmdr", "adm", "gov", "sen", "rep", "mt", "ft", "vol", "ch", "vs", "etc", "esq",
        "viz", "cf", "approx", "dept", "inc", "ltd",
    };

    public static IReadOnlyList<string> Split(string paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        var sentences = new List<string>();
        var text = paragraph.Trim();
        var start = 0;
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] is not ('.' or '!' or '?'))
            {
                i++;
                continue;
            }

            var terminator = i;
            var end = i + 1;

            while (end < text.Length && text[end] is '.' or '!' or '?')
            {
                end++;
            }

            while (end < text.Length && IsCloser(text[end]))
            {
                end++;
            }

            if (end < text.Length && char.IsWhiteSpace(text[end]) && StartsASentence(text, end)
                && !(text[terminator] == '.' && end == terminator + 1 && EndsAnAbbreviation(text, start, terminator)))
            {
                sentences.Add(text[start..end].Trim());
                start = end;
            }

            i = end;
        }

        if (start < text.Length && text[start..].Trim() is { Length: > 0 } rest)
        {
            sentences.Add(rest);
        }

        return sentences;
    }

    private static bool IsCloser(char c) => c is '"' or '\'' or ')' or ']' or '”' or '’' or '»';

    private static bool StartsASentence(string text, int from)
    {
        var next = from;

        while (next < text.Length && char.IsWhiteSpace(text[next]))
        {
            next++;
        }

        if (next == text.Length)
        {
            return false;
        }

        var c = text[next];

        return char.IsUpper(c) || char.IsDigit(c) || c is '"' or '\'' or '(' or '[' or '“' or '‘' or '«';
    }

    /// <summary>Whether the word before the full stop at <paramref name="stop"/> is an initial (not "I"), a dotted abbreviation such as "e.g" or a known title.</summary>
    private static bool EndsAnAbbreviation(string text, int sentenceStart, int stop)
    {
        var wordStart = stop;

        while (wordStart > sentenceStart && !char.IsWhiteSpace(text[wordStart - 1]) && text[wordStart - 1] is not ('(' or '"' or '“'))
        {
            wordStart--;
        }

        var word = text[wordStart..stop];

        if (word.Length == 1 && char.IsUpper(word[0]) && word[0] != 'I')
        {
            return true;
        }

        if (word.Contains('.') && word.Split('.').All(part => part.Length is 1 or 2 && part.All(char.IsLetter)))
        {
            return true;
        }

        return Abbreviations.Contains(word);
    }
}
