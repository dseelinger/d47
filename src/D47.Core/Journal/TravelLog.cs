using System.Globalization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>How a Commander arrived in a system.</summary>
public enum TravelArrival
{
    Jump,
    CarrierJump,
    Location,
}

/// <summary>One arrival in a system, with where the system is.</summary>
public sealed record TravelStep(
    DateTimeOffset At,
    long SystemAddress,
    string System,
    StarPosition Position,
    TravelArrival How);

/// <summary>A system the Commander died in, with where it is and how many times the Commander died there.</summary>
public sealed record DeathPlace(long SystemAddress, string System, StarPosition Position, int Count);

/// <summary>A system where the Commander scanned bodies nobody had discovered, with where it is and how many bodies.</summary>
public sealed record FirstDiscoveryPlace(long SystemAddress, string System, StarPosition Position, int Bodies);

/// <summary>
/// Every system each Commander has arrived in, oldest first, when each Commander died, and the bodies each was the
/// first to discover. A death is in the system of the arrival at or before its time; a first discovery is in the
/// system its scan names. Rebuilt from the journals on every start and extended by live events; holds no file.
/// </summary>
public sealed class TravelLog(IFileSystem fileSystem, ILogger logger)
{
    private readonly Lock _gate = new();

    private readonly Dictionary<string, List<TravelStep>> _steps = new(StringComparer.Ordinal);

    private readonly Dictionary<string, SortedSet<DateTimeOffset>> _deaths = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Dictionary<(long System, string Body), DateTimeOffset>> _discoveries = new(StringComparer.Ordinal);

    private string _liveFid = string.Empty;

    private bool _historyFolded;

    /// <summary>Whether the journals older than this session have been folded in.</summary>
    public bool HistoryFolded
    {
        get
        {
            lock (_gate)
            {
                return _historyFolded;
            }
        }
    }

