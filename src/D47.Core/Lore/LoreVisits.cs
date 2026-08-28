using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Lore;

/// <summary>
/// When each system was last remarked on, kept between sessions (Phase 23, "Remark on
/// arrival, and not again today").
/// <para>
/// <b>The stamps are absolute, never elapsed.</b> An "eleven hours ago" written to disk is a
/// number that starts lying the moment the app exits, and the replay harness runs at 100x — where
/// an elapsed figure would make "24 hours" mean fourteen minutes. An instant compared against the
/// tick's injected <see cref="Ticking.TickContext.Now"/> means the same thing at either speed.
/// </para>
/// <para>
/// <b>Derived state, so it follows <see cref="Journal.SamplingStore"/> rather than
/// <see cref="LoreStore"/>.</b> A file that has gone bad is discarded with a log line: the worst
/// it costs is hearing one remark a second time. Losing a note the Commander typed is a different
/// kind of loss, which is why that one is a different file with different rules.
/// </para>
/// <para>
/// <b>One set for the installation</b>, matching the book beside it. The rule is about not saying
/// the same thing to the same person twice in a day, and swapping characters does not make them a
/// different person.
/// </para>
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

    /// <summary>Whether anything has been recorded since the last write. The app flushes on it.</summary>
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

    /// <summary>
    /// Records that a remark just happened. Takes the time rather than reading a clock, like
    /// everything else in Core.
    /// </summary>
    public void Record(long systemAddress, DateTimeOffset now)
    {
        lock (_gate)
        {
            _spokenAt[systemAddress] = now;
            _dirty = true;
        }
    }

    /// <summary>Reads the file. A missing one is the normal first run.</summary>
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
            // Left dirty on purpose, so the next flush tries again. The cost of never succeeding
            // is one repeated remark per system after a restart.
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
