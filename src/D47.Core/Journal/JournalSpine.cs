using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The tick-loop-facing piece: rescans the journal folder for the newest file, switches to it
/// when it changes, and feeds every event through <see cref="GameStateStore"/>. Owns no
/// thread and no timer — it is <see cref="Poll"/>, called at whatever cadence the caller
/// chooses, exactly like <see cref="JournalReader"/> underneath it.
/// </summary>
public sealed class JournalSpine(string directory, GameStateStore gameState, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<JournalSpine>();

    private JournalReader? _reader;

    public string Directory { get; } = directory;

    /// <summary>The currently-tailed file, or null if none has been found yet.</summary>
    public string? CurrentFile => _reader?.Path;

    public IReadOnlyList<JournalEvent> Poll()
    {
        var latest = JournalFolder.LatestFile(Directory);

        if (latest is null)
        {
            return [];
        }

        if (_reader is null || _reader.Path != latest)
        {
            // A new file becoming latest means a new session — Elite restarted, or a different
            // Commander logged in. The old reader is simply dropped; existing GameStateStore
            // buckets are untouched, since only events feed those.
            _logger.LogInformation("Now tailing {Path}", latest);
            _reader = new JournalReader(latest, _logger);
        }

        var events = _reader.Poll();

        foreach (var journalEvent in events)
        {
            gameState.Apply(journalEvent);
        }

        return events;
    }
}
