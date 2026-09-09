using System.Text.Json;

namespace D47.Core.Speech;

/// <summary>
/// One correction the Commander wrote down: either a respelling to run down the ladder, or IPA exactly
/// as they want it said.
/// </summary>
/// <param name="Key">The words it matches, as written in the file.</param>
/// <param name="Value">The respelling, or the IPA with its marker already removed.</param>
/// <param name="IsIpa">Whether <paramref name="Value"/> goes straight to the tokenizer.</param>
public sealed record Pronunciation(string Key, string Value, bool IsIpa);

/// <summary>
/// The Commander's own pronunciations, read from a file in <c>data\</c> and re-read when they change it
/// (#150).
/// </summary>
public sealed class PronunciationOverrides
{
    /// <summary>The file's name in the data folder.</summary>
    public const string FileName = "pronunciations.json";

    /// <summary>What marks a value as IPA rather than a respelling.</summary>
    public const string IpaMarker = "ipa:";

    private readonly string _path;
    private readonly IReadOnlySet<char>? _speakable;
    private readonly Action<string>? _complain;
    private readonly Lock _gate = new();

    private Dictionary<string, Pronunciation> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    private int _longest;
    private (bool Exists, long Length, DateTime Written) _read = (false, -1, DateTime.MinValue);

    /// <param name="path">The file, which does not have to exist.</param>
    /// <param name="speakable">
    /// The symbols the voice this feeds can actually say, or null to accept any IPA.
    /// </param>
    /// <param name="complain">Where a rejected entry is named.</param>
    public PronunciationOverrides(
        string path,
        IReadOnlySet<char>? speakable = null,
        Action<string>? complain = null)
    {
        _path = path;
        _speakable = speakable;
        _complain = complain;
    }

    /// <summary>How many corrections are live.</summary>
    public int Count => _entries.Count;

    /// <summary>The file this reads, for the diagnostics page.</summary>
    public string FilePath => _path;

    /// <summary>Re-reads the file if it has changed since the last look.</summary>
    public void Refresh()
    {
        var file = new FileInfo(_path);
        var stamp = file.Exists
            ? (true, file.Length, file.LastWriteTimeUtc)
            : (false, -1L, DateTime.MinValue);

        lock (_gate)
        {
            if (stamp == _read)
            {
                return;
            }

            // Stamped before the read rather than after it, so a file that cannot be parsed is complained
            // about once and not on every line spoken afterwards.
            _read = stamp;

            if (!stamp.Item1)
            {
                // Deleted.
                _entries = new Dictionary<string, Pronunciation>(StringComparer.OrdinalIgnoreCase);
                _longest = 0;
                return;
            }

            Reread();
        }
    }

    /// <summary>
    /// The correction covering the words starting at <paramref name="at"/>, and how many of them it
    /// consumed — or null where the Commander has said nothing about them.
    /// </summary>
    public (int Words, Pronunciation Said)? Match(IReadOnlyList<string> words, int at)
    {
        var entries = _entries;

        if (entries.Count == 0 || at >= words.Count || words[at].Length == 0)
        {
            return null;
        }

        for (var take = Math.Min(_longest, words.Count - at); take >= 1; take--)
        {
            var key = take == 1 ? words[at] : string.Join(' ', words.Skip(at).Take(take));

            if (entries.TryGetValue(key, out var said))
            {
                return (take, said);
            }
        }

        return null;
    }

    /// <summary>The file, parsed.</summary>
    private void Reread()
    {
        var entries = new Dictionary<string, Pronunciation>(StringComparer.OrdinalIgnoreCase);
        var longest = 0;

        try
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(_path),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                _complain?.Invoke(
                    $"{FileName} is not a list of words and pronunciations, so none were read");
                return;
            }

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (Accept(entry) is { } said)
                {
                    entries[said.Key] = said;
                    longest = Math.Max(longest, said.Key.Count(character => character == ' ') + 1);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // The previous entries stand.
            _complain?.Invoke($"{FileName} could not be read ({ex.Message}), so the last good entries stand");
            return;
        }

        _entries = entries;
        _longest = longest;
    }

    /// <summary>One entry, or null where it is not usable.</summary>
    private Pronunciation? Accept(JsonProperty entry)
    {
        // Whitespace-normalised, because "Shinrarta Dezhra" and "Shinrarta Dezhra" are the same key and the
        // match above joins words with exactly one space.
        var name = string.Join(
            ' ',
            entry.Name.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

        if (name.Length == 0)
        {
            _complain?.Invoke($"{FileName} has an entry with no word to correct, which is ignored");
            return null;
        }

        if (entry.Value.ValueKind != JsonValueKind.String ||
            entry.Value.GetString()?.Trim() is not { Length: > 0 } value)
        {
            _complain?.Invoke(
                $"{FileName}: \"{name}\" has no pronunciation, so it is said the usual way");
            return null;
        }

        if (!value.StartsWith(IpaMarker, StringComparison.OrdinalIgnoreCase))
        {
            if (value.Any(char.IsLetterOrDigit))
            {
                return new Pronunciation(name, value, IsIpa: false);
            }

            _complain?.Invoke(
                $"{FileName}: \"{name}\" is respelled as \"{value}\", which has nothing to say; "
                + "it is said the usual way");
            return null;
        }

        var ipa = value[IpaMarker.Length..].Trim();

        if (ipa.Length == 0)
        {
            _complain?.Invoke(
                $"{FileName}: \"{name}\" is marked as IPA and is empty, so it is said the usual way");
            return null;
        }

        if (_speakable is not null && Stray(ipa) is { } symbol)
        {
            _complain?.Invoke(
                $"{FileName}: \"{name}\" is IPA containing '{symbol}', which this voice cannot say; "
                + "it is said the usual way");
            return null;
        }

        return new Pronunciation(name, ipa, IsIpa: true);
    }

    /// <summary>The first symbol this voice has no token for, or null where it can say all of them.</summary>
    private char? Stray(string ipa)
    {
        foreach (var symbol in ipa)
        {
            if (!_speakable!.Contains(symbol))
            {
                return symbol;
            }
        }

        return null;
    }
}
