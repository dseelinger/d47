using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// One codex entry judged a possible region or galaxy first when it was first seen. <c>Journal</c> is the
/// <c>CodexEntry</c> line; <c>TableDate</c> is the date of the table it was judged against; <c>Reported</c> is
/// when it was sent to Canonn, null until then.
/// </summary>
public sealed record RegionFirstRecord(
    long EntryId,
    string? Name,
    int RegionId,
    string? Region,
    CodexFirst First,
    string? System,
    long? SystemAddress,
    StarPosition? StarPos,
    int? BodyId,
    string? Body,
    double? Latitude,
    double? Longitude,
    DateTimeOffset At,
    string? Journal,
    string? TableDate,
    DateTimeOffset? Reported = null);

/// <summary>
/// Each Commander's possible region and galaxy firsts, keyed by Frontier ID in <see cref="FileName"/>. A record is
/// judged once, against the table shipped when it is first seen, and never judged again or removed.
/// </summary>
public sealed class RegionFirstBook
{
    /// <summary>The file's name in the data folder.</summary>
    public const string FileName = "region-firsts.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly IFileSystem _files;
    private readonly ILogger _logger;
    private readonly Action<Action> _offTick;
    private readonly Lock _gate = new();

    /// <summary>Held for the file write; never taken while holding <see cref="_gate"/>.</summary>
    private readonly Lock _writeGate = new();

    private readonly Dictionary<string, List<RegionFirstRecord>> _records;

    /// <summary>Per Commander, the entry list last examined and the pairs judged this run. Tick thread only.</summary>
    private readonly Dictionary<string, (IReadOnlyList<CodexLogEntry> Seen, HashSet<(long, int)> Judged)> _examined =
        new(StringComparer.Ordinal);

    /// <summary>Bumped under <see cref="_gate"/> for each change.</summary>
    private long _version;

    /// <summary>The newest version on disk, under <see cref="_writeGate"/>.</summary>
    private long _written;

    /// <summary>
    /// Reads <paramref name="path"/>. <paramref name="offTick"/> runs each write; by default on the thread pool.
    /// </summary>
    public RegionFirstBook(string path, IFileSystem fileSystem, ILogger logger, Action<Action>? offTick = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);

        _path = path;
        _files = fileSystem;
        _logger = logger;
        _offTick = offTick ?? (work => Task.Run(work));
        _records = Load();
    }

    /// <summary>
    /// Judges each (entry, region) pair the Commander's whole ledger holds that has no record and was not judged
    /// this run, by its earliest entry, and records the firsts. Called on the tick; the file is written off it.
    /// </summary>
    public void Observe(string frontierId, CodexLedger ledger, CodexTable table)
    {
        ArgumentException.ThrowIfNullOrEmpty(frontierId);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(table);

        if (!ledger.IsWhole)
        {
            return;
        }

        var entries = ledger.Entries;

        if (_examined.TryGetValue(frontierId, out var examined) && ReferenceEquals(examined.Seen, entries))
        {
            return;
        }

        var judged = examined.Judged ?? [];
        _examined[frontierId] = (entries, judged);

        List<RegionFirstRecord>? added = null;

        foreach (var entry in entries)
        {
            if (entry.RegionId is not { } regionId || !judged.Add((entry.EntryId, regionId)) || Find(frontierId, entry.EntryId, regionId) is not null)
            {
                continue;
            }

            var first = CodexFirsts.Judge(table, entry.EntryId, regionId, entry.SubCategorySymbol);

            if (first is CodexFirst.None)
            {
                continue;
            }

            (added ??= []).Add(new RegionFirstRecord(
                entry.EntryId,
                entry.Name,
                regionId,
                GalacticRegions.NameOf(regionId),
                first,
                entry.System,
                entry.SystemAddress,
                entry.StarPos,
                entry.BodyId,
                entry.Body,
                entry.Latitude,
                entry.Longitude,
                entry.At,
                entry.Journal,
                table.Date));
        }

        if (added is null)
        {
            return;
        }

        Snapshot snapshot;

        lock (_gate)
        {
            if (!_records.TryGetValue(frontierId, out var held))
            {
                _records[frontierId] = held = [];
            }

            held.AddRange(added);
            snapshot = new Snapshot(Copy(), ++_version);
        }

        _offTick(() => Save(snapshot));
    }

    /// <summary>Changes each time a record is added or marked reported.</summary>
    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>The Commander's record for the entry in the region, or null.</summary>
    public RegionFirstRecord? Find(string frontierId, long entryId, int regionId)
    {
        lock (_gate)
        {
            return _records.TryGetValue(frontierId, out var held)
                ? held.Find(record => record.EntryId == entryId && record.RegionId == regionId)
                : null;
        }
    }

    /// <summary>The Commander's newest record not yet reported that holds its journal line and position, or null.</summary>
    public RegionFirstRecord? NewestUnreported(string frontierId)
    {
        lock (_gate)
        {
            return _records.TryGetValue(frontierId, out var held)
                ? held
                    .Where(record => record is { Reported: null, Journal.Length: > 0, StarPos: not null })
                    .MaxBy(record => record.At)
                : null;
        }
    }

    /// <summary>Marks the Commander's record for the entry in the region reported at <paramref name="at"/>; the file is written off the caller's thread.</summary>
    public void MarkReported(string frontierId, long entryId, int regionId, DateTimeOffset at)
    {
        Snapshot snapshot;

        lock (_gate)
        {
            if (!_records.TryGetValue(frontierId, out var held))
            {
                return;
            }

            var index = held.FindIndex(record => record.EntryId == entryId && record.RegionId == regionId);

            if (index < 0)
            {
                return;
            }

            held[index] = held[index] with { Reported = at };
            snapshot = new Snapshot(Copy(), ++_version);
        }

        _offTick(() => Save(snapshot));
    }

    private sealed record Snapshot(Dictionary<string, List<RegionFirstRecord>> Records, long Version);

    /// <summary>Called under <see cref="_gate"/>.</summary>
    private Dictionary<string, List<RegionFirstRecord>> Copy() =>
        _records.ToDictionary(pair => pair.Key, pair => pair.Value.OrderBy(record => record.At).ToList(), StringComparer.Ordinal);

    /// <summary>Writes the records unless a newer version is already on disk. Called outside <see cref="_gate"/>.</summary>
    private void Save(Snapshot snapshot)
    {
        lock (_writeGate)
        {
            if (snapshot.Version <= _written)
            {
                return;
            }

            _written = snapshot.Version;

            try
            {
                _files.WriteText(_path, JsonSerializer.Serialize(snapshot.Records, Json));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not write {Path}", _path);
            }
        }
    }

    private Dictionary<string, List<RegionFirstRecord>> Load()
    {
        try
        {
            if (_files.ReadText(_path) is { Length: > 0 } text
                && JsonSerializer.Deserialize<Dictionary<string, List<RegionFirstRecord>>>(text, Json) is { } stored)
            {
                return new Dictionary<string, List<RegionFirstRecord>>(stored, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Could not read {Path}; starting with no region firsts", _path);
        }

        return new Dictionary<string, List<RegionFirstRecord>>(StringComparer.Ordinal);
    }
}
