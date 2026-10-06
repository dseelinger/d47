using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Activities;

/// <summary>An activity and the last time it was done.</summary>
public sealed record ActivityDate(string Key, DateTimeOffset At);

/// <summary>
/// When each catalogued activity was last done, per Commander, from the journals and then live (#585).
/// Only the choice of which activities to suggest is written to disk.
/// </summary>
public sealed class ActivityLedger(string? path, ILogger logger)
{
    private sealed class Book
    {
        public Dictionary<string, DateTimeOffset> Last { get; } = new(StringComparer.Ordinal);

        public DateTimeOffset? Earliest { get; set; }

        public HashSet<string> NotSuggested { get; } = new(StringComparer.Ordinal);
    }

    private sealed class DataFile
    {
        public Dictionary<string, List<string>> NotSuggested { get; set; } = [];
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    private string? _current;

    private bool _historyFolded;

    /// <summary>Raised after events are folded or a suggestion is changed.</summary>
    public event Action? Changed;

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

    /// <summary>Reads the suggestion choices from <c>path</c>.</summary>
    public void Load()
    {
        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<DataFile>(File.ReadAllText(path), Json);

            lock (_gate)
            {
                foreach (var (fid, keys) in file?.NotSuggested ?? [])
                {
                    Open(fid).NotSuggested.UnionWith(keys);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; every activity is suggested", path);
        }

        Changed?.Invoke();
    }

    /// <summary>Folds a tick's events, in order, attributing them to <paramref name="commander"/> until one names another.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events, string? commander = null)
    {
        lock (_gate)
        {
            _current ??= commander;

            foreach (var journalEvent in events)
            {
                Fold(journalEvent, ref _current);
            }
        }

        if (events.Count > 0)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Folds journal files oldest first, on the calling thread.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        string? commander = null;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            var events = new List<JournalEvent>();

            foreach (var line in Lines(file))
            {
                if (Relevant(line) && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
                {
                    events.Add(parsed);
                }
            }

            lock (_gate)
            {
                foreach (var journalEvent in events)
                {
                    Fold(journalEvent, ref commander);
                }
            }
        }

        lock (_gate)
        {
            _historyFolded = true;
        }

        Changed?.Invoke();
    }

    /// <summary>When <paramref name="key"/> was last done, or null when the journals hold no date for it.</summary>
    public DateTimeOffset? LastDone(string key, string? commander = null)
    {
        lock (_gate)
        {
            return BookOf(commander) is { } book && book.Last.TryGetValue(key, out var at) ? at : null;
        }
    }

    /// <summary>The earliest time folded for the Commander; an undated activity reads as not done since then.</summary>
    public DateTimeOffset? CorpusFrom(string? commander = null)
    {
        lock (_gate)
        {
            return BookOf(commander)?.Earliest;
        }
    }

    /// <summary>Whether <paramref name="key"/> may be suggested; true until the Commander says otherwise.</summary>
    public bool IsSuggested(string key, string? commander = null)
    {
        lock (_gate)
        {
            return BookOf(commander) is not { } book || !book.NotSuggested.Contains(key);
        }
    }

    /// <summary>The <paramref name="count"/> oldest dated activities that may be suggested, oldest first.</summary>
    public IReadOnlyList<ActivityDate> Stalest(int count, string? commander = null)
    {
        lock (_gate)
        {
            if (BookOf(commander) is not { } book)
            {
                return [];
            }

            return
            [
                .. book.Last
                    .Where(pair => !book.NotSuggested.Contains(pair.Key))
                    .OrderBy(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(Math.Max(0, count))
                    .Select(pair => new ActivityDate(pair.Key, pair.Value)),
            ];
        }
    }

    /// <summary>Sets whether <paramref name="key"/> may be suggested to <paramref name="commander"/>, and writes the choice.</summary>
    public void SetSuggested(string key, string commander, bool suggested)
    {
        DataFile file;

        lock (_gate)
        {
            var book = Open(commander);

            if (suggested)
            {
                book.NotSuggested.Remove(key);
            }
            else
            {
                book.NotSuggested.Add(key);
            }

            file = new DataFile
            {
                NotSuggested = _books
                    .Where(pair => pair.Value.NotSuggested.Count > 0)
                    .ToDictionary(pair => pair.Key, pair => pair.Value.NotSuggested.Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal),
            };
        }

        if (path is not null)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                AtomicFile.WriteAllText(path, JsonSerializer.Serialize(file, Json));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not write {Path}; the choice holds until d47 restarts", path);
            }
        }

        Changed?.Invoke();
    }

    private static bool Relevant(string line) =>
        line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MiningRefined\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"AsteroidCracked\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SAAScanComplete\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MultiSellExplorationData\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SellExplorationData\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Bounty\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"FactionKillBond\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"PowerplayMerits\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"CollectItems\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SellMicroResources\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"ScanOrganic\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SellOrganicData\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MarketBuy\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"EngineerCraft\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"CarrierJumpRequest\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MissionCompleted\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SearchAndRescue\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"LaunchFighter\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"ColonisationContribution\"", StringComparison.Ordinal);

    private void Fold(JournalEvent journalEvent, ref string? commander)
    {
        string key;

        switch (journalEvent.Kind)
        {
            case "Commander" or "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } fid)
                {
                    commander = fid;
                }

                if (commander is not null)
                {
                    Seen(Open(commander), journalEvent.Timestamp);
                }

                return;

            case "MiningRefined" or "AsteroidCracked":
                key = "mining";
                break;

            case "SAAScanComplete" or "MultiSellExplorationData" or "SellExplorationData":
                key = "exploration";
                break;

            case "Bounty":
                key = "bounty-hunting";
                break;

            case "FactionKillBond":
                key = "combat-zones";
                break;

            case "PowerplayMerits":
                key = "powerplay";
                break;

            case "CollectItems" or "SellMicroResources":
                key = "on-foot";
                break;

            case "ScanOrganic" or "SellOrganicData":
                key = "exobiology";
                break;

            case "MarketBuy":
                key = "trading";
                break;

            case "EngineerCraft":
                key = "engineering";
                break;

            case "CarrierJumpRequest":
                key = "carrier";
                break;

            case "MissionCompleted":
                key = "missions";
                break;

            case "SearchAndRescue":
                key = "search-and-rescue";
                break;

            case "LaunchFighter":
                if (!journalEvent.Bool("PlayerControlled"))
                {
                    return;
                }

                key = "fighter";
                break;

            case "ColonisationContribution":
                key = "colonisation";
                break;

            default:
                return;
        }

        if (commander is null)
        {
            return;
        }

        var book = Open(commander);

        Seen(book, journalEvent.Timestamp);

        if (!book.Last.TryGetValue(key, out var last) || journalEvent.Timestamp > last)
        {
            book.Last[key] = journalEvent.Timestamp;
        }
    }

    private static void Seen(Book book, DateTimeOffset at)
    {
        if (book.Earliest is not { } earliest || at < earliest)
        {
            book.Earliest = at;
        }
    }

    private Book? BookOf(string? commander) =>
        (string.IsNullOrEmpty(commander) ? _current : commander) is { } fid && _books.TryGetValue(fid, out var book)
            ? book
            : null;

    private Book Open(string commander)
    {
        if (!_books.TryGetValue(commander, out var book))
        {
            book = new Book();
            _books[commander] = book;
        }

        return book;
    }

    private IEnumerable<string> Lines(string file)
    {
        FileStream stream;

        try
        {
            stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for activity dates", file);
            yield break;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }
}
