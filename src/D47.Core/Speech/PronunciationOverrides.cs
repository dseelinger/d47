using System.Text.Json;

namespace D47.Core.Speech;

/// <summary>
/// One correction the Commander wrote down: either a respelling to run down the ladder, or IPA
/// exactly as they want it said.
/// </summary>
/// <param name="Key">The words it matches, as written in the file.</param>
/// <param name="Value">The respelling, or the IPA with its marker already removed.</param>
/// <param name="IsIpa">Whether <paramref name="Value"/> goes straight to the tokenizer.</param>
public sealed record Pronunciation(string Key, string Value, bool IsIpa);

/// <summary>
/// The Commander's own pronunciations, read from a file in <c>data\</c> and re-read when they
/// change it (#150).
/// <para>
/// <b>Every other road to a wrong word ends in a rebuild.</b> The ladder's rungs are the shipped
/// dictionary and code, so correcting one name meant changing <see cref="Phonotactics"/>,
/// <see cref="LetterToSound"/> or a generated table and cutting a release for it. The 410 game
/// words the local voice knew would fall through the dictionary — engineer systems worst of all —
/// are exactly the vocabulary Frontier adds to and the community argues about.
/// </para>
/// <para>
/// <b>Two ways to write an entry, because IPA is expert-hostile.</b> A respelling is processed
/// through the rest of the ladder as if it were the text, which makes the easy case writable by
/// anyone; raw IPA is marked <c>ipa:</c> and goes straight to the tokenizer. Capitals in a
/// respelling are for the reader — the ladder is case-blind — so a Commander who wants the stress
/// somewhere exact writes the IPA form.
/// </para>
/// <code>
/// {
///   "Shinrarta Dezhra": "shin rar tah dezh rah",
///   "Dezhra": "ipa:ˈdɛʒɹə"
/// }
/// </code>
/// <para>
/// <b>Per-installation, not per-Commander.</b> A pronunciation is a fact about the voice rather
/// than about who is flying, so it stays out of the Frontier-id keyed stores.
/// </para>
/// <para>
/// <b>A bad entry degrades to the ladder and is named once.</b> Once per version of the file
/// rather than once per utterance: a wrong entry is a thing to go and fix, not a thing to be
/// nagged about every time d47 speaks.
/// </para>
/// </summary>
public sealed class PronunciationOverrides
{
    /// <summary>The file's name in the data folder. Named here so nothing else spells it.</summary>
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

    /// <summary>
    /// </summary>
    /// <param name="path">The file, which does not have to exist.</param>
    /// <param name="speakable">
    /// The symbols the voice this feeds can actually say, or null to accept any IPA. Passed by
    /// the provider from its own tokenizer, which is what makes "unparseable IPA" a real check
    /// rather than a guess: a symbol outside the vocabulary is dropped on the way to the model, so
    /// an entry full of them is silence, and silence is the one outcome an override must never
    /// produce.
    /// </param>
    /// <param name="complain">Where a rejected entry is named. One call per rejected entry.</param>
    public PronunciationOverrides(
        string path,
        IReadOnlySet<char>? speakable = null,
        Action<string>? complain = null)
    {
        _path = path;
        _speakable = speakable;
        _complain = complain;
    }

    /// <summary>How many corrections are live. Zero when the file is absent, which is the default.</summary>
    public int Count => _entries.Count;

    /// <summary>The file this reads, for the diagnostics page.</summary>
    public string FilePath => _path;

    /// <summary>
    /// Re-reads the file if it has changed since the last look.
    /// <para>
    /// <b>A stat rather than a watcher, and no thread of its own</b> — Core owns neither
    /// (architecture.md). The check is the file's own length and write time, so an unchanged file
    /// costs one <c>FileInfo</c> per utterance and a changed one is live on the next thing d47
    /// says. That is the whole feature: edit, save, say the word again, hear the difference.
    /// </para>
    /// </summary>
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

            // Stamped before the read rather than after it, so a file that cannot be parsed is
            // complained about once and not on every line spoken afterwards.
            _read = stamp;

            if (!stamp.Item1)
            {
                // Deleted. Shipped behaviour, exactly.
                _entries = new Dictionary<string, Pronunciation>(StringComparer.OrdinalIgnoreCase);
                _longest = 0;
                return;
            }

            Reread();
        }
    }

    /// <summary>
    /// The correction covering the words starting at <paramref name="at"/>, and how many of them
    /// it consumed — or null where the Commander has said nothing about them.
    /// <para>
    /// <b>Longest first, over whole words.</b> A key is a run of words rather than a substring, so
    /// an entry for <em>male</em> cannot capture the middle of <em>female</em> — the lesson of
    /// <see href="https://github.com/dseelinger/d47/issues/146">#146</see>, applied before it could
    /// be learned a second time.
    /// </para>
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

    /// <summary>
    /// The file, parsed. Comments and trailing commas are allowed because this is a file a person
    /// types into, and a JSON parser refusing a trailing comma is not a thing to make somebody
    /// debug by ear.
    /// </summary>
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
            // The previous entries stand. A half-written file is what a save looks like from here,
            // and dropping a Commander's corrections because they were mid-keystroke would be
            // worse than being one edit behind until the next one.
            _complain?.Invoke($"{FileName} could not be read ({ex.Message}), so the last good entries stand");
            return;
        }

        _entries = entries;
        _longest = longest;
    }

    /// <summary>
    /// One entry, or null where it is not usable. Everything refused here falls through to the
    /// ladder, which is what would have been said anyway.
    /// </summary>
    private Pronunciation? Accept(JsonProperty entry)
    {
        // Whitespace-normalised, because "Shinrarta  Dezhra" and "Shinrarta Dezhra" are the same
        // key and the match above joins words with exactly one space.
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
