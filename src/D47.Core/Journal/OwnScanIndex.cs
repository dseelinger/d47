using System.Buffers;
using System.Text.Json;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// Which journal files hold each Commander's scans of each system, by <c>SystemAddress</c>, the entry and discovery
/// events of each system that the fold needs, and the session's live events. Rebuilt from the journals on every start
/// and extended by live events; holds no file and no scans: <see cref="Read"/> reads them back.
/// </summary>
public sealed class OwnScanIndex(IFileSystem fileSystem, ILogger logger)
{
    private const string Purpose = "own scans";

    private static readonly HashSet<string> Exploration = new(StringComparer.Ordinal)
    {
        "Scan", "ScanBaryCentre", "FSSDiscoveryScan", "FSSAllBodiesFound", "FSSBodySignals", "SAASignalsFound",
        "SAAScanComplete", "FSDJump", "Location", "CarrierJump",
    };

    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
    [
        .. Exploration, "Commander", "NewCommander", "LoadGame", "GameModeChange", "Fileheader", "Shutdown",
    ]);

    /// <summary>The fields of an entry or discovery event that <see cref="OwnSystemFold"/> reads.</summary>
    private static readonly string[] SummaryFields =
        ["timestamp", "event", "StarSystem", "StarPos", "SystemAddress", "Population", "SystemName", "BodyCount"];

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Dictionary<long, List<string>>> _files = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Dictionary<long, Summary>> _summaries = new(StringComparer.Ordinal);

    /// <summary>Each folded file's place in <see cref="_order"/> when its fold began.</summary>
    private readonly Dictionary<string, long> _fileStarts = new(StringComparer.Ordinal);

    /// <summary>Counts the events folded from files, in the order a read lists them.</summary>
    private long _order;

    private readonly Dictionary<string, Dictionary<long, List<JournalEvent>>> _live = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Fid, long SystemAddress), int> _versions = [];

    private string _liveFid = string.Empty;

    /// <summary>
    /// The last system's events read from its files. A line added to a listed file later also arrives live, so the
    /// events hold while the system's file list is unchanged.
    /// </summary>
    private Collected? _collected;

    /// <summary>Folds live events; a synchronous add with no I/O.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        JournalFold.Apply(_gate, events, FoldLive);
    }

    /// <summary>Folds journal files oldest first, on the calling thread.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            FoldFile(file, JournalLines.Read(fileSystem, file, logger, Purpose, warnIfMissing: false));
        }
    }

    /// <summary>Changes whenever a live event for the system is folded.</summary>
    public int Version(string fid, long systemAddress)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _versions.GetValueOrDefault((fid, systemAddress));
        }
    }

    /// <summary>The Commander's own record of one system, or null with none. Reads journal files: call off the tick and the UI thread.</summary>
    public OwnSystem? Read(string fid, long systemAddress, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(fid);

        string[] files;
        long[] starts;
        Kept[] summary;
        JournalEvent[] live;
        Collected? collected;

        lock (_gate)
        {
            files = _files.TryGetValue(fid, out var systems) && systems.TryGetValue(systemAddress, out var named) ? [.. named] : [];
            starts = [.. files.Select(file => _fileStarts[file])];
            summary = _summaries.TryGetValue(fid, out var summed) && summed.TryGetValue(systemAddress, out var kept) ? kept.Events() : [];
            live = _live.TryGetValue(fid, out var held) && held.TryGetValue(systemAddress, out var events) ? [.. events] : [];
            collected = _collected;
        }

        if (files.Length == 0 && summary.Length == 0 && live.Length == 0)
        {
            return null;
        }

        List<(long Start, JournalEvent Event)> read;

        if (collected is not null && collected.Fid == fid && collected.SystemAddress == systemAddress && collected.Files.SequenceEqual(files))
        {
            read = [.. collected.Events];
        }
        else
        {
            read = [];

            for (var i = 0; i < files.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                Collect(JournalLines.Read(fileSystem, files[i], logger, Purpose, warnIfMissing: false), fid, systemAddress, starts[i], read);
            }

            lock (_gate)
            {
                var listed = _files.TryGetValue(fid, out var now) && now.TryGetValue(systemAddress, out var still) ? still : [];

                if (listed.SequenceEqual(files))
                {
                    _collected = new Collected(fid, systemAddress, files, [.. read]);
                }
            }
        }

        return OwnSystemFold.Fold(systemAddress, [.. InFileOrder(read, summary, files), .. live]);
    }

    /// <summary>
    /// The events read from the listed files with the summary events of files not listed, each at its file's place in
    /// the fold, which is where a read of every file would have put it. A summary event of a listed file is left out,
    /// since the file was read whole.
    /// </summary>
    private static IEnumerable<JournalEvent> InFileOrder(List<(long Start, JournalEvent Event)> read, Kept[] summary, string[] files)
    {
        var unlisted = summary.Where(kept => !files.Contains(kept.File, StringComparer.Ordinal)).ToList();
        var next = 0;

        foreach (var (start, journalEvent) in read)
        {
            while (next < unlisted.Count && unlisted[next].Order < start)
            {
                yield return unlisted[next++].Event;
            }

            yield return journalEvent;
        }

        while (next < unlisted.Count)
        {
            yield return unlisted[next++].Event;
        }
    }

    private static bool IsExploration(JournalEvent journalEvent) => Exploration.Contains(journalEvent.Kind);

    private void FoldFile(string file, IEnumerable<string> lines)
    {
        var events = JournalFold.Parse(lines, Reads.Matches, logger);
        var fid = string.Empty;

        lock (_gate)
        {
            _fileStarts.TryAdd(file, _order);

            foreach (var journalEvent in OperationMarker.Mark(events))
            {
                var order = _order++;

                if (Commander(journalEvent, ref fid, live: false) || fid.Length == 0 || journalEvent.InOperation || !IsExploration(journalEvent)
                    || journalEvent.Long("SystemAddress") is not { } address)
                {
                    continue;
                }

                if (IsSummary(journalEvent))
                {
                    Keep(fid, address, journalEvent, file, order);
                    continue;
                }

                if (!_files.TryGetValue(fid, out var systems))
                {
                    systems = [];
                    _files[fid] = systems;
                }

                if (!systems.TryGetValue(address, out var named))
                {
                    named = [];
                    systems[address] = named;
                }

                if (named.Count == 0 || !string.Equals(named[^1], file, StringComparison.Ordinal))
                {
                    named.Add(file);
                }
            }
        }
    }

    /// <summary>An event whose fold needs only the latest of its kind, and for an entry each change of population.</summary>
    private static bool IsSummary(JournalEvent journalEvent) =>
        BodyScans.IsSystemEntry(journalEvent) || journalEvent.Kind is "FSSDiscoveryScan" or "FSSAllBodiesFound";

    /// <summary>Keeps the event if it is the latest of its kind for the system or an entry that changes its population.</summary>
    private void Keep(string fid, long systemAddress, JournalEvent journalEvent, string file, long order)
    {
        if (!_summaries.TryGetValue(fid, out var systems))
        {
            systems = [];
            _summaries[fid] = systems;
        }

        if (!systems.TryGetValue(systemAddress, out var summary))
        {
            summary = new Summary();
            systems[systemAddress] = summary;
        }

        switch (journalEvent.Kind)
        {
            case "FSSDiscoveryScan":
                if (IsLatest(summary.Discovery, journalEvent))
                {
                    summary.Discovery = new Kept(Trimmed(journalEvent), file, order);
                }

                return;

            case "FSSAllBodiesFound":
                if (IsLatest(summary.AllFound, journalEvent))
                {
                    summary.AllFound = new Kept(Trimmed(journalEvent), file, order);
                }

                return;
        }

        var populated = BodyScans.PopulationOf(journalEvent) is { Populated: true };
        var latest = IsLatest(summary.Entry, journalEvent);

        if (populated == summary.Populated && !latest)
        {
            return;
        }

        var trimmed = new Kept(Trimmed(journalEvent), file, order);

        if (populated != summary.Populated)
        {
            summary.Changes.Add(trimmed);
            summary.Populated = populated;
        }

        if (latest)
        {
            summary.Entry = trimmed;
        }
    }

    /// <summary>Whether the event sorts at or after the one kept, as <see cref="OwnSystemFold"/> orders by time.</summary>
    private static bool IsLatest(Kept? kept, JournalEvent journalEvent) => kept is null || journalEvent.Timestamp >= kept.Value.Event.Timestamp;

    /// <summary>The event with only <see cref="SummaryFields"/>.</summary>
    private static JournalEvent Trimmed(JournalEvent journalEvent)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var field in SummaryFields)
            {
                if (journalEvent.Raw.TryGetProperty(field, out var value))
                {
                    writer.WritePropertyName(field);
                    value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);

        return new JournalEvent(journalEvent.Timestamp, journalEvent.Kind, document.RootElement.Clone());
    }

    private void FoldLive(JournalEvent journalEvent)
    {
        if (Commander(journalEvent, ref _liveFid, live: true) || _liveFid.Length == 0 || journalEvent.InOperation || !IsExploration(journalEvent)
            || journalEvent.Long("SystemAddress") is not { } address)
        {
            return;
        }

        if (!_live.TryGetValue(_liveFid, out var systems))
        {
            systems = [];
            _live[_liveFid] = systems;
        }

        if (!systems.TryGetValue(address, out var events))
        {
            events = [];
            systems[address] = events;
        }

        events.Add(journalEvent);
        _versions[(_liveFid, address)] = _versions.GetValueOrDefault((_liveFid, address)) + 1;
    }

    /// <summary>
    /// Follows the Commander lines; true for one. A <c>NewCommander</c> drops the Commander's files, and a live one their
    /// live events too.
    /// </summary>
    private bool Commander(JournalEvent journalEvent, ref string fid, bool live)
    {
        switch (journalEvent.Kind)
        {
            case "Commander" or "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } named)
                {
                    fid = named;
                }

                return true;

            case "NewCommander":
                if (journalEvent.String("FID") is { Length: > 0 } erased)
                {
                    fid = erased;
                    _files.Remove(erased);
                    _summaries.Remove(erased);

                    if (_collected?.Fid == erased)
                    {
                        _collected = null;
                    }

                    if (live)
                    {
                        _live.Remove(erased);

                        foreach (var key in _versions.Keys.Where(key => key.Fid == erased).ToList())
                        {
                            _versions[key]++;
                        }
                    }
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>Adds the file's events for the Commander and system; a <c>NewCommander</c> for the Commander clears what came before.</summary>
    private void Collect(IEnumerable<string> lines, string fid, long systemAddress, long start, List<(long Start, JournalEvent Event)> found)
    {
        var current = string.Empty;

        foreach (var journalEvent in OperationMarker.Mark(JournalFold.Parse(lines, Reads.Matches, logger)))
        {
            switch (journalEvent.Kind)
            {
                case "Commander" or "LoadGame":
                    if (journalEvent.String("FID") is { Length: > 0 } named)
                    {
                        current = named;
                    }

                    continue;

                case "NewCommander":
                    if (journalEvent.String("FID") is { Length: > 0 } erased)
                    {
                        current = erased;

                        if (erased == fid)
                        {
                            found.Clear();
                        }
                    }

                    continue;
            }

            if (current == fid && !journalEvent.InOperation && IsExploration(journalEvent)
                && journalEvent.Long("SystemAddress") == systemAddress)
            {
                found.Add((start, journalEvent));
            }
        }
    }

    /// <summary>The events read from a system's files, each with its file's start in the fold order.</summary>
    private sealed record Collected(string Fid, long SystemAddress, string[] Files, (long Start, JournalEvent Event)[] Events);

    /// <summary>A trimmed summary event, the file it came from and its place in the fold order.</summary>
    private readonly record struct Kept(JournalEvent Event, string File, long Order);

    /// <summary>
    /// The entry and discovery events of one system that <see cref="OwnSystemFold"/> needs, since a file is listed only
    /// for the system's other events: the latest of each kind, for the name, position, body count and time, and each
    /// entry that changed the population, which sets the footfall flag of a later scan.
    /// </summary>
    private sealed class Summary
    {
        public Kept? Entry { get; set; }

        public List<Kept> Changes { get; } = [];

        public bool Populated { get; set; }

        public Kept? Discovery { get; set; }

        public Kept? AllFound { get; set; }

        /// <summary>Each kept event once, in the fold order.</summary>
        public Kept[] Events() =>
            [.. Changes.Concat(new[] { Entry, Discovery, AllFound }.OfType<Kept>()).DistinctBy(kept => kept.Order).OrderBy(kept => kept.Order)];
    }
}

/// <summary>The Commander's own record of one system, from their journals.</summary>
public sealed record OwnSystem(long SystemAddress)
{
    public string? Name { get; init; }

    public StarPosition? Position { get; init; }

    /// <summary>From FSSDiscoveryScan.</summary>
    public int? BodyCount { get; init; }

    public IReadOnlyList<BodyProfile> Bodies { get; init; } = [];

    public IReadOnlyList<BarycentreProfile> Barycentres { get; init; } = [];

    /// <summary>By body id.</summary>
    public IReadOnlyDictionary<int, OwnBody> Own { get; init; } = new Dictionary<int, OwnBody>();

    /// <summary>The latest event folded.</summary>
    public DateTimeOffset LatestAt { get; init; }

    /// <summary>The same events folded as the session's <see cref="CommanderGameState.Scans"/>.</summary>
    public BodyScans Scans { get; init; } = BodyScans.Empty;

    /// <summary>The same events folded as the session's <see cref="CommanderGameState.Bodies"/>.</summary>
    public BodySignals Signals { get; init; } = BodySignals.Empty;
}

/// <summary>What the Commander's own events say about one body beyond its profile.</summary>
public sealed record OwnBody(DateTimeOffset ScannedAt)
{
    public bool? WasDiscovered { get; init; }

    public bool? WasMapped { get; init; }

    /// <summary>From SAAScanComplete.</summary>
    public bool MappedByCommander { get; init; }

    /// <summary>The counts came from SAASignalsFound rather than FSSBodySignals.</summary>
    public bool SignalsFromSurfaceScan { get; init; }

    public DateTimeOffset? SignalsAt { get; init; }
}
