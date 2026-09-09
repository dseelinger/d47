using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Memory;

/// <summary>
/// What d47 remembers about the Commander, on disk (Phase 31, "A memory is written down, never inferred
/// into being").
/// </summary>
public sealed class MemoryStore(string path, ILogger<MemoryStore> logger)
{
    /// <summary>The key every entry written without a Commander aboard is filed under.</summary>
    public const string NoCommander = "";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _gate = new();

    private Dictionary<string, IReadOnlyList<MemoryEntry>> _byCommander = new(StringComparer.Ordinal);
    private IReadOnlyList<MemoryProblem> _problems = [];

    /// <summary>The file's contents as last read.</summary>
    private string? _seen;

    public string Path => path;

    /// <summary>Raised when the contents changed, whoever changed them.</summary>
    public event Action? Changed;

    /// <summary>Entries that were refused, and why.</summary>
    public IReadOnlyList<MemoryProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>Everything remembered about one Commander, oldest first.</summary>
    public IReadOnlyList<MemoryEntry> For(string? frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId ?? NoCommander, []);
        }
    }

    /// <summary>Every entry in the file, whoever it is about.</summary>
    public IReadOnlyList<(string FrontierId, MemoryEntry Entry)> Everything()
    {
        lock (_gate)
        {
            return
            [
                .. _byCommander
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .SelectMany(pair => pair.Value.Select(entry => (pair.Key, entry))),
            ];
        }
    }

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                // Not an error: an empty store is the normal state of a fresh install.
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _byCommander = new Dictionary<string, IReadOnlyList<MemoryEntry>>(StringComparer.Ordinal);
                    _problems = [];
                    _seen = null;
                }

                Changed?.Invoke();
                return true;
            }

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            // A read that lands mid-write is retried on the next tick rather than reported.
            logger.LogDebug(ex, "Could not read {Path}", path);
            return false;
        }

        if (string.Equals(text, _seen, StringComparison.Ordinal))
        {
            return false;
        }

        Reload(text);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Writes one entry and saves the file, replacing whatever shared its key.</summary>
    public MemoryEntry Write(string? frontierId, MemoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var kept = _byCommander
                .GetValueOrDefault(commander, [])
                .Where(existing => !string.Equals(existing.Key, entry.Key, StringComparison.Ordinal));

            _byCommander = new Dictionary<string, IReadOnlyList<MemoryEntry>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = [.. kept, entry],
            };
        }

        Save();
        Changed?.Invoke();
        return entry;
    }

    /// <summary>Forgets one entry by key.</summary>
    public bool Forget(string? frontierId, string key)
    {
        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);
            var kept = existing.Where(entry => !string.Equals(entry.Key, key, StringComparison.Ordinal)).ToArray();

            if (kept.Length == existing.Count)
            {
                return false;
            }

            _byCommander = new Dictionary<string, IReadOnlyList<MemoryEntry>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = kept,
            };
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Empties the store — every Commander, not just the one flying — and reports how many went.
    /// </summary>
    public int Empty()
    {
        Poll();

        int removed;

        lock (_gate)
        {
            removed = _byCommander.Sum(pair => pair.Value.Count);

            if (removed == 0 && _problems.Count == 0)
            {
                return 0;
            }

            _byCommander = new Dictionary<string, IReadOnlyList<MemoryEntry>>(StringComparer.Ordinal);

            // The refusals go with it.
            _problems = [];
        }

        Save();
        Changed?.Invoke();
        return removed;
    }

    /// <summary>Removes everything older than <paramref name="window"/> and reports what went.</summary>
    /// <paramref name="window"/>and reports what went.</paramref>
    public IReadOnlyList<MemoryEntry> Expire(DateTimeOffset now, TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            return [];
        }

        Poll();

        var gone = new List<MemoryEntry>();

        lock (_gate)
        {
            var next = new Dictionary<string, IReadOnlyList<MemoryEntry>>(StringComparer.Ordinal);

            foreach (var (commander, entries) in _byCommander)
            {
                // An entry with no stamp is never expired.
                var kept = entries.Where(entry => entry.AddedAt is not { } at || now - at <= window).ToArray();

                gone.AddRange(entries.Except(kept));
                next[commander] = kept;
            }

            if (gone.Count == 0)
            {
                return [];
            }

            _byCommander = next;
        }

        Save();
        Changed?.Invoke();
        return gone;
    }

    private void Save()
    {
        Document document;

        lock (_gate)
        {
            document = new Document
            {
                Commanders =
                [
                    .. _byCommander
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new CommanderRecord
                        {
                            FrontierId = pair.Key,
                            Memories =
                            [
                                .. pair.Value.Select(entry => new EntryRecord
                                {
                                    Key = entry.Key,
                                    Fact = entry.Fact,
                                    Tier = entry.Tier,
                                    Arrival = entry.Arrival,
                                    About = entry.About.Count == 0 ? null : [.. entry.About],
                                    AddedAt = entry.AddedAt,
                                }),
                            ],
                        }),
                ],
            };
        }

        var text = JsonSerializer.Serialize(document, Json);

        try
        {
            AtomicFile.WriteAllText(path, text);

            // Recorded as seen, so the write this instance just made does not read back as somebody else's
            // edit on the next poll.
            lock (_gate)
            {
                _seen = text;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
        }
    }

    private void Reload(string text)
    {
        var loaded = new Dictionary<string, IReadOnlyList<MemoryEntry>>(StringComparer.Ordinal);
        var problems = new List<MemoryProblem>();

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var entries = new List<MemoryEntry>();
                var keys = new HashSet<string>(StringComparer.Ordinal);

                foreach (var record in commander.Memories ?? [])
                {
                    if (string.IsNullOrWhiteSpace(record.Fact))
                    {
                        problems.Add(new MemoryProblem(record.Key ?? "(an entry)", "nothing in it"));
                        continue;
                    }

                    // A missing key is filled in rather than refused: the file is meant to be hand-edited,
                    // and typing a fact is the part a person will do.
                    var key = string.IsNullOrWhiteSpace(record.Key)
                        ? MemoryKeys.Next(MemoryKeys.HandPrefix, keys)
                        : record.Key.Trim();

                    if (!keys.Add(key))
                    {
                        problems.Add(new MemoryProblem(key, "two entries share this key"));
                        continue;
                    }

                    entries.Add(new MemoryEntry(key, record.Fact.Trim())
                    {
                        Tier = record.Tier,
                        Arrival = record.Arrival,
                        About = record.About is { Count: > 0 } about
                            ? [.. about.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim())]
                            : [],
                        AddedAt = record.AddedAt,
                    });
                }

                loaded[commander.FrontierId ?? NoCommander] = entries;
            }
        }
        catch (JsonException ex)
        {
            // The whole file, not one entry.
            problems.Add(new MemoryProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            // A parse failure keeps whatever was already loaded rather than emptying the store, for the same
            // reason it is reported.
            if (problems.Count == 0 || loaded.Count > 0)
            {
                _byCommander = loaded;
            }

            _problems = problems;
            _seen = text;
        }

        logger.LogInformation(
            "Loaded {Count} remembered facts from {Path} ({Problems} refused)",
            _byCommander.Sum(pair => pair.Value.Count),
            path,
            problems.Count);
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public IReadOnlyList<EntryRecord>? Memories { get; set; }
    }

    private sealed class EntryRecord
    {
        public string? Key { get; set; }

        public string? Fact { get; set; }

        /// <summary>
        /// A hand-written entry with no tier is the Commander's own word, which is both the safest
        /// thing an unlabelled line can be taken for and the only thing a person editing this file by
        /// hand is plausibly doing.
        /// </summary>
        public MemoryTier Tier { get; set; } = MemoryTier.Stated;

        public MemoryArrival Arrival { get; set; } = MemoryArrival.Panel;

        public IReadOnlyList<string>? About { get; set; }

        public DateTimeOffset? AddedAt { get; set; }
    }
}

/// <summary>Where an entry's key comes from.</summary>
public static class MemoryKeys
{
    /// <summary>The Commander's own, typed on the panel.</summary>
    public const string StatedPrefix = "told";

    /// <summary>The model's, out of a conversation.</summary>
    public const string InferredPrefix = "noted";

    /// <summary>Written into the file by hand with no key on it.</summary>
    public const string HandPrefix = "hand";

    /// <summary>The prefix a route's entries are keyed under.</summary>
    public static string PrefixFor(MemoryArrival arrival) => arrival switch
    {
        MemoryArrival.Panel => StatedPrefix,
        MemoryArrival.Model => InferredPrefix,

        // Never reached in practice — the observer names its own keys per subject, which is what makes an
        // observation replace rather than accumulate.
        _ => MemoryObserver.KeyPrefix,
    };

    /// <summary>The lowest <c>prefix-N</c> not already taken.</summary>
    public static string Next(string prefix, IReadOnlyCollection<string> taken)
    {
        for (var n = 1; ; n++)
        {
            var candidate = $"{prefix}-{n}";

            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
