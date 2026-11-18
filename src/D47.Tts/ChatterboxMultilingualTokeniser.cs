using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Storage;

namespace D47.Tts;

/// <summary>
/// Chatterbox Multilingual's text front end, as <c>tokenizer.json</c> describes it: added tokens matched whole, the
/// normaliser's replacements, a whitespace pre-tokeniser, a character BPE with an unknown token, then the
/// post-processor's template around the line.
/// </summary>
internal sealed partial class ChatterboxMultilingualTokeniser
{
    /// <summary>The pre-tokeniser <c>Whitespace</c>: <c>\w+|[^\w\s]+</c>.</summary>
    [GeneratedRegex(@"[\p{L}\p{M}\p{Nd}\p{Pc}]+|[^\p{L}\p{M}\p{Nd}\p{Pc}\s]+")]
    private static partial Regex Pieces();

    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<(string, string), int> _ranks;
    private readonly int? _unknown;
    private readonly IReadOnlyList<(string From, string To)> _replacements;
    private readonly Dictionary<string, int> _added;
    private readonly string[] _addedLongestFirst;
    private readonly int[] _prefix;
    private readonly int[] _suffix;

    private ChatterboxMultilingualTokeniser(
        Dictionary<string, int> vocab,
        Dictionary<(string, string), int> ranks,
        int? unknown,
        IReadOnlyList<(string, string)> replacements,
        Dictionary<string, int> added,
        int[] prefix,
        int[] suffix)
    {
        _vocab = vocab;
        _ranks = ranks;
        _unknown = unknown;
        _replacements = replacements;
        _added = added;
        _addedLongestFirst = [.. added.Keys.OrderByDescending(t => t.Length)];
        _prefix = prefix;
        _suffix = suffix;
    }

    public static ChatterboxMultilingualTokeniser Load(IFileSystem files, string path) =>
        Parse(files.ReadText(path) ?? throw new FileNotFoundException("No Chatterbox Multilingual tokenizer.", path));

