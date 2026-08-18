using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Memory;

/// <summary>
/// What d47 remembers about the Commander, on disk (list.md Phase 31, "A memory is written down,
/// never inferred into being").
/// <para>
/// <b>This is the store that survives.</b> Before it, the whole of d47's recall was
/// <c>_personaLastSeen</c> — a <see cref="Dictionary{TKey,TValue}"/> field in <c>AppHost</c>, alive
/// for as long as the window was open and gone at exit.
/// </para>
/// <para>
/// <b>It has both halves of Phase 23's store pattern, which no earlier store needed at once.</b>
/// Like <see cref="Lore.LoreStore"/> it holds words a person typed, so it is polled for hand edits
/// and a line that cannot be read is reported through <see cref="Problems"/> rather than dropped —
/// nothing rebuilds a sentence somebody wrote. Like <see cref="Journal.SamplingStore"/> it is
/// <b>keyed per Commander with the key inside the document</b>, because a Frontier id comes out of
/// the journal, journal content is untrusted input, and turning it into a filename buys a
/// path-traversal surface for an organisational convenience.
/// </para>
/// <para>
/// <b>The observed entries live in here too</b>, rather than in a derived sibling the way
/// <see cref="Lore.LoreVisits"/> sits beside the lore book. That is a deliberate break: item 3 asks
/// for <em>the whole store</em> to be readable as plain text and emptied in one action, and a store
/// with a second half somewhere else is two places to look and one of them gets missed. Observed
/// entries carry a fixed key per subject and are <b>replaced</b>, so the file does not grow with
/// them.
/// </para>
/// <para>
/// <b>Change is detected by comparing content, never by a last-write time.</b> Phase 21's
/// correction, and it applies here for the reason <see cref="Hotas.SwitchStore"/> records: Windows
/// moves the file-system clock about every 15.6 ms, so two writes inside one tick carry the same
/// stamp — and unlike Status.json, which is rewritten every second and self-corrects, a hand edit is
/// a one-off that stays missed.
/// </para>
/// </summary>
public sealed class MemoryStore(string path, ILogger<MemoryStore> logger)
{
    /// <summary>
    /// The key every entry written without a Commander aboard is filed under. Real, rather than a
    /// null the readers have to remember to handle: d47 runs before Elite has said who is flying,
    /// and a memory typed on the panel in that state belongs somewhere it can be found again.
    /// </summary>
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

    /// <summary>The file's contents as last read. What "has it changed" is answered against.</summary>
    private string? _seen;

    public string Path => path;

    /// <summary>Raised when the contents changed, whoever changed them. The panel follows this.</summary>
    public event Action? Changed;

    /// <summary>Entries that were refused, and why. Empty in the ordinary case.</summary>
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

    /// <summary>
    /// Every entry in the file, whoever it is about. What the panel's window reads, and what makes
    /// "the whole store is readable" true rather than "the store for whoever is flying right now".
    /// </summary>
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

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core, so
    /// a fact edited in a text editor is live without a restart.
    /// </summary>
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
            // A read that lands mid-write is retried on the next tick rather than reported. The
            // writer is atomic, so the window is a rename and this is never a lasting state.
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

    /// <summary>
    /// Writes one entry and saves the file, replacing whatever shared its key. The single write
    /// path, so the panel, the observer and the tool surface cannot disagree about what a write
    /// does.
    /// <para>
    /// Polls first, so a hand edit made since the last tick is not overwritten by a change made on
    /// top of a stale copy.
    /// </para>
    /// </summary>
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

    /// <summary>Forgets one entry by key. Returns whether there was one.</summary>
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
    /// Empties the store — <b>every Commander, not just the one flying</b> — and reports how many
    /// went. What the panel's one action does.
    /// <para>
    /// Every Commander, because the button is in the privacy section and a Commander pressing it
    /// means "forget me", not "forget the character I happen to be logged in as". A per-character
    /// erase that left three other characters' facts on disk would be the kind of privacy control
    /// that reads as one and is not.
    /// </para>
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

            // The refusals go with it. They describe lines in a file that is about to say nothing,
            // and a problem list outliving the file it is about is a panel reporting a fault
            // nobody can look at.
            _problems = [];
        }

        Save();
        Changed?.Invoke();
        return removed;
    }

    /// <summary>
    /// Removes everything older than <paramref name="window"/> and <b>reports what went</b>.
    /// <para>
    /// The report is the point. list.md Phase 31 item 3: a companion that quietly drops what you
    /// told it last month is worse than one that never remembered, so this returns the entries
    /// rather than a count and the caller decides which of them are worth a sentence.
    /// </para>
    /// </summary>
    /// <param name="window">
    /// How long an entry lives. <see cref="TimeSpan.Zero"/> or less is "never expire", which is a
    /// real setting rather than an absence — see <c>MemorySettings.ExpiryDays</c>.
    /// </param>
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
                // An entry with no stamp is never expired. A hand-written line is the likeliest
                // thing to be missing one, and deleting the Commander's own words because they
                // did not type a date is the worst reading of an ambiguous file.
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

            // Recorded as seen, so the write this instance just made does not read back as
            // somebody else's edit on the next poll.
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

                    // A missing key is filled in rather than refused: the file is meant to be
                    // hand-edited, and typing a fact is the part a person will do.
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
            // The whole file, not one entry. Reported rather than discarded: these are facts about
            // the Commander, some of them in their own words, and silently starting empty would
            // look exactly like d47 having forgotten them.
            problems.Add(new MemoryProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            // A parse failure keeps whatever was already loaded rather than emptying the store,
            // for the same reason it is reported.
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
        /// thing an unlabelled line can be taken for and the only thing a person editing this file
        /// by hand is plausibly doing.
        /// </summary>
        public MemoryTier Tier { get; set; } = MemoryTier.Stated;

        public MemoryArrival Arrival { get; set; } = MemoryArrival.Panel;

        public IReadOnlyList<string>? About { get; set; }

        public DateTimeOffset? AddedAt { get; set; }
    }
}

/// <summary>
/// Where an entry's key comes from. Deterministic and lowest-unused, the same rule authored
/// checklist keys and ship build ids follow, and for the same reason: Core reads no clock and owns
/// no randomness, so a key has to be a function of what is already there.
/// </summary>
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

        // Never reached in practice — the observer names its own keys per subject, which is what
        // makes an observation replace rather than accumulate. Answered rather than thrown so that
        // a caller which does reach it gets a key instead of an exception.
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
