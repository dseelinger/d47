using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Activities;

/// <summary>An activity and the last time it was done.</summary>
public sealed record ActivityDate(string Key, DateTimeOffset At);

/// <summary>
/// When each catalogued activity was last done, per Commander, from the journals and then live (#585).
/// Nothing is written to disk.
/// </summary>
public sealed class ActivityLedger(IFileSystem fileSystem, ILogger logger)
{
    private sealed class Book
    {
        public Dictionary<string, DateTimeOffset> Last { get; } = new(StringComparer.Ordinal);

        public DateTimeOffset? Earliest { get; set; }

        /// <summary>The latest <c>NewCommander</c>; nothing at or before it is recorded.</summary>
        public DateTimeOffset? ErasedAt { get; set; }
    }

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    private string? _current;

    private bool _historyFolded;

    /// <summary>Raised after events are folded.</summary>
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

            var events = JournalHistory.Parse(fileSystem, file, logger, "activity dates", warnIfMissing: false, Reads.Matches);

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

    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "Commander", "NewCommander", "LoadGame", "MiningRefined", "AsteroidCracked", "SAAScanComplete",
        "MultiSellExplorationData", "SellExplorationData", "Bounty", "FactionKillBond", "PowerplayMerits",
        "CollectItems", "SellMicroResources", "ScanOrganic", "SellOrganicData", "MarketBuy", "EngineerCraft",
        "CarrierJumpRequest", "MissionCompleted", "SearchAndRescue", "LaunchFighter", "ColonisationContribution");

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

            case "NewCommander":
                if (journalEvent.String("FID") is { Length: > 0 } erased)
                {
                    commander = erased;
                    Erase(Open(erased), journalEvent.Timestamp);
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

        Record(book, key, journalEvent.Timestamp);

        if (journalEvent.Kind == "MissionCompleted" && MissionFamilyKey(journalEvent.String("Name")) is { } familyKey)
        {
            Record(book, familyKey, journalEvent.Timestamp);
        }
    }

    /// <summary>Drops the dates at or before <paramref name="at"/>.</summary>
    private static void Erase(Book book, DateTimeOffset at)
    {
        if (book.ErasedAt is { } erased && erased >= at)
        {
            return;
        }

        book.ErasedAt = at;

        foreach (var key in book.Last.Where(pair => pair.Value <= at).Select(pair => pair.Key).ToList())
        {
            book.Last.Remove(key);
        }

        if (book.Earliest is not { } earliest || earliest < at)
        {
            book.Earliest = at;
        }
    }

    private static void Record(Book book, string key, DateTimeOffset at)
    {
        if (book.ErasedAt is { } erased && at <= erased)
        {
            return;
        }

        if (!book.Last.TryGetValue(key, out var last) || at > last)
        {
            book.Last[key] = at;
        }
    }

    /// <summary>The <c>mission:&lt;family&gt;</c> key for a mission name: the longest family in the draw table the name starts with.</summary>
    public static string? MissionFamilyKey(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : ChapterActivities.All
                .Where(row => row.Family is not null && name.StartsWith(row.Family, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(row => row.Family!.Length)
                .Select(row => row.Key)
                .FirstOrDefault();

    private static void Seen(Book book, DateTimeOffset at)
    {
        if (book.ErasedAt is { } erased && at < erased)
        {
            return;
        }

        if (book.Earliest is not { } earliest || at < earliest)
        {
            book.Earliest = at;
        }
    }

    private Book? BookOf(string? commander) =>
        (string.IsNullOrEmpty(commander) ? _current : commander) is { } fid && _books.TryGetValue(fid, out var book)
            ? book
            : null;

    private Book Open(string commander) => LedgerBooks.Open(_books, commander);
}
