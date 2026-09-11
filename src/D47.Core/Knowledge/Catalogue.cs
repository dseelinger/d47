namespace D47.Core.Knowledge;

/// <summary>
/// Matching something a Commander said against a closed list of names the search service actually
/// honours.
/// </summary>
public static class Catalogue
{
    /// <summary>The catalogue name for something the Commander said, or null.</summary>
    public static string? Match(IReadOnlyList<string> catalogue, string spoken)
    {
        var wanted = spoken.Trim();

        var exact = catalogue.FirstOrDefault(name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        var relaxed = Relax(wanted);

        if (relaxed.Length == 0)
        {
            return null;
        }

        var loose = catalogue.FirstOrDefault(name => string.Equals(Relax(name), relaxed, StringComparison.Ordinal));

        if (loose is not null)
        {
            return loose;
        }

        // Then without the bit in quotes. "Tod 'The Blaster' McQuinn" is how the id list spells him and "Tod
        // McQuinn" is what a person says; nothing above joins those two.
        var bare = Bare(wanted);

        var nicknamed = catalogue.FirstOrDefault(name =>
            string.Equals(Bare(name), bare, StringComparison.Ordinal));

        if (nicknamed is not null)
        {
            return nicknamed;
        }

        // Deliberately not the shortest containing name, nor the first: an ambiguous fragment has no right
        // answer, and picking one silently is how "gas giant" becomes "Class I gas giant" and the Commander
        // is told about the wrong thing with total confidence.
        var containing = catalogue
            .Where(name => Relax(name).Contains(relaxed, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return containing.Length == 1 ? containing[0] : null;
    }

    internal static string Relax(string text) =>
        new([.. text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    /// <summary>Relaxed, with any quoted aside removed.</summary>
    private static string Bare(string text)
    {
        if (text.Count(IsQuote) is not (> 0 and var quotes) || quotes % 2 != 0)
        {
            return Relax(text);
        }

        var kept = new List<char>(text.Length);
        var inside = false;

        foreach (var character in text)
        {
            if (IsQuote(character))
            {
                inside = !inside;
            }
            else if (!inside)
            {
                kept.Add(character);
            }
        }

        return Relax(new string([.. kept]));
    }

    private static bool IsQuote(char character) => character is '\'' or '‘' or '’' or '"';

    /// <summary>Names close enough to be worth offering back, best first.</summary>
    public static IReadOnlyList<string> Near(IReadOnlyList<string> catalogue, string spoken)
    {
        var relaxed = Relax(spoken);

        if (relaxed.Length < 3)
        {
            return [];
        }

        var fragments = catalogue
            .Where(name =>
            {
                var candidate = Relax(name);

                // A catalogue name under four letters is a fragment of almost anything spoken that contains
                // it — "Ra" inside "Shinrata Desra" — so below that floor it has to be the whole word (#36).
                if (candidate.Length < 4)
                {
                    return candidate == relaxed;
                }

                return candidate.Contains(relaxed, StringComparison.Ordinal)
                       || relaxed.Contains(candidate, StringComparison.Ordinal);
            })
            .ToList();

        // Scaled to the length of what was said: two wrong characters in "Sensors" is a different word, while
        // two in "Mk II Ablative Military Grade Composite" is a transcription.
        var budget = Math.Clamp(relaxed.Length / 6, 1, 4);

        var misspellings = catalogue
            .Except(fragments)
            .Select(name =>
            {
                var candidate = Relax(name);

                // A long compound name has more positions where a hearing error can occur, so it is allowed
                // one more edit than the length-scaled budget below would otherwise give it (#36).
                var allowance = candidate.Length >= 12 ? budget + 1 : budget;

                return (Name: name, Distance: Distance(candidate, relaxed, allowance), Allowance: allowance);
            })
            .Where(candidate => candidate.Distance <= candidate.Allowance)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Name);

        return [.. fragments.Concat(misspellings).Take(5)];
    }

    /// <summary>The same, with a third rung for names that arrived through a microphone (#134).</summary>
    public static IReadOnlyList<string> NearSpoken(IReadOnlyList<string> catalogue, string spoken)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        var written = Near(catalogue, spoken);

        if (written.Count >= 5 || Sound(spoken) is not { Length: > 0 } key)
        {
            return written;
        }

        var relaxed = Relax(spoken);

        var sounded = catalogue
            .Except(written)
            .Where(name => string.Equals(Sound(name), key, StringComparison.Ordinal))
            .OrderBy(name => Distance(Relax(name), relaxed, int.MaxValue))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);

        return [.. written.Concat(sounded).Take(5)];
    }

    /// <summary>What a name sounds like, as a Soundex key (#134).</summary>
    public static string Sound(string name)
    {
        var relaxed = Relax(name ?? string.Empty);

        if (relaxed.Length == 0)
        {
            return string.Empty;
        }

        var key = new System.Text.StringBuilder(4);
        key.Append(char.ToUpperInvariant(relaxed[0]));

        var previous = Code(relaxed[0]);

        foreach (var letter in relaxed.Skip(1))
        {
            var code = Code(letter);

            if (code != '0' && code != previous)
            {
                key.Append(code);

                if (key.Length == 4)
                {
                    break;
                }
            }

            // H and W are transparent: they do not code, and they do not break a run either, so "Ashcroft"
            // keys its two consonants as one the way it is said.
            if (letter is not ('h' or 'w'))
            {
                previous = code;
            }
        }

        return key.Append('0', 4 - key.Length).ToString();
    }

    private static char Code(char letter) => letter switch
    {
        'b' or 'f' or 'p' or 'v' => '1',
        'c' or 'g' or 'j' or 'k' or 'q' or 's' or 'x' or 'z' => '2',
        'd' or 't' => '3',
        'l' => '4',
        'm' or 'n' => '5',
        'r' => '6',
        _ => '0',
    };

    /// <summary>Levenshtein distance, abandoned once it passes <paramref name="budget"/>.</summary>
    private static int Distance(string left, string right, int budget)
    {
        if (Math.Abs(left.Length - right.Length) > budget)
        {
            return budget + 1;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            var best = current[0];

            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);

                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
                best = Math.Min(best, current[j]);
            }

            if (best > budget)
            {
                return budget + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    /// <summary>The sentence a rejected name gets.</summary>
    public static string Unknown(string kind, string spoken, IReadOnlyList<string> near) =>
        near.Count > 0
            ? $"I don't know a {kind} called '{spoken}'. Did you mean {string.Join(", ", near)}?"
            : $"I don't know a {kind} called '{spoken}'.";
}
