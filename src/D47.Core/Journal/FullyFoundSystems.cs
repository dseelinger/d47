using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The systems each Commander's journals show with every body found, by <c>SystemAddress</c>. Rebuilt from
/// the journals on every start and extended by live events; holds no file.
/// </summary>
public sealed class FullyFoundSystems(IFileSystem fileSystem, ILogger logger)
{
    private readonly Lock _gate = new();

    private readonly Dictionary<string, HashSet<long>> _found = new(StringComparer.Ordinal);

    private string _liveFid = string.Empty;

    /// <summary>Folds live events; a synchronous add with no I/O.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        JournalFold.Apply(_gate, events, e => Fold(e, ref _liveFid));
    }

    /// <summary>Folds journal files oldest first, on the calling thread.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        JournalFold.Files(fileSystem, logger, files, "fully found systems", Fold, cancellation);
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

    /// <summary>Whether a journal of this Commander shows every body of the system found.</summary>
    public bool Contains(string fid, long systemAddress)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _found.TryGetValue(fid, out var set) && set.Contains(systemAddress);
        }
    }

    /// <summary>How many systems a Commander has found whole.</summary>
    public int Count(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _found.TryGetValue(fid, out var set) ? set.Count : 0;
        }
    }

    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "Commander", "NewCommander", "LoadGame", "GameModeChange", "Fileheader", "Shutdown", "FSSAllBodiesFound",
        "FSSDiscoveryScan");

    private void Fold(JournalEvent journalEvent, ref string fid)
    {
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
                    _found.Remove(erased);
                }

                return;

            case "FSSAllBodiesFound":
            case "FSSDiscoveryScan" when journalEvent.Double("Progress") >= 1:
                if (fid.Length == 0 || journalEvent.InOperation || journalEvent.Long("SystemAddress") is not { } address)
                {
                    return;
                }

                if (!_found.TryGetValue(fid, out var set))
                {
                    set = [];
                    _found[fid] = set;
                }

                set.Add(address);
                return;
        }
    }
}
