using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>When each Commander last erased their pilot data, from <c>NewCommander</c>, by Frontier id.</summary>
public sealed class CommanderErases(IFileSystem fileSystem, ILogger<CommanderErases> logger)
{
    private const string Marker = "\"event\":\"NewCommander\"";

    private readonly Dictionary<string, DateTimeOffset> _erases = new(StringComparer.Ordinal);

    private readonly object _gate = new();

    /// <summary>Raised with the Frontier id and time when a Commander's latest erase moves forward.</summary>
    public event Action<string, DateTimeOffset>? Changed;

    /// <summary>The Commander's latest erase, or null when none is known.</summary>
    public DateTimeOffset? Of(string frontierId)
    {
        ArgumentNullException.ThrowIfNull(frontierId);

        lock (_gate)
        {
            return _erases.TryGetValue(frontierId, out var at) ? at : null;
        }
    }

    /// <summary>Folds every journal file, on the calling thread. Raises <see cref="Changed"/> for each Commander it moves.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            try
            {
                using var stream = fileSystem.OpenRead(file);

                if (stream is null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream);

                while (reader.ReadLine() is { } line)
                {
                    if (line.Contains(Marker, StringComparison.Ordinal)
                        && JournalEvent.TryParse(line, logger, out var journalEvent))
                    {
                        Apply(journalEvent!);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {File} for Commander erases", file);
            }
        }
    }

    /// <summary>Moves a Commander's erase forward when the event is a later <c>NewCommander</c>.</summary>
    public void Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind != "NewCommander" || journalEvent.String("FID") is not { Length: > 0 } fid)
        {
            return;
        }

        lock (_gate)
        {
            if (_erases.TryGetValue(fid, out var last) && journalEvent.Timestamp <= last)
            {
                return;
            }

            _erases[fid] = journalEvent.Timestamp;
        }

        Changed?.Invoke(fid, journalEvent.Timestamp);
    }

    /// <summary>Folds a batch of live events.</summary>
    public void Apply(IEnumerable<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var journalEvent in events)
        {
            Apply(journalEvent);
        }
    }
}
