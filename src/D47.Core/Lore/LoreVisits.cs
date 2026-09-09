using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Lore;

/// <summary>
/// When each system was last remarked on, kept between sessions (Phase 23, "Remark on arrival, and not
/// again today").
/// </summary>
public sealed class LoreVisits(string path, ILogger<LoreVisits> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Lock _gate = new();

    private Dictionary<long, DateTimeOffset> _spokenAt = [];
    private bool _dirty;

    public string Path => path;

    /// <summary>Whether anything has been recorded since the last write.</summary>
    public bool Dirty
    {
        get
        {
            lock (_gate)
            {
                return _dirty;
            }
        }
    }

    /// <summary>When this system was last remarked on, or null for never.</summary>
    public DateTimeOffset? LastSpokenAt(long systemAddress)
    {
        lock (_gate)
        {
            return _spokenAt.TryGetValue(systemAddress, out var when) ? when : null;
        }
    }

    /// <summary>Records that a remark just happened.</summary>
    public void Record(long systemAddress, DateTimeOffset now)
    {
        lock (_gate)
        {
            _spokenAt[systemAddress] = now;
            _dirty = true;
        }
    }

    /// <summary>Reads the file.</summary>
    public void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json);

            var loaded = new Dictionary<long, DateTimeOffset>();

            foreach (var record in document?.Systems ?? [])
            {
                if (record.SystemAddress > 0)
                {
                    loaded[record.SystemAddress] = record.SpokenAt;
                }
            }

            lock (_gate)
            {
                _spokenAt = loaded;
                _dirty = false;
            }

            logger.LogInformation("Loaded lore remark history for {Count} systems", loaded.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; every system will be remarked on afresh", path);
        }
    }

    /// <summary>Writes through <see cref="AtomicFile"/>, and clears <see cref="Dirty"/>.</summary>
    public void Save()
    {
        Document document;

        lock (_gate)
        {
            document = new Document
            {
                Systems =
                [
                    .. _spokenAt
                        .OrderBy(pair => pair.Key)
                        .Select(pair => new VisitRecord { SystemAddress = pair.Key, SpokenAt = pair.Value }),
                ],
            };
        }

        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(document, Json));

            lock (_gate)
            {
                _dirty = false;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            // Left dirty on purpose, so the next flush tries again.
            logger.LogWarning(ex, "Could not write {Path}", path);
        }
    }

    private sealed class Document
    {
        public IReadOnlyList<VisitRecord> Systems { get; set; } = [];
    }

    private sealed class VisitRecord
    {
        public long SystemAddress { get; set; }

        public DateTimeOffset SpokenAt { get; set; }
    }
}