    /// <summary>Folds live events; a synchronous append with no I/O.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        JournalFold.Apply(_gate, events, e => Fold(e, ref _liveFid));
    }

    /// <summary>Folds journal files oldest first, on the calling thread.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        JournalFold.Files(fileSystem, logger, files, "travel", Fold, cancellation);

        lock (_gate)
        {
            _historyFolded = true;
        }
    }

    /// <summary>Folds the lines of one journal file in order.</summary>
    public void Fold(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var events = JournalFold.Parse(lines, Reads.Matches, logger);

        var fid = string.Empty;

        lock (_gate)
        {
            foreach (var journalEvent in OperationMarker.Mark(events))
            {
                Fold(journalEvent, ref fid);
            }
        }
    }

    /// <summary>A copy, oldest first; empty for a Commander with none.</summary>
    public IReadOnlyList<TravelStep> Steps(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _steps.TryGetValue(fid, out var steps) ? [.. steps] : [];
        }
    }

    /// <summary>How many steps a Commander has; zero for one with none.</summary>
    public int Count(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _steps.TryGetValue(fid, out var steps) ? steps.Count : 0;
        }
    }

    /// <summary>The systems a Commander died in, with how many times each, ordered by name; a death before any arrival is left out.</summary>
    public IReadOnlyList<DeathPlace> Deaths(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            if (!_deaths.TryGetValue(fid, out var times) || !_steps.TryGetValue(fid, out var steps))
            {
                return [];
            }

            var places = new Dictionary<long, DeathPlace>();

            foreach (var time in times)
            {
                if (StepAt(steps, time) is not { } step)
                {
                    continue;
                }

                places[step.SystemAddress] = places.TryGetValue(step.SystemAddress, out var seen)
                    ? seen with { Count = seen.Count + 1 }
                    : new DeathPlace(step.SystemAddress, step.System, step.Position, 1);
            }

            return [.. places.Values.OrderBy(place => place.System, StringComparer.OrdinalIgnoreCase)];
        }
    }

    /// <summary>How many deaths a Commander has recorded, placed or not; zero for one with none.</summary>
    public int DeathCount(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _deaths.TryGetValue(fid, out var times) ? times.Count : 0;
        }
    }

    /// <summary>
    /// The systems where a Commander was the first to discover a body, with how many bodies each, ordered by name; a
    /// system with no arrival is left out.
    /// </summary>
    public IReadOnlyList<FirstDiscoveryPlace> FirstDiscoveries(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            if (!_discoveries.TryGetValue(fid, out var bodies) || !_steps.TryGetValue(fid, out var steps))
            {
                return [];
            }

            var arrived = new Dictionary<long, TravelStep>();

            foreach (var step in steps)
            {
                arrived[step.SystemAddress] = step;
            }

            var places = new List<FirstDiscoveryPlace>();

            foreach (var system in bodies.Keys.GroupBy(body => body.System))
            {
                if (arrived.TryGetValue(system.Key, out var step))
                {
                    places.Add(new FirstDiscoveryPlace(system.Key, step.System, step.Position, system.Count()));
                }
            }

            return [.. places.OrderBy(place => place.System, StringComparer.OrdinalIgnoreCase)];
        }
    }

    /// <summary>How many bodies a Commander was the first to discover, placed or not; zero for one with none.</summary>
    public int FirstDiscoveryCount(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _discoveries.TryGetValue(fid, out var bodies) ? bodies.Count : 0;
        }
    }

    /// <summary>The latest step at or before <paramref name="time"/>, or null when there is none.</summary>
    private static TravelStep? StepAt(List<TravelStep> steps, DateTimeOffset time)
    {
        var low = 0;
        var high = steps.Count;

        while (low < high)
        {
            var middle = (low + high) / 2;

            if (steps[middle].At <= time)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low == 0 ? null : steps[low - 1];
    }

    /// <summary>The position of the latest visit to a system, by name without regard to case, or null.</summary>
    public StarPosition? PositionOf(string fid, string system)
    {
        ArgumentNullException.ThrowIfNull(fid);
        ArgumentNullException.ThrowIfNull(system);

        lock (_gate)
        {
            if (_steps.TryGetValue(fid, out var steps))
            {
                for (var index = steps.Count - 1; index >= 0; index--)
                {
                    if (string.Equals(steps[index].System, system, StringComparison.OrdinalIgnoreCase))
                    {
                        return steps[index].Position;
                    }
                }
            }

            return null;
        }
    }

    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "Commander", "NewCommander", "LoadGame", "GameModeChange", "Fileheader", "Shutdown", "Location", "FSDJump",
        "CarrierJump", "Died", "Scan");

    private void Fold(JournalEvent journalEvent, ref string fid)
    {
        TravelArrival how;

        switch (journalEvent.Kind)
        {
            case "Commander" or "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } named)
                {
                    fid = named;
                }

                return;

            case "NewCommander":
                if (journalEvent.String("FID") is { Length: > 0 } erased)
                {
                    fid = erased;

                    if (_steps.TryGetValue(erased, out var before))
                    {
                        before.RemoveAll(step => step.At <= journalEvent.Timestamp);
                    }

                    _deaths.GetValueOrDefault(erased)?.RemoveWhere(time => time <= journalEvent.Timestamp);

                    if (_discoveries.TryGetValue(erased, out var found))
                    {
                        foreach (var erasedBody in found.Where(pair => pair.Value <= journalEvent.Timestamp).Select(pair => pair.Key).ToList())
                        {
                            found.Remove(erasedBody);
                        }
                    }
                }

                return;

            case "Scan":
                if (fid.Length > 0
                    && FirstDiscovery.Is(journalEvent.Raw)
                    && journalEvent.Long("SystemAddress") is { } scanned
                    && (journalEvent.Int("BodyID")?.ToString(CultureInfo.InvariantCulture) ?? journalEvent.String("BodyName")) is { } body)
                {
                    if (!_discoveries.TryGetValue(fid, out var bodies))
                    {
                        bodies = [];
                        _discoveries[fid] = bodies;
                    }

                    bodies.TryAdd((scanned, body), journalEvent.Timestamp);
                }

                return;

            case "Died":
                if (fid.Length > 0)
                {
                    if (!_deaths.TryGetValue(fid, out var times))
                    {
                        times = [];
                        _deaths[fid] = times;
                    }

                    times.Add(journalEvent.Timestamp);
                }

                return;

            case "FSDJump":
                how = TravelArrival.Jump;
                break;

            case "CarrierJump":
                how = TravelArrival.CarrierJump;
                break;

            case "Location" when !journalEvent.InOperation:
                how = TravelArrival.Location;
                break;

            default:
                return;
        }

        if (fid.Length == 0
            || journalEvent.Long("SystemAddress") is not { } address
            || journalEvent.String("StarSystem") is not { } system
            || StarPosition.Read(journalEvent.Raw) is not { } position)
        {
            return;
        }

        if (!_steps.TryGetValue(fid, out var steps))
        {
            steps = [];
            _steps[fid] = steps;
        }

        var at = journalEvent.Timestamp;
        var index = steps.Count;

        while (index > 0 && steps[index - 1].At > at)
        {
            index--;
        }

        for (var same = index - 1; same >= 0 && steps[same].At == at; same--)
        {
            if (steps[same].SystemAddress == address)
            {
                return;
            }
        }

        steps.Insert(index, new TravelStep(at, address, system, position, how));
    }
}
