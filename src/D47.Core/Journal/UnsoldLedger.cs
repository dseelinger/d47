using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The state and plumbing shared by the ledgers of data carried and not yet sold, per Commander.</summary>
public abstract class UnsoldLedger(
    string? path, IFileSystem fileSystem, ILogger logger, string resetProperty, string noun, string purpose)
{
    protected Lock Gate { get; } = new();

    protected Dictionary<string, DateTimeOffset> Resets { get; } = new(StringComparer.Ordinal);

    /// <summary>When each Commander last had pilot data erased, from <c>NewCommander</c>; not written to <c>path</c>.</summary>
    protected Dictionary<string, DateTimeOffset> Erases { get; } = new(StringComparer.Ordinal);

    /// <summary>The Commander last seen. Read and written under <see cref="Gate"/>.</summary>
    protected string? Current;

    private bool _historyFolded;

    private int _revision;

    /// <summary>Whether the journals older than this session have been folded in.</summary>
    public bool HistoryFolded
    {
        get
        {
            lock (Gate)
            {
                return _historyFolded;
            }
        }
    }

    /// <summary>Changes whenever events are folded or a reset is made or read.</summary>
    public int Revision
    {
        get
        {
            lock (Gate)
            {
                return _revision;
            }
        }
    }

    /// <summary>When <paramref name="commander"/> last reset the total, or null when never.</summary>
    public DateTimeOffset? ResetAt(string commander)
    {
        lock (Gate)
        {
            return Resets.TryGetValue(commander, out var at) ? at : null;
        }
    }

    /// <summary>Reads the reset times from <c>path</c>.</summary>
    public void Load()
    {
        if (path is null)
        {
            return;
        }

        try
        {
            var resets = UnsoldDataFile.Read(fileSystem, path, resetProperty);

            lock (Gate)
            {
                foreach (var (fid, at) in resets)
                {
                    Resets[fid] = at;
                }

                _revision++;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; the unsold {Noun} total counts from the last sale or death", path, noun);
        }
    }

    /// <summary>Folds a tick's events, in order, attributing them to <paramref name="commander"/> until one names another.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events, string? commander = null)
    {
        lock (Gate)
        {
            Current ??= commander;

            foreach (var journalEvent in events)
            {
                Fold(journalEvent, ref Current);
            }

            if (events.Count > 0)
            {
                _revision++;
            }
        }
    }

    /// <summary>Folds journal files oldest first, on the calling thread; files already folded fold to no change.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        string? commander = null;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            var events = JournalHistory.Parse(fileSystem, file, logger, purpose, warnIfMissing: false, Relevant);

            lock (Gate)
            {
                foreach (var journalEvent in events)
                {
                    Fold(journalEvent, ref commander);
                }
            }
        }

        lock (Gate)
        {
            _historyFolded = true;
            _revision++;
        }
    }

    /// <summary>Clears <paramref name="commander"/>'s total as of <paramref name="at"/>, and writes the time to <c>path</c>.</summary>
    public void Reset(string commander, DateTimeOffset at)
    {
        Dictionary<string, DateTimeOffset> resets;

        lock (Gate)
        {
            Resets[commander] = at;
            _revision++;
            resets = new Dictionary<string, DateTimeOffset>(Resets, StringComparer.Ordinal);
        }

        if (path is null)
        {
            return;
        }

        try
        {
            UnsoldDataFile.Write(fileSystem, path, resetProperty, resets);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write {Path}; the reset holds until d47 restarts", path);
        }
    }

    /// <summary>The text test, before any JSON is touched.</summary>
    protected abstract bool Relevant(string line);

    /// <summary>Folds one event under <see cref="Gate"/>.</summary>
    protected abstract void Fold(JournalEvent journalEvent, ref string? commander);

    /// <summary>Folds the events that name the Commander or erase its data; true where <paramref name="journalEvent"/> was one.</summary>
    protected bool FoldCommander(JournalEvent journalEvent, ref string? commander)
    {
        switch (journalEvent.Kind)
        {
            case "Commander" or "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } fid)
                {
                    commander = fid;
                }

                return true;

            case "NewCommander":
                if (journalEvent.String("FID") is { Length: > 0 } erased
                    && (!Erases.TryGetValue(erased, out var last) || journalEvent.Timestamp > last))
                {
                    Erases[erased] = journalEvent.Timestamp;
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>When the total last restarted for <paramref name="fid"/>: the later of its reset and its erase.</summary>
    protected DateTimeOffset ResetFloor(string fid)
    {
        var reset = Resets.TryGetValue(fid, out var resetAt) ? resetAt : DateTimeOffset.MinValue;

        return Erases.TryGetValue(fid, out var erasedAt) && erasedAt > reset ? erasedAt : reset;
    }
}
