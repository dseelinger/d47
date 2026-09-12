using System.Text.RegularExpressions;

namespace D47.Core.Knowledge;

/// <summary>A star system name found in text, and the spelling to use for it.</summary>
public readonly record struct SystemNameHit(int Start, int Length, string Name);

/// <summary>Finds star system names in plain text.</summary>
public static partial class SystemNameFinder
{
    private const string SectorWord = @"[A-Z0-9(][A-Za-z0-9'.()]*";

    /// <summary>
    /// A named sector ending in <c>Sector</c> or <c>Region</c> — <c>Col 285 Sector</c>, <c>Pipe (stem)
    /// Sector</c>, <c>Musca Dark Region</c> — or <c>ICZ</c>, or one or two capitalised words.
    /// </summary>
    private const string Sector =
        $@"(?:[A-Z][A-Za-z0-9'.()]*(?: {SectorWord}){{0,3}} (?:Sector|Region)|ICZ|[A-Z][a-z]+(?: [A-Z][a-z]+)?)";

    private const string Boxel = @"[A-Z]{2}-[A-Z] [a-h](?:\d+-)?\d+";

    private const string Prefix =
        "2MASS|Groombridge|Lalande|Luyten|Struve|Gliese|Kruger|LAWD|NLTT|SDSS|StKM|Ross|WISE|Wolf"
        + "|CPD|HIP|LFT|LHS|LTT|BD|CD|GJ|HD|HR|LP|G";

    private const string Group = @"\d(?:[\d.+\-]*\d)?";

    private const string Catalogue = $@"(?:{Prefix})(?: [+\-]?|[+\-]){Group}(?: {Group})?";

    private const string Before = @"(?<![\p{L}\p{N}])";

    private const string After = @"(?![\p{L}\p{N}])";

    [GeneratedRegex($"{Before}{Sector} {Boxel}{After}", RegexOptions.CultureInvariant)]
    private static partial Regex ProceduralInText { get; }

    [GeneratedRegex($"{Before}{Catalogue}{After}", RegexOptions.CultureInvariant)]
    private static partial Regex CatalogueInText { get; }

    [GeneratedRegex($"^{Sector} {Boxel}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProceduralWhole { get; }

    [GeneratedRegex($"^{Catalogue}$", RegexOptions.CultureInvariant)]
    private static partial Regex CatalogueWhole { get; }

    /// <summary>Whether the whole of a name is a procedural name. <c>tools/gen-system-names.py</c> ports this pattern.</summary>
    public static bool IsProcedural(string name) => ProceduralWhole.IsMatch(name);

    /// <summary>Whether the whole of a name is a catalogue number. <c>tools/gen-system-names.py</c> ports this pattern.</summary>
    public static bool IsCatalogue(string name) => CatalogueWhole.IsMatch(name);

    private const int KnownRank = 0;
    private const int TableRank = 1;
    private const int PatternRank = 2;

    /// <summary>
    /// The system names in <paramref name="text"/>, in text order and never overlapping. The longest hit
    /// wins; on equal length a known name beats the table, which beats a pattern. A pattern hit that
    /// contains a known name gives way to it.
    /// </summary>
    public static IReadOnlyList<SystemNameHit> Find(string text, IReadOnlyCollection<string> known)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var knownHits = Known(text, known);
        var candidates = new List<(SystemNameHit Hit, int Rank)>();

        candidates.AddRange(knownHits.Select(hit => (hit, KnownRank)));
        candidates.AddRange(Table(text).Select(hit => (hit, TableRank)));

        foreach (var regex in new[] { ProceduralInText, CatalogueInText })
        {
            foreach (Match match in regex.Matches(text))
            {
                var hit = new SystemNameHit(match.Index, match.Length, match.Value);

                if (!knownHits.Any(inner => inner.Length < hit.Length && Contains(hit, inner)))
                {
                    candidates.Add((hit, PatternRank));
                }
            }
        }

        var chosen = new List<SystemNameHit>();

        foreach (var (hit, _) in candidates
            .OrderByDescending(candidate => candidate.Hit.Length)
            .ThenBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Hit.Start))
        {
            if (!chosen.Any(taken => Overlaps(taken, hit)))
            {
                chosen.Add(hit);
            }
        }

        return [.. chosen.OrderBy(hit => hit.Start)];
    }

    /// <summary>The runs of letters and digits in a string, which is how the finder counts words.</summary>
    internal static List<(int Start, int End)> Words(string text)
    {
        var words = new List<(int Start, int End)>();

        for (var at = 0; at < text.Length;)
        {
            if (!char.IsLetterOrDigit(text[at]))
            {
                at++;
                continue;
            }

            var start = at;

            while (at < text.Length && char.IsLetterOrDigit(text[at]))
            {
                at++;
            }

            words.Add((start, at));
        }

        return words;
    }

    private static List<SystemNameHit> Known(string text, IReadOnlyCollection<string> known)
    {
        var hits = new List<SystemNameHit>();

        foreach (var entry in known)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var name = entry.Trim();

            for (var at = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
                 at >= 0;
                 at = text.IndexOf(name, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                if (Bounded(text, at, at + name.Length))
                {
                    hits.Add(new SystemNameHit(at, name.Length, name));
                }
            }
        }

        return hits;
    }

    private static List<SystemNameHit> Table(string text)
    {
        var hits = new List<SystemNameHit>();
        var words = Words(text);
        var longest = SystemNameTable.LongestWordCount;

        for (var first = 0; first < words.Count; first++)
        {
            var start = words[first].Start;

            for (var count = Math.Min(longest, words.Count - first); count >= 1; count--)
            {
                if (TableSpan(text, start, words[first + count - 1].End) is not { } end)
                {
                    continue;
                }

                if (count > 1 || !AtSentenceStart(text, start))
                {
                    hits.Add(new SystemNameHit(start, end - start, text[start..end]));
                }

                break;
            }
        }

        return hits;
    }

    /// <summary>
    /// The end of the longest table name running from <paramref name="start"/> to <paramref name="end"/>,
    /// or past it over trailing punctuation such as the <c>*</c> in <c>Sagittarius A*</c>.
    /// </summary>
    private static int? TableSpan(string text, int start, int end)
    {
        int? found = null;

        for (var at = end;; at++)
        {
            if (Bounded(text, start, at) && SystemNameTable.Contains(text[start..at]))
            {
                found = at;
            }

            if (at >= text.Length || char.IsWhiteSpace(text[at]) || char.IsLetterOrDigit(text[at]))
            {
                return found;
            }
        }
    }

    /// <summary>Whether a word is the first of its sentence or line.</summary>
    private static bool AtSentenceStart(string text, int start)
    {
        for (var at = start - 1; at >= 0; at--)
        {
            var c = text[at];

            if (c is '\n' or '\r')
            {
                return true;
            }

            if (char.IsWhiteSpace(c) || c is '"' or '\'' or '(' or '[' or '`' or '*' or '_' or '-' or '>' or '#'
                    or '“' or '‘')
            {
                continue;
            }

            return c is '.' or '!' or '?';
        }

        return true;
    }

    private static bool Bounded(string text, int start, int end) =>
        (start == 0 || !char.IsLetterOrDigit(text[start - 1]))
        && (end == text.Length || !char.IsLetterOrDigit(text[end]));

    private static bool Contains(SystemNameHit outer, SystemNameHit inner) =>
        inner.Start >= outer.Start && inner.Start + inner.Length <= outer.Start + outer.Length;

    private static bool Overlaps(SystemNameHit a, SystemNameHit b) =>
        a.Start < b.Start + b.Length && b.Start < a.Start + a.Length;
}
