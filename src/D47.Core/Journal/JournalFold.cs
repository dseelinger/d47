using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The shell shared by the logs that fold live events and journal files into their own state.</summary>
internal static class JournalFold
{
    /// <summary>Folds each event under the gate.</summary>
    public static void Apply(Lock gate, IReadOnlyList<JournalEvent> events, Action<JournalEvent> fold)
    {
        ArgumentNullException.ThrowIfNull(events);

        lock (gate)
        {
            foreach (var journalEvent in events)
            {
                fold(journalEvent);
            }
        }
    }

    /// <summary>Folds journal files in the order given, on the calling thread.</summary>
    public static void Files(
        IFileSystem fileSystem,
        ILogger logger,
        IReadOnlyList<string> files,
        string purpose,
        Action<IEnumerable<string>> fold,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            fold(JournalLines.Read(fileSystem, file, logger, purpose, warnIfMissing: false));
        }
    }

    /// <summary>The lines that pass <paramref name="relevant"/> and parse, in order.</summary>
    public static List<JournalEvent> Parse(IEnumerable<string> lines, Func<string, bool> relevant, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var events = new List<JournalEvent>();

        foreach (var line in lines)
        {
            if (relevant(line) && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
            {
                events.Add(parsed);
            }
        }

        return events;
    }
}
