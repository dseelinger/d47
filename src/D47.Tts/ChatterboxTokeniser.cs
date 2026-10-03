using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace D47.Tts;

/// <summary>
/// Chatterbox's text front end: GPT-2 byte-level BPE, with the <c>added_tokens</c> of
/// <c>tokenizer.json</c> matched whole before the BPE sees the text.
/// </summary>
internal sealed partial class ChatterboxTokeniser
{
    private const string EndOfText = "<|endoftext|>";

    /// <summary>GPT-2's pre-tokeniser; <c>tokenizer.json</c> says ByteLevel with use_regex.</summary>
    [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+")]
    private static partial Regex Pieces();

    private static readonly char[] ByteToChar = BuildByteToChar();

    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<(string, string), int> _ranks;
    private readonly Dictionary<string, int> _added;
    private readonly string[] _addedLongestFirst;
    private readonly int[] _suffix;

    private ChatterboxTokeniser(
        Dictionary<string, int> vocab,
        Dictionary<(string, string), int> ranks,
        Dictionary<string, int> added,
        int[] suffix)
    {
        _vocab = vocab;
        _ranks = ranks;
        _added = added;
        _addedLongestFirst = [.. added.Keys.OrderByDescending(t => t.Length)];
        _suffix = suffix;
    }

    /// <summary>The bracketed tags the model performs, without their brackets, in id order.</summary>
    public IReadOnlyList<string> Tags =>
    [
        .. _added
            .Where(a => a.Key.Length > 2 && a.Key[0] == '[' && a.Key[^1] == ']')
            .OrderBy(a => a.Value)
            .Select(a => a.Key[1..^1]),
    ];

    /// <summary>Whether <paramref name="tag"/>, written without brackets, is one of the model's own tokens.</summary>
    public bool Performs(string tag) => _added.ContainsKey($"[{tag}]");

    public static ChatterboxTokeniser Load(string path)
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
            // tokenizers 0.20 writes a merge as a two-element array; earlier versions as "a b".
            var pair = merge.ValueKind == JsonValueKind.Array
                ? (merge[0].GetString()!, merge[1].GetString()!)
                : Split(merge.GetString()!);

            ranks.TryAdd(pair, rank++);
        }

        // Case-insensitive, so "[Laugh]" from a model is the [laugh] token.
        var added = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in root.GetProperty("added_tokens").EnumerateArray())
        {
            added[token.GetProperty("content").GetString()!] = token.GetProperty("id").GetInt32();
        }

        return new ChatterboxTokeniser(vocab, ranks, added, Suffix(root, added));

        static (string, string) Split(string merge)
        {
            var space = merge.IndexOf(' ', StringComparison.Ordinal);
            return (merge[..space], merge[(space + 1)..]);
        }
    }

    /// <summary>The special tokens the post-processor's template appends after the sequence.</summary>
    private static int[] Suffix(JsonElement root, Dictionary<string, int> added)
    {
        if (!root.TryGetProperty("post_processor", out var post)
            || post.ValueKind != JsonValueKind.Object
            || !post.TryGetProperty("single", out var single))
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
                foreach (var part in Merge(Map(match.Value)))
                {
                    if (_vocab.TryGetValue(part, out var id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }

        ids.AddRange(_suffix.Select(id => (long)id));

        return [.. ids];
    }

    /// <summary>Added tokens win over the BPE, longest first.</summary>
    private IEnumerable<(string Piece, bool IsTag)> SplitOnTags(string text)
    {
        var at = 0;
        var plain = new StringBuilder();

        while (at < text.Length)
        {
            var tag = Array.Find(
                _addedLongestFirst,
                t => t != EndOfText
                     && at + t.Length <= text.Length
                     && string.Compare(text, at, t, 0, t.Length, StringComparison.OrdinalIgnoreCase) == 0);

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

    /// <summary>Greedy lowest-rank merge.</summary>
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
