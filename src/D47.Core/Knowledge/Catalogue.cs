namespace D47.Core.Knowledge;

/// <summary>
/// Matching something a Commander said against a closed list of names the search service
/// actually honours.
/// <para>
/// Extracted from <see cref="OutfittingCatalogue"/> when <see cref="BodyCatalogue"/> needed the
/// same three behaviours — exact, relaxed, and near-miss suggestions — for a different
/// vocabulary. The rules are the same because the failure is the same: a name arrives through a
/// transcriber, so it is either punctuated differently than it is written, shortened to what a
/// person would actually say, or misheard outright.
/// </para>
/// </summary>
public static class Catalogue
{
    /// <summary>
    /// The catalogue name for something the Commander said, or null.
    /// <para>
    /// Three passes, narrowest first. An <b>exact</b> match wins outright, so "Cargo Rack"
    /// cannot be captured by "Mk II Cargo Rack". A <b>relaxed</b> match ignores the punctuation
    /// nobody pronounces — the hyphen in "Multi-Cannon", the brackets in "Frame Shift Drive
    /// (SCO)". A <b>unique fragment</b> is accepted last and only when it is unique: "earth-like"
    /// names exactly one body subtype and is what a person says, while "world" names nine and is
    /// not an answer.
    /// </para>
    /// </summary>
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

        // Then without the bit in quotes. "Tod 'The Blaster' McQuinn" is how the id list spells
        // him and "Tod McQuinn" is what a person says; nothing above joins those two.
        var bare = Bare(wanted);

        var nicknamed = catalogue.FirstOrDefault(name =>
            string.Equals(Bare(name), bare, StringComparison.Ordinal));

        if (nicknamed is not null)
        {
            return nicknamed;
        }

        // Deliberately not the shortest containing name, nor the first: an ambiguous fragment has
        // no right answer, and picking one silently is how "gas giant" becomes "Class I gas
        // giant" and the Commander is told about the wrong thing with total confidence.
        var containing = catalogue
            .Where(name => Relax(name).Contains(relaxed, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return containing.Length == 1 ? containing[0] : null;
    }

    internal static string Relax(string text) =>
        new([.. text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    /// <summary>
    /// Relaxed, with any quoted aside removed.
    /// <para>
    /// Only when the quotes pair up, which is the guard that matters: a lone apostrophe is a
    /// possessive, and treating "Broo's Legacy" as an opening quote would swallow everything
    /// after it and match the wrong thing with total confidence.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Names close enough to be worth offering back, best first.
    /// <para>
    /// Two kinds of near miss, because a spoken name arrives through a transcriber and both
    /// happen. A <em>fragment</em> — "shield generator" for "Bi-Weave Shield Generator" — is what
    /// a Commander actually says, and substring matching catches it. A <em>misspelling</em> —
    /// "Frame Shift Drve" — is what the transcriber does to them, and only an edit distance
    /// catches that. Fragments rank first: they are the deliberate case, and a Commander who said
    /// a real thing shortly should not be shown a list headed by a typo of something else.
    /// </para>
    /// </summary>
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
                return candidate.Contains(relaxed, StringComparison.Ordinal)
                       || relaxed.Contains(candidate, StringComparison.Ordinal);
            })
            .ToList();

        // Scaled to the length of what was said: two wrong characters in "Sensors" is a different
        // word, while two in "Mk II Ablative Military Grade Composite" is a transcription.
        var budget = Math.Clamp(relaxed.Length / 6, 1, 4);

        var misspellings = catalogue
            .Except(fragments)
            .Select(name => (Name: name, Distance: Distance(Relax(name), relaxed, budget)))
            .Where(candidate => candidate.Distance <= budget)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Name);

        return [.. fragments.Concat(misspellings).Take(5)];
    }

    /// <summary>
    /// The same, with a third rung for names that arrived through a microphone
    /// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
    /// <para>
    /// <b>Edit distance is the wrong model for a transcriber, and this is measured rather than
    /// argued.</b> Typing errors are near in spelling; hearing errors are near in <em>sound</em>,
    /// and the two are not the same set. Run over this Commander's own 15,216 journal names:
    /// <c>"Eurebia"</c> for <c>Eurybia</c> is one edit and both rungs find it, but
    /// <c>"Dessy at"</c> for <c>Deciat</c> is <b>four</b> edits — the misspelling rung returns
    /// nothing at all, and the sound-alike rung returns exactly one candidate, the right one.
    /// </para>
    /// <para>
    /// <b>Last, and ranked by edit distance inside its own rung.</b> A phonetic key is deliberately
    /// loose — <c>"Jamison Memorial"</c> keys the same as <i>Jing Comms Co</i> — so it goes below
    /// the two precise rungs, and within itself the closest spelling comes first, which puts
    /// <i>Jameson Memorial</i> at the head of those five.
    /// </para>
    /// <para>
    /// <b>A limit, recorded rather than papered over.</b> It keys the whole name, so a transcriber
    /// that moved the word boundaries defeats it: <c>"shin arta desha"</c> finds
    /// <i>Shinrarta Dezhra</i> under neither rung. That case wants the biasing
    /// <c>ProperNouns</c> already does, and is why this is a recovery rather than a replacement
    /// for it.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// What a name sounds like, as a Soundex key
    /// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
    /// <para>
    /// <b>Soundex rather than something cleverer, because it was measured and it was enough.</b>
    /// It catches both of the mishearings this repository has actually written down — the reported
    /// <c>Eurebia</c> and the counter-example <c>"Dessy at"</c> that edit distance misses — over a
    /// real 15,216-name catalogue, and it does it with one candidate each. Double Metaphone is
    /// several hundred lines for a case nobody has produced.
    /// </para>
    /// <para>
    /// Keyed over the <see cref="Relax"/>ed form, so punctuation and spacing are already gone: a
    /// transcriber writes "Hutton Orbital" and "hutton orbital" indifferently, and neither is a
    /// difference in sound.
    /// </para>
    /// </summary>
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

            // H and W are transparent: they do not code, and they do not break a run either, so
            // "Ashcroft" keys its two consonants as one the way it is said.
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

    /// <summary>
    /// Levenshtein distance, abandoned once it passes <paramref name="budget"/>.
    /// <para>
    /// The bound is what keeps this cheap: it runs across every name in the catalogue per
    /// rejected lookup, and a row whose best entry already exceeds the budget cannot improve, so
    /// there is nothing to be gained by finishing the matrix.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The sentence a rejected name gets. One wording, so five callers cannot each invent their
    /// own and leave the model guessing whether it was refused or nothing was found.
    /// </summary>
    public static string Unknown(string kind, string spoken, IReadOnlyList<string> near) =>
        near.Count > 0
            ? $"I don't know a {kind} called '{spoken}'. Did you mean {string.Join(", ", near)}?"
            : $"I don't know a {kind} called '{spoken}'.";
}
