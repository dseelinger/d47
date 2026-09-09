using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Lore;

/// <summary>One entry that could not be read back, and why.</summary>
public sealed record LoreProblem(string What, string Why);

/// <summary>Commander's Lore on disk (Phase 23, "Commander's Lore").</summary>
public sealed class LoreStore(string path, ILogger<LoreStore> logger)
{
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

    private IReadOnlyList<LoreEntry> _entries = [];
    private IReadOnlyList<LoreProblem> _problems = [];

    /// <summary>The file's contents as last read.</summary>
    private string? _seen;

    public string Path => path;

    /// <summary>Raised when the contents changed, whoever changed them.</summary>
    public event Action? Changed;

    public IReadOnlyList<LoreEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries;
            }
        }
    }

    /// <summary>Entries that were refused, and why.</summary>
    public IReadOnlyList<LoreProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
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
                // Not an error: an empty book is the normal state, and will be for most Commanders.
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _entries = [];
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

    /// <summary>Adds one entry and writes the file.</summary>
    public LoreEntry Add(LoreEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Poll();

        lock (_gate)
        {
            // Replaced rather than appended when the same system is written about twice by the same route.
            _entries =
            [
                .. _entries.Where(existing =>
                    existing.SystemAddress != entry.SystemAddress || existing.Arrival != entry.Arrival),
                entry,
            ];
        }

        Write();
        Changed?.Invoke();
        return entry;
    }

    /// <summary>Forgets every entry for one system, and reports how many went.</summary>
    public int Forget(long systemAddress)
    {
        Poll();

        int removed;

        lock (_gate)
        {
            var kept = _entries.Where(entry => entry.SystemAddress != systemAddress).ToArray();
            removed = _entries.Count - kept.Length;
            _entries = kept;
        }

        if (removed == 0)
        {
            return 0;
        }

        Write();
        Changed?.Invoke();
        return removed;
    }

    private void Write()
    {
        Document document;

        lock (_gate)
        {
            document = new Document
            {
                Entries =
                [
                    .. _entries.Select(entry => new EntryRecord
                    {
                        SystemAddress = entry.SystemAddress,
                        Name = entry.Name,
                        Note = entry.Note,
                        Tier = entry.Tier,
                        Arrival = entry.Arrival,
                        FrontierId = entry.FrontierId,
                        AddedAt = entry.AddedAt,
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
        var entries = new List<LoreEntry>();
        var problems = new List<LoreProblem>();

        try
        {
            foreach (var record in JsonSerializer.Deserialize<Document>(text, Json)?.Entries ?? [])
            {
                if (record.SystemAddress <= 0)
                {
                    problems.Add(new LoreProblem(
                        record.Name ?? record.Note ?? "(an entry)",
                        "no system address, so nothing could ever match it"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.Note))
                {
                    problems.Add(new LoreProblem(record.Name ?? record.SystemAddress.ToString(), "no note"));
                    continue;
                }

                entries.Add(new LoreEntry(record.SystemAddress, record.Name ?? "", record.Note.Trim())
                {
                    // A hand-written entry with no tier is the Commander's word, which is the safest thing an
                    // unlabelled note can be taken for.
                    Tier = record.Tier == LoreTier.Shipped ? LoreTier.Commander : record.Tier,
                    Arrival = record.Arrival,
                    FrontierId = record.FrontierId,
                    AddedAt = record.AddedAt,
                });
            }
        }
        catch (JsonException ex)
        {
            // The whole file, not one entry.
            problems.Add(new LoreProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            // The parse failure keeps whatever was already loaded rather than emptying the book, for the same
            // reason it is reported: the file on disk is recoverable and the entries in memory are still
            // good.
            if (problems.Count == 0 || entries.Count > 0)
            {
                _entries = entries;
            }

            _problems = problems;
            _seen = text;
        }

        logger.LogInformation(
            "Loaded {Count} Commander's Lore entries from {Path} ({Problems} refused)",
            _entries.Count,
            path,
            problems.Count);
    }

    private sealed class Document
    {
        public IReadOnlyList<EntryRecord> Entries { get; set; } = [];
    }

    private sealed class EntryRecord
    {
        public long SystemAddress { get; set; }

        public string? Name { get; set; }

        public string? Note { get; set; }

        public LoreTier Tier { get; set; } = LoreTier.Commander;

        public LoreArrival Arrival { get; set; } = LoreArrival.Panel;

        public string? FrontierId { get; set; }

        public DateTimeOffset? AddedAt { get; set; }
    }
}
