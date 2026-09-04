using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChatterboxProbe;

/// <summary>
/// Chatterbox's text front end: a stock GPT-2 byte-level BPE with the paralinguistic tags added as
/// whole tokens, read out of <c>tokenizer.json</c>.
/// <para>
/// Hand-rolled rather than taken from a package, for the reason the whole probe exists: the
/// question is what d47 would have to carry, and a tokeniser it cannot get from a MIT-licensed
/// NuGet is part of the bill. It turns out to be about a hundred lines, and the tags — the feature
/// #293 wants — are the easy half: they are entries in <c>added_tokens</c>, matched whole before
/// the BPE ever sees the text.
/// </para>
/// </summary>
internal sealed partial class Tokeniser
{
    /// <summary>GPT-2's pre-tokeniser, verbatim. <c>tokenizer.json</c> says ByteLevel/use_regex.</summary>
    [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+")]
    private static partial Regex Pieces();

    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<(string, string), int> _ranks;
    private readonly Dictionary<string, int> _added;
    private readonly int[] _suffix;

    private static readonly char[] ByteToChar = BuildByteToChar();

    private Tokeniser(
        Dictionary<string, int> vocab,
        Dictionary<(string, string), int> ranks,
        Dictionary<string, int> added,
        int[] suffix)
    {
        _vocab = vocab;
        _ranks = ranks;
        _added = added;
        _suffix = suffix;
    }

    /// <summary>Every tag the model knows, in id order — the surface #293 would expose.</summary>
    public IEnumerable<string> Tags => _added.Where(a => a.Value != 50256)
                                             .OrderBy(a => a.Value)
                                             .Select(a => a.Key);

    public static Tokeniser Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
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
            // 3.x writes a merge as a two-element array; 1.x wrote it as one space-separated string.
            var pair = merge.ValueKind == JsonValueKind.Array
                ? (merge[0].GetString()!, merge[1].GetString()!)
                : Split(merge.GetString()!);

            ranks.TryAdd(pair, rank++);
        }

        var added = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var token in root.GetProperty("added_tokens").EnumerateArray())
        {
            added[token.GetProperty("content").GetString()!] = token.GetProperty("id").GetInt32();
        }

        return new Tokeniser(vocab, ranks, added, Suffix(root, added));

        static (string, string) Split(string merge)
        {
            var space = merge.IndexOf(' ');
            return (merge[..space], merge[(space + 1)..]);
        }
    }

    /// <summary>
    /// What the post-processor appends. Chatterbox's template is <c>A &lt;|endoftext|&gt;
    /// &lt;|endoftext|&gt;</c> — two of them, and nothing in front — which is the sort of thing that
    /// produces plausible-sounding wrong audio if it is guessed instead of read.
    /// </summary>
    private static int[] Suffix(JsonElement root, Dictionary<string, int> added)
    {
        if (!root.TryGetProperty("post_processor", out var post) ||
            post.ValueKind != JsonValueKind.Object ||
            !post.TryGetProperty("single", out var single))
        {
            return [];
        }

        var ids = new List<int>();
        var seenSequence = false;

        foreach (var step in single.EnumerateArray())
        {
            if (step.TryGetProperty("Sequence", out _))
            {
                seenSequence = true;
                continue;
            }

            if (seenSequence && step.TryGetProperty("SpecialToken", out var special))
            {
                ids.Add(added[special.GetProperty("id").GetString()!]);
            }
        }

        return [.. ids];
    }

    public long[] Encode(string text)
    {
        var ids = new List<long>();

        foreach (var (piece, isTag) in SplitOnTags(text))
        {
            if (isTag)
            {
                ids.Add(_added[piece]);
                continue;
            }

            foreach (Match match in Pieces().Matches(piece))
            {
                ids.AddRange(Merge(Map(match.Value)).Select(t => (long)_vocab[t]));
            }
        }

        ids.AddRange(_suffix.Select(id => (long)id));

        return [.. ids];
    }

    /// <summary>
    /// Added tokens win over the BPE, and longest wins over shortest, so <c>[chuckle]</c> stays one
    /// token instead of becoming five pieces of punctuation and a word.
    /// </summary>
    private IEnumerable<(string Piece, bool IsTag)> SplitOnTags(string text)
    {
        var tags = _added.Keys.OrderByDescending(t => t.Length).ToArray();
        var at = 0;
        var plain = new StringBuilder();

        while (at < text.Length)
        {
            var tag = tags.FirstOrDefault(t => string.CompareOrdinal(text, at, t, 0, t.Length) == 0);

            if (tag is null)
            {
                plain.Append(text[at++]);
                continue;
            }

            if (plain.Length > 0)
            {
                yield return (plain.ToString(), false);
                plain.Clear();
            }

            yield return (tag, true);
            at += tag.Length;
        }

        if (plain.Length > 0)
        {
            yield return (plain.ToString(), false);
        }
    }

    /// <summary>UTF-8 bytes to the printable alphabet the vocabulary is written in.</summary>
    private static string Map(string piece)
    {
        var bytes = Encoding.UTF8.GetBytes(piece);
        var mapped = new char[bytes.Length];

        for (var i = 0; i < bytes.Length; i++)
        {
            mapped[i] = ByteToChar[bytes[i]];
        }

        return new string(mapped);
    }

    private static char[] BuildByteToChar()
    {
        var map = new char[256];
        var next = 0;

        for (var b = 0; b < 256; b++)
        {
            var printable = b is (>= 0x21 and <= 0x7E) or (>= 0xA1 and <= 0xAC) or (>= 0xAE and <= 0xFF);
            map[b] = printable ? (char)b : (char)(256 + next++);
        }

        return map;
    }

    /// <summary>Greedy lowest-rank merge, the standard BPE inner loop.</summary>
    private List<string> Merge(string mapped)
    {
        var parts = mapped.Select(c => c.ToString()).ToList();

        while (parts.Count > 1)
        {
            var best = int.MaxValue;
            var at = -1;

            for (var i = 0; i < parts.Count - 1; i++)
            {
                if (_ranks.TryGetValue((parts[i], parts[i + 1]), out var rank) && rank < best)
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

        return parts;
    }
}
