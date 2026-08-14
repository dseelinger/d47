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
