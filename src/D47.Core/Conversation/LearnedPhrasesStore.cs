using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>One utterance a Commander confirmed stands for a declared phrase.</summary>
public sealed record LearnedPhrase(string Said, string Phrase, DateTimeOffset LearnedAt);

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
                        forCommander[KeywordRouter.Utterance(phrase.Said)] =
                            new LearnedPhrase(phrase.Said, phrase.Phrase, phrase.LearnedAt);
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

    /// <summary>Every phrase this Commander has taught d47, for the settings row and its "forget" button.</summary>
    public IReadOnlyList<LearnedPhrase> For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.TryGetValue(frontierId, out var learned) ? [.. learned.Values] : [];
        }
    }

    /// <summary>Records that an utterance stands for a phrase, replacing any earlier mapping for it.</summary>
    public void Learn(string frontierId, string said, string phrase, DateTimeOffset at)
    {
        lock (_writeGate)
        {
            var merged = CloneReplacing(frontierId, out var forCommander);

            forCommander[KeywordRouter.Utterance(said)] = new LearnedPhrase(said, phrase, at);

            Write(merged);
        }

        logger.LogInformation("Learned that \"{Said}\" means \"{Phrase}\"", said, phrase);
    }

    /// <summary>Forgets one learned utterance, so a mishearing accepted once does not stay in the router.</summary>
    public bool Forget(string frontierId, string said)
    {
        lock (_writeGate)
        {
            if (!_byCommander.TryGetValue(frontierId, out var learned)
                || !learned.ContainsKey(KeywordRouter.Utterance(said)))
            {
                return false;
            }

            var merged = CloneReplacing(frontierId, out var forCommander);

            forCommander.Remove(KeywordRouter.Utterance(said));
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
                        .. entry.Value.Values.Select(phrase => new PhraseRecord
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
