using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// Which journal files hold each Commander's exploration events for each system, by <c>SystemAddress</c>, and the
/// session's live events. Rebuilt from the journals on every start and extended by live events; holds no file and
/// no scans: <see cref="Read"/> reads them back.
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

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Dictionary<long, List<string>>> _files = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Dictionary<long, List<JournalEvent>>> _live = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Fid, long SystemAddress), int> _versions = [];

    private string _liveFid = string.Empty;

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
        JournalEvent[] live;

        lock (_gate)
        {
            files = _files.TryGetValue(fid, out var systems) && systems.TryGetValue(systemAddress, out var named) ? [.. named] : [];
            live = _live.TryGetValue(fid, out var held) && held.TryGetValue(systemAddress, out var events) ? [.. events] : [];
        }

        if (files.Length == 0 && live.Length == 0)
        {
            return null;
        }

        var found = new List<JournalEvent>();

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            Collect(JournalLines.Read(fileSystem, file, logger, Purpose, warnIfMissing: false), fid, systemAddress, found);
        }

        found.AddRange(live);

        return OwnSystemFold.Fold(systemAddress, found);
    }

    private static bool IsExploration(JournalEvent journalEvent) => Exploration.Contains(journalEvent.Kind);

    private void FoldFile(string file, IEnumerable<string> lines)
    {
        var events = JournalFold.Parse(lines, Reads.Matches, logger);
        var fid = string.Empty;

        lock (_gate)
        {
            foreach (var journalEvent in OperationMarker.Mark(events))
            {
                if (Commander(journalEvent, ref fid, live: false) || fid.Length == 0 || journalEvent.InOperation || !IsExploration(journalEvent)
                    || journalEvent.Long("SystemAddress") is not { } address)
                {
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
    private void Collect(IEnumerable<string> lines, string fid, long systemAddress, List<JournalEvent> found)
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
                found.Add(journalEvent);
            }
        }
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
