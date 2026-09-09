using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The tick-loop-facing piece: rescans the journal folder for the newest file, switches to it when it
/// changes, and feeds every event through <see cref="GameStateStore"/>.
/// </summary>
/// <param name="position">
/// The live <c>Status.json</c>, for stamping a position onto events that carry none — organic sampling
/// is the whole reason it exists (Phase 18).
/// </param>
public sealed class JournalSpine(
    string directory,
    GameStateStore gameState,
    ILoggerFactory loggerFactory,
    Func<GameStatus>? position = null)
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

    /// <summary><param name="priming"> Whether this poll is the startup replay.</summary>
    /// <param name="priming">Whether this poll is the startup replay.</param>
    public IReadOnlyList<JournalEvent> Poll(bool priming = false)
    {
        var latest = JournalFolder.LatestFile(Directory);

        if (latest is null)
        {
            return [];
        }

        if (_reader is null || _reader.Path != latest)
        {
            // A new file becoming latest means a new session — Elite restarted, or a different Commander
            // logged in.
            _logger.LogInformation("Now tailing {Path}", latest);
            _reader = new JournalReader(latest, _logger);
        }

        var events = _reader.Poll();

        foreach (var journalEvent in events)
        {
            gameState.Apply(journalEvent, FixFor(journalEvent), priming);
        }

        // After the events, so the Commander whose locker this is has been established by them on the very
        // first poll.
        if (_suit.Poll() && gameState.Active is { } active)
        {
            active.Suit = _suit.Current;
        }

        // Cargo.json, on the same terms as the two above: same folder, same cadence, same reason for reading
        // a file rather than an event.
        if (_hold.Poll() && gameState.Active is { } carrying)
        {
            carrying.Hold = _hold.Current;
        }

        return events;
    }

    /// <summary>
    /// How far apart a journal line and a <c>Status.json</c> write may be and still describe the same
    /// moment.
    /// </summary>
    private static readonly TimeSpan SameMoment = TimeSpan.FromSeconds(10);

    private SurfaceFix? FixFor(JournalEvent journalEvent)
    {
        if (position?.Invoke() is not { HasPosition: true } status
            || status.ReadAt is not { } readAt
            || status.PlanetRadius is not { } radius)
        {
            return null;
        }

        return (readAt - journalEvent.Timestamp).Duration() <= SameMoment
            ? new SurfaceFix(status.Latitude!.Value, status.Longitude!.Value, radius)
            : null;
    }
}