    public static ChatterboxMultilingualTokeniser Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var model = root.GetProperty("model");

        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entry in model.GetProperty("vocab").EnumerateObject())
        {
            vocab[entry.Name] = entry.Value.GetInt32();
        }

        var ranks = new Dictionary<(string, string), int>();
        var rank = 0;

        foreach (var merge in model.GetProperty("merges").EnumerateArray())
        {
            var pair = merge.ValueKind == JsonValueKind.Array
                ? (merge[0].GetString()!, merge[1].GetString()!)
                : Split(merge.GetString()!);

            ranks.TryAdd(pair, rank++);
        }

        int? unknown = model.TryGetProperty("unk_token", out var unk) && unk.ValueKind == JsonValueKind.String
            ? vocab[unk.GetString()!]
            : null;

        var added = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var token in root.GetProperty("added_tokens").EnumerateArray())
        {
            added[token.GetProperty("content").GetString()!] = token.GetProperty("id").GetInt32();
        }

        var (prefix, suffix) = Template(root);

        return new ChatterboxMultilingualTokeniser(vocab, ranks, unknown, Replacements(root), added, prefix, suffix);

        static (string, string) Split(string merge)
        {
            var space = merge.IndexOf(' ', StringComparison.Ordinal);
            return (merge[..space], merge[(space + 1)..]);
        }
    }

    /// <summary>The normaliser's <c>Replace</c> steps; any other kind of normaliser is refused rather than skipped.</summary>
    private static List<(string, string)> Replacements(JsonElement root)
    {
        var replacements = new List<(string, string)>();

        if (!root.TryGetProperty("normalizer", out var normalizer) || normalizer.ValueKind != JsonValueKind.Object)
        {
            return replacements;
        }

        var steps = normalizer.GetProperty("type").GetString() == "Sequence"
            ? normalizer.GetProperty("normalizers").EnumerateArray().ToList()
            : [normalizer];

        foreach (var step in steps)
        {
            var type = step.GetProperty("type").GetString();

            if (type != "Replace" || !step.GetProperty("pattern").TryGetProperty("String", out var from))
            {
                throw new InvalidDataException($"The Chatterbox Multilingual tokenizer has a {type} normaliser, which d47 does not apply.");
            }

            replacements.Add((from.GetString()!, step.GetProperty("content").GetString()!));
        }

        return replacements;
    }

    /// <summary>The ids the post-processor's single template puts before and after the line.</summary>
    private static (int[] Prefix, int[] Suffix) Template(JsonElement root)
    {
        if (!root.TryGetProperty("post_processor", out var post)
            || post.ValueKind != JsonValueKind.Object
            || !post.TryGetProperty("single", out var single))
        {
            return ([], []);
        }

        var special = post.GetProperty("special_tokens");
        var prefix = new List<int>();
        var suffix = new List<int>();
        var seenSequence = false;

        foreach (var step in single.EnumerateArray())
        {
            if (step.TryGetProperty("Sequence", out _))
            {
                seenSequence = true;
                continue;
            }

            var name = step.GetProperty("SpecialToken").GetProperty("id").GetString()!;
            var ids = special.GetProperty(name).GetProperty("ids").EnumerateArray().Select(id => id.GetInt32());

            (seenSequence ? suffix : prefix).AddRange(ids);
        }

        return ([.. prefix], [.. suffix]);
    }

    public long[] Encode(string text)
    {
        foreach (var (from, to) in _replacements)
        {
            text = text.Replace(from, to, StringComparison.Ordinal);
        }

        var ids = new List<long>(_prefix.Select(id => (long)id));

        foreach (var (piece, isAdded) in SplitOnAdded(text))
        {
            if (isAdded)
            {
                ids.Add(_added[piece]);
                continue;
            }

            foreach (Match match in Pieces().Matches(piece))
            {
                foreach (var part in Merge(match.Value))
                {
                    ids.Add(part);
                }
            }
        }

        ids.AddRange(_suffix.Select(id => (long)id));

        return [.. ids];
    }

    /// <summary>Added tokens win over the BPE, longest first, matched exactly.</summary>
    private IEnumerable<(string Piece, bool IsAdded)> SplitOnAdded(string text)
    {
        var at = 0;
        var plain = new StringBuilder();

        while (at < text.Length)
        {
            var token = Array.Find(
                _addedLongestFirst,
                t => at + t.Length <= text.Length && string.CompareOrdinal(text, at, t, 0, t.Length) == 0);

            if (token is null)
            {
                plain.Append(text[at++]);
                continue;
            }

            if (plain.Length > 0)
            {
                yield return (plain.ToString(), false);
                plain.Clear();
            }

            yield return (token, true);
            at += token.Length;
        }

        if (plain.Length > 0)
        {
            yield return (plain.ToString(), false);
        }
    }

    /// <summary>One character per symbol, an unknown one as the unknown token, then greedy lowest-rank merges.</summary>
    private List<long> Merge(string word)
    {
        var parts = new List<string?>();

        foreach (var rune in word.EnumerateRunes())
        {
            var symbol = rune.ToString();
            parts.Add(_vocab.ContainsKey(symbol) ? symbol : null);
        }

        while (parts.Count > 1)
        {
            var best = int.MaxValue;
            var at = -1;

            for (var i = 0; i < parts.Count - 1; i++)
            {
                if (parts[i] is { } left && parts[i + 1] is { } right
                    && _ranks.TryGetValue((left, right), out var rank) && rank < best
                    && _vocab.ContainsKey(left + right))
                {
                    best = rank;
                    at = i;
                }
            }

            if (at < 0)
            {
                break;
            }

            parts[at] += parts[at + 1];
            parts.RemoveAt(at + 1);
        }

        var ids = new List<long>(parts.Count);

        foreach (var part in parts)
        {
            if (part is not null)
            {
                ids.Add(_vocab[part]);
            }
            else if (_unknown is { } unknown)
            {
                ids.Add(unknown);
            }
        }

        return ids;
    }
}
