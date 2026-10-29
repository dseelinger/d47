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

/// <summary>
/// Every system each Commander has arrived in, oldest first. Rebuilt from the journals on every start and
/// extended by live events; holds no file.
/// </summary>
public sealed class TravelLog(IFileSystem fileSystem, ILogger logger)
{
    private readonly Lock _gate = new();

    private readonly Dictionary<string, List<TravelStep>> _steps = new(StringComparer.Ordinal);

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

        var events = JournalFold.Parse(lines, Relevant, logger);

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

    private static bool Relevant(string line) =>
        line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"NewCommander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"GameModeChange\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Fileheader\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Shutdown\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Location\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"FSDJump\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"CarrierJump\"", StringComparison.Ordinal);

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
