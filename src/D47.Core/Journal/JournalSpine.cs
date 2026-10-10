using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The tick-loop-facing piece: tails the newest journal file, switches to a newer one when it appears,
/// and feeds every event through <see cref="GameStateStore"/>.
/// </summary>
/// <param name="position">
/// The live <c>Status.json</c>, for stamping a position onto events that carry none — organic sampling
/// is the whole reason it exists (Phase 18).
/// </param>
public sealed class JournalSpine(
    string directory,
    IFileSystem files,
    GameStateStore gameState,
    ILoggerFactory loggerFactory,
    Func<GameStatus>? position = null)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<JournalSpine>();

    private readonly SuitInventoryReader _suit =
        new(directory, files, loggerFactory.CreateLogger<SuitInventoryReader>());

    private readonly CargoManifestReader _hold =
        new(directory, files, loggerFactory.CreateLogger<CargoManifestReader>());

    private JournalReader? _reader;

    private DateTime? _listedAt;

    private bool _relistNext;

    private string? _latest;

    public string Directory { get; } = directory;

    /// <summary>The currently-tailed file, or null if none has been found yet.</summary>
    public string? CurrentFile => _reader?.Path;

    /// <summary>The tailed file and the reader's position, or null when no file is tailed.</summary>
    public JournalMark? Mark => _reader is { } reader ? new JournalMark(reader.Path, reader.Position) : null;

    /// <summary>What each event of the last poll changed, one per event and in the same order.</summary>
    public IReadOnlyList<FoldReceipt> Receipts { get; private set; } = [];

    /// <summary><param name="priming"> Whether this poll is the startup replay.</summary>
    /// <param name="priming">Whether this poll is the startup replay.</param>
    public IReadOnlyList<JournalEvent> Poll(bool priming = false)
    {
        var latest = LatestFile();

        if (latest is null)
        {
            Receipts = [];
            return [];
        }

        if (_reader is null || _reader.Path != latest)
        {
            // A new file becoming latest means a new session — Elite restarted, or a different Commander
            // logged in.
            _logger.LogInformation("Now tailing {Path}", latest);
            _reader = new JournalReader(latest, files, _logger);
        }

        var events = _reader.Poll();

        Receipts = [.. events.Select(journalEvent => gameState.Apply(journalEvent, FixFor(journalEvent), priming))];

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
    /// The newest journal file, listing the folder only when its write time has changed since the last
    /// listing, and once more on the poll after, for a file created within the clock tick of that change.
    /// </summary>
    private string? LatestFile()
    {
        var writtenAt = files.FolderWritten(Directory);

        if (writtenAt == _listedAt && !_relistNext)
        {
            return _latest;
        }

        _relistNext = writtenAt != _listedAt;
        _listedAt = writtenAt;
        _latest = JournalFolder.LatestFile(files, Directory);
        return _latest;
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
