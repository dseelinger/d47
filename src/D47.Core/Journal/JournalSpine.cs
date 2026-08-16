using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The tick-loop-facing piece: rescans the journal folder for the newest file, switches to it
/// when it changes, and feeds every event through <see cref="GameStateStore"/>. Owns no
/// thread and no timer — it is <see cref="Poll"/>, called at whatever cadence the caller
/// chooses, exactly like <see cref="JournalReader"/> underneath it.
/// <para>
/// It also polls the three inventory files — the two on-foot ones and the cargo hold — which sit
/// in the same folder but are state rather than a log: Elite rewrites them in place instead of
/// appending. Same folder, same cadence, same <see cref="Poll"/> — the difference is entirely
/// inside <see cref="SuitInventoryReader"/> and <see cref="CargoManifestReader"/>.
/// </para>
/// </summary>
public sealed class JournalSpine(string directory, GameStateStore gameState, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<JournalSpine>();

    private readonly SuitInventoryReader _suit =
        new(directory, loggerFactory.CreateLogger<SuitInventoryReader>());

    private readonly CargoManifestReader _hold =
        new(directory, loggerFactory.CreateLogger<CargoManifestReader>());

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

        // After the events, so the Commander whose locker this is has been established by them
        // on the very first poll. The files carry no identity of their own — they belong to
        // whoever is playing, which only the journal can say.
        if (_suit.Poll() && gameState.Active is { } active)
        {
            active.Suit = _suit.Current;
        }

        // Cargo.json, on the same terms as the two above: same folder, same cadence, same reason
        // for reading a file rather than an event.
        if (_hold.Poll() && gameState.Active is { } carrying)
        {
            carrying.Hold = _hold.Current;
        }

        return events;
    }
}
