using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>A pattern, as written, a Commander taught that stands for a declared phrase.</summary>
public sealed record LearnedPhrase(string Said, string Phrase, DateTimeOffset LearnedAt);

public enum PhraseClashKind
{
    /// <summary>The wording is a phrase in the phrase book.</summary>
    BookPhrase,

    /// <summary>The wording is one of this Commander's own phrases, standing for a different phrase.</summary>
    OwnPhrase,
}

/// <summary>
/// A wording a new pattern cannot take. <paramref name="StandsFor"/> is the book phrase itself or the phrase
/// the Commander's own pattern stands for; <paramref name="CapabilityId"/> is set for a book phrase and
/// <paramref name="Pattern"/> for a Commander's own phrase.
/// </summary>
public sealed record PhraseClash(
    string Wording, PhraseClashKind Kind, string StandsFor, string? CapabilityId, string? Pattern);

/// <summary>
/// Per Commander Frontier id, the wording they have confirmed stands for a declared phrase (#169). Kept
/// between sessions at <c>data/phrases.json</c>, beside <see cref="Listening.HeardNamesStore"/>.
/// </summary>
public sealed class LearnedPhrasesStore(string path, ILogger<LearnedPhrasesStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    /// <summary>
    /// Serialises <see cref="Learn"/> and <see cref="Forget"/> against each other, across the read that
    /// decides what changes and the write that applies it — <see cref="_gate"/> alone only protects one
    /// snapshot at a time, and two callers each starting from the same snapshot would otherwise silently
    /// undo one another's change.
    /// </summary>
    private readonly Lock _writeGate = new();

    private Dictionary<string, Dictionary<string, LearnedPhrase>> _byCommander = new(StringComparer.Ordinal);

    public string Path => path;

    /// <summary>Raised when a phrase is learned or forgotten, whoever changed it.</summary>
    public event Action? Changed;

    public void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json);

            var loaded = new Dictionary<string, Dictionary<string, LearnedPhrase>>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId))
                {
                    continue;
                }

                var forCommander = new Dictionary<string, LearnedPhrase>(StringComparer.OrdinalIgnoreCase);

                foreach (var phrase in commander.Phrases ?? [])
                {
                    if (phrase is { Said.Length: > 0, Phrase.Length: > 0 })
                    {
                        var entry = new LearnedPhrase(phrase.Said, phrase.Phrase, phrase.LearnedAt);

                        foreach (var wording in WordingsOf(phrase.Said))
                        {
                            forCommander[wording] = entry;
                        }
                    }
                }

                loaded[commander.FrontierId] = forCommander;
            }

            lock (_gate)
            {
                _byCommander = loaded;
            }

            logger.LogInformation(
                "Loaded {Phrases} learned phrase(s) for {Count} Commanders",
                loaded.Values.Sum(commander => commander.Count),
                loaded.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; phrases learned before now are lost", path);
        }
    }

    /// <summary>What this Commander has taught d47 that "said" means "phrase", or null for nothing.</summary>
    public string? PhraseFor(string frontierId, string utterance)
    {
        lock (_gate)
        {
            return _byCommander.TryGetValue(frontierId, out var learned)
                && learned.TryGetValue(KeywordRouter.Utterance(utterance), out var entry)
                ? entry.Phrase
                : null;
        }
    }

    /// <summary>Every phrase this Commander has taught d47, one per pattern as written.</summary>
    public IReadOnlyList<LearnedPhrase> For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.TryGetValue(frontierId, out var learned) ? Distinct(learned) : [];
        }
    }

    /// <summary>The entry that already teaches <paramref name="pattern"/> as written for <paramref name="phrase"/>.</summary>
    public LearnedPhrase? Taught(string frontierId, string pattern, string phrase) =>
        For(frontierId).FirstOrDefault(entry =>
            string.Equals(Same(entry.Said), Same(pattern), StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Phrase, phrase, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The first of <paramref name="wordings"/> that cannot stand for <paramref name="phrase"/>: a phrase in
    /// the book, or one of this Commander's phrases standing for a different phrase.
    /// </summary>
    public PhraseClash? FindClash(string frontierId, IEnumerable<string> wordings, string phrase, PhraseBook book)
    {
        var inBook = new Dictionary<string, PhraseEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in book.Entries)
        {
            inBook.TryAdd(KeywordRouter.WithoutThe(entry.Phrase), entry);
        }

        foreach (var wording in wordings)
        {
            if (inBook.TryGetValue(KeywordRouter.WithoutThe(wording), out var declared))
            {
                return new PhraseClash(wording, PhraseClashKind.BookPhrase, declared.Phrase, declared.CapabilityId, null);
            }

            lock (_gate)
            {
                if (_byCommander.TryGetValue(frontierId, out var learned)
                    && learned.TryGetValue(wording, out var own)
                    && !string.Equals(own.Phrase, phrase, StringComparison.OrdinalIgnoreCase))
                {
                    return new PhraseClash(wording, PhraseClashKind.OwnPhrase, own.Phrase, null, own.Said);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Records that a pattern stands for a phrase, replacing any earlier mapping for each of its wordings.
    /// False when the pattern is refused.
    /// </summary>
    public bool Learn(string frontierId, string said, string phrase, DateTimeOffset at)
    {
        if (!PhrasePattern.TryExpand(said, out var wordings, out var error))
        {
            logger.LogWarning("Not learning \"{Said}\": {Error}", said, error);
            return false;
        }

        lock (_writeGate)
        {
            var merged = CloneReplacing(frontierId, out var forCommander);
            var entry = new LearnedPhrase(said, phrase, at);

            foreach (var wording in wordings)
            {
                forCommander[wording] = entry;
            }

            Write(merged);
        }

        logger.LogInformation("Learned that \"{Said}\" means \"{Phrase}\"", said, phrase);

        return true;
    }

    /// <summary>Forgets one pattern as written, with every wording it produced and no others.</summary>
    public bool Forget(string frontierId, string said)
    {
        lock (_writeGate)
        {
            var target = _byCommander.TryGetValue(frontierId, out var learned)
                ? learned.Values.FirstOrDefault(entry =>
                    string.Equals(Same(entry.Said), Same(said), StringComparison.OrdinalIgnoreCase))
                : null;

            if (target is null)
            {
                return false;
            }

            var merged = CloneReplacing(frontierId, out var forCommander);

            foreach (var key in forCommander.Where(pair => ReferenceEquals(pair.Value, target)).Select(pair => pair.Key).ToList())
            {
                forCommander.Remove(key);
            }

            Write(merged);
        }

        logger.LogInformation("Forgot \"{Said}\"", said);

        return true;
    }

    /// <summary>
    /// The outer dictionary, shallow-copied so every other Commander's map is reused rather than cloned,
    /// with <paramref name="frontierId"/>'s own map deep-copied so it can be changed without touching what
    /// <see cref="_byCommander"/> still points at. Called only while holding <see cref="_writeGate"/>, so
    /// it always starts from the latest write rather than a snapshot an earlier caller has since replaced.
    /// </summary>
    private Dictionary<string, Dictionary<string, LearnedPhrase>> CloneReplacing(
        string frontierId, out Dictionary<string, LearnedPhrase> forCommander)
    {
        var merged = new Dictionary<string, Dictionary<string, LearnedPhrase>>(_byCommander, StringComparer.Ordinal);

        forCommander = merged.TryGetValue(frontierId, out var existing)
            ? new Dictionary<string, LearnedPhrase>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, LearnedPhrase>(StringComparer.OrdinalIgnoreCase);

        merged[frontierId] = forCommander;

        return merged;
    }

    private static string Same(string text) => KeywordRouter.Utterance(text);

    private static List<LearnedPhrase> Distinct(Dictionary<string, LearnedPhrase> byWording) =>
        [.. byWording.Values.Distinct(ReferenceEqualityComparer.Instance).Cast<LearnedPhrase>()];

    /// <summary>The wordings of a stored pattern; one that no longer parses is kept as a literal wording.</summary>
    private static IReadOnlyList<string> WordingsOf(string said) =>
        PhrasePattern.TryExpand(said, out var wordings, out _)
            ? wordings
            : [KeywordRouter.Utterance(PhrasePattern.Literal(said))];

    private void Write(Dictionary<string, Dictionary<string, LearnedPhrase>> merged)
    {
        var document = new Document
        {
            Commanders =
            [
                .. merged.Select(entry => new CommanderRecord
                {
                    FrontierId = entry.Key,
                    Phrases =
                    [
                        .. Distinct(entry.Value).Select(phrase => new PhraseRecord
                        {
                            Said = phrase.Said,
                            Phrase = phrase.Phrase,
                            LearnedAt = phrase.LearnedAt,
                        }),
                    ],
                }),
            ],
        };

        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(document, Json));
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
            return;
        }

        lock (_gate)
        {
            _byCommander = merged;
        }

        Changed?.Invoke();
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public IReadOnlyList<PhraseRecord>? Phrases { get; set; }
    }

    private sealed class PhraseRecord
    {
        public string Said { get; set; } = string.Empty;

        public string Phrase { get; set; } = string.Empty;

        public DateTimeOffset LearnedAt { get; set; }
    }
}
