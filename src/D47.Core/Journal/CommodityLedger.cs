using System.Globalization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One sale of a commodity, with what the cargo had cost.</summary>
/// <param name="At">When Elite wrote it.</param>
/// <param name="Commodity">The journal's spelling, localised where it gives one.</param>
/// <param name="Count">Tonnes sold.</param>
/// <param name="TotalSale">What the station paid, as Elite reported it.</param>
/// <param name="CostBasis">What those tonnes had cost.</param>
public sealed record CommoditySale(
    DateTimeOffset At,
    string Commodity,
    int Count,
    long TotalSale,
    long CostBasis)
{
    /// <summary>Gain or loss on the sale, net of what the cargo cost.</summary>
    public long Net => TotalSale - CostBasis;

    public int UnitPrice => Count > 0 ? (int)(TotalSale / Count) : 0;

    public int UnitPaid => Count > 0 ? (int)(CostBasis / Count) : 0;
}

/// <summary>Sales added up over a window.</summary>
public sealed record LedgerTotal(long Net, int Sales, int Tonnes, long Revenue, long Cost)
{
    public static readonly LedgerTotal Empty = new(0, 0, 0, 0, 0);

    /// <summary>
    /// The figure said out loud: "2.1 billion up", "412,000 down", "level" — banded by <see
    /// cref="SpokenCredits.Band"/>, because that is how a Commander says a credit total.
    /// </summary>
    public string Said
    {
        get
        {
            if (Net == 0)
            {
                return "level";
            }

            var figure = SpokenCredits.Band(Net);

            return Net > 0 ? $"{figure} up" : $"{figure} down";
        }
    }
}

/// <summary>A stretch of time the ledger is asked about, and what to call it.</summary>
public sealed record LedgerWindow(DateTimeOffset From, DateTimeOffset To, string Label);

/// <summary>What a commodity has made or lost, per Commander, across sessions (#296).</summary>
public sealed class CommodityLedger
{
    /// <summary>How far back the startup fold reaches, as a floor.</summary>
    public static readonly TimeSpan Lookback = TimeSpan.FromDays(10);

    private sealed class Book
    {
        public List<CommoditySale> Sales { get; } = [];

        /// <summary>Cost and count of what was bought, by commodity, for the fallback basis.</summary>
        public Dictionary<string, (long Cost, int Count)> Bought { get; } = new(StringComparer.OrdinalIgnoreCase);

        public DateTimeOffset? SessionStartedAt { get; set; }
    }

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    private string? _current;

    /// <summary>Raised after a fold that changed something, so a page can redraw without polling.</summary>
    public event Action? Changed;

    /// <summary>The Commander the most recent journal event belonged to, by Frontier id.</summary>
    public string? CurrentCommander
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Folds a tick's events, in order.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        var changed = false;

        lock (_gate)
        {
            foreach (var journalEvent in events)
            {
                changed |= Fold(journalEvent);
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void Apply(JournalEvent journalEvent) => Apply([journalEvent]);

    /// <summary>Folds the journal files on disk that cover a window, oldest first.</summary>
    public int FoldHistory(string directory, DateTimeOffset since, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}; the commodity ledger starts empty", directory);
            return 0;
        }

        var files = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var first = files.FindIndex(file => OpenedAt(file) is { } opened && opened >= since);
        var start = first < 0 ? files.Count - 1 : Math.Max(0, first - 1);

        // The file the fold starts at is frequently a continuation (…T095127.02.log) carrying no identity
        // event of its own.
        lock (_gate)
        {
            for (var i = start - 1; i >= 0 && _current is null; i--)
            {
                _current = LatestIdentity(files[i], logger);
            }
        }

        var read = 0;

        for (var i = Math.Max(0, start); i < files.Count; i++)
        {
            var reader = new JournalReader(files[i], logger);

            while (reader.Poll() is { Count: > 0 } batch)
            {
                Apply(batch);
            }

            read++;
        }

        logger.LogInformation("Commodity ledger folded {Count} journal files back to {Since:u}", read, since);

        return read;
    }

    /// <summary>The last Commander/LoadGame FID a file carries, without folding it (#314).</summary>
    private static string? LatestIdentity(string file, ILogger logger)
    {
        string? fid = null;

        // FileShare.ReadWrite | Delete for the reason JournalReader gives: Elite holds a journal open for
        // writing for the whole session, and a plain read share fails against that.
        using var stream = new FileStream(
            file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        using var lines = new StreamReader(stream);

        while (lines.ReadLine() is { } line)
        {
            if (line.Contains("\"FID\"", StringComparison.Ordinal)
                && JournalEvent.TryParse(line, logger, out var parsed)
                && CommanderIdentity.From(parsed!)?.FrontierId is { Length: > 0 } found)
            {
                fid = found;
            }
        }

        return fid;
    }

    /// <summary>Sales of a commodity since the Commander's LoadGame, or all of them before one is seen.</summary>
    public LedgerTotal Session(string? commander, string commodity)
    {
        lock (_gate)
        {
            if (BookFor(commander) is not { } book)
            {
                return LedgerTotal.Empty;
            }

            return Total(book.Sales.Where(sale =>
                Matches(sale, commodity) && (book.SessionStartedAt is not { } start || sale.At >= start)));
        }
    }

    /// <summary>Sales inside a window, ends inclusive at the start and exclusive at the end.</summary>
    public LedgerTotal Between(string? commander, string commodity, LedgerWindow window)
    {
        lock (_gate)
        {
            if (BookFor(commander) is not { } book)
            {
                return LedgerTotal.Empty;
            }

            return Total(book.Sales.Where(sale =>
                Matches(sale, commodity) && sale.At >= window.From && sale.At < window.To));
        }
    }

    /// <summary>The most recent sale of the commodity, or null.</summary>
    public CommoditySale? LastSale(string? commander, string commodity)
    {
        lock (_gate)
        {
            return BookFor(commander)?.Sales.LastOrDefault(sale => Matches(sale, commodity));
        }
    }

    /// <summary>The calendar day <paramref name="now"/> falls in, in its own offset.</summary>
    public static LedgerWindow Today(DateTimeOffset now)
    {
        var start = LocalMidnight(now);

        return new LedgerWindow(start, start.AddDays(1), "today");
    }

    /// <summary>
    /// Local midnight on the day <paramref name="now"/> falls in, stamped with the offset that was
    /// actually in force AT that midnight — not <paramref name="now"/>'s own offset, which on a DST
    /// transition day can be an hour off and lose sales at the boundary (#312).
    /// </summary>
    private static DateTimeOffset LocalMidnight(DateTimeOffset now) =>
        Conversation.SpendPeriods.StartOfLocalDay(now.Date, TimeZoneInfo.Local);

    /// <summary>
    /// The Elite week <paramref name="now"/> falls in (#332): the stretch between two boundaries the
    /// galaxy itself turns on — <paramref name="boundaryDay"/>/<paramref name="boundaryHourUtc"/>,
    /// Thursday 07:00 UTC by default, where the Powerplay cycle, the BGS tick and the weekly server
    /// maintenance all sit.
    /// </summary>
    public static LedgerWindow Week(DateTimeOffset now, DayOfWeek boundaryDay, int boundaryHourUtc)
    {
        var utcNow = now.ToUniversalTime();
        var sinceBoundary = ((int)utcNow.DayOfWeek - (int)boundaryDay + 7) % 7;

        var boundary = new DateTimeOffset(utcNow.Date, TimeSpan.Zero)
            .AddDays(-sinceBoundary)
            .AddHours(boundaryHourUtc);

        if (boundary > utcNow)
        {
            boundary = boundary.AddDays(-7);
        }

        return new LedgerWindow(boundary, boundary.AddDays(7), "this week");
    }

    private Book? BookFor(string? commander)
    {
        var key = commander ?? _current;

        return key is not null && _books.TryGetValue(key, out var book) ? book : null;
    }

    private static bool Matches(CommoditySale sale, string commodity) =>
        string.Equals(
            sale.Commodity.Replace(" ", string.Empty),
            commodity.Replace(" ", string.Empty),
            StringComparison.OrdinalIgnoreCase);

    private static LedgerTotal Total(IEnumerable<CommoditySale> sales)
    {
        var total = LedgerTotal.Empty;

        foreach (var sale in sales)
        {
            total = new LedgerTotal(
                total.Net + sale.Net,
                total.Sales + 1,
                total.Tonnes + sale.Count,
                total.Revenue + sale.TotalSale,
                total.Cost + sale.CostBasis);
        }

        return total;
    }

    /// <summary>True when the event changed the ledger.</summary>
    private bool Fold(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Commander":
            case "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } fid)
                {
                    _current = fid;

                    if (journalEvent.Kind == "LoadGame")
                    {
                        // A fresh session wipes the session slate and nothing else: the day and the week are
                        // windows over the same sales.
                        Open(fid).SessionStartedAt = journalEvent.Timestamp;
                    }
                }

                return false;

            case "MarketBuy":
                if (_current is null
                    || !Seen(journalEvent, journalEvent.Long("TotalCost"), journalEvent.Long("BuyPrice")))
                {
                    return false;
                }

                if (journalEvent.Named("Type") is { Length: > 0 } bought)
                {
                    var book = Open(_current);
                    var held = book.Bought.GetValueOrDefault(bought);

                    book.Bought[bought] = (
                        held.Cost + (journalEvent.Long("TotalCost") ?? 0),
                        held.Count + (journalEvent.Int("Count") ?? 0));
                }

                return false;

            case "MarketSell":
                if (_current is null
                    || !Seen(journalEvent, journalEvent.Long("TotalSale"), journalEvent.Long("AvgPricePaid")))
                {
                    return false;
                }

                if (journalEvent.Named("Type") is not { Length: > 0 } sold)
                {
                    return false;
                }

                var count = journalEvent.Int("Count") ?? 0;
                var owner = Open(_current);

                // What this sale takes out of the lot, which is one figure and not two: the fallback basis is
                // by definition what the tonnage drawn down had cost.
                var lot = owner.Bought.GetValueOrDefault(sold);
                var drawn = Math.Min(count, lot.Count);
                var drawnCost = lot.Count > 0
                    ? (long)Math.Round((double)lot.Cost / lot.Count * drawn)
                    : 0;

                // Elite's own figure first.
                var basis = journalEvent.Long("AvgPricePaid") is { } paid && paid > 0
                    ? paid * count
                    : drawnCost;

                // Bought is a lifetime running total unless drawn down here: without this, a later sale of
                // mined/free cargo falls back to a stale purchase price for tonnage that has already been
                // sold off (#304).
                if (lot.Count > 0)
                {
                    owner.Bought[sold] = (lot.Cost - drawnCost, lot.Count - drawn);
                }

                owner.Sales.Add(new CommoditySale(
                    journalEvent.Timestamp,
                    sold,
                    count,
                    journalEvent.Long("TotalSale") ?? 0,
                    basis));

                return true;

            default:
                return false;
        }
    }

    private Book Open(string commander)
    {
        if (!_books.TryGetValue(commander, out var book))
        {
            book = new Book();
            _books[commander] = book;
        }

        return book;
    }

    /// <summary>Whether this is the first time the event has been folded.</summary>
    private bool Seen(JournalEvent journalEvent, long? total, long? price) =>
        _seen.Add(string.Join(
            '|',
            journalEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            journalEvent.Kind,
            journalEvent.Long("MarketID")?.ToString(CultureInfo.InvariantCulture),
            journalEvent.String("Type"),
            journalEvent.Int("Count")?.ToString(CultureInfo.InvariantCulture),
            total?.ToString(CultureInfo.InvariantCulture),
            price?.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// The moment a journal file was opened, read from its name:
    /// <c>Journal.2026-09-03T095127.01.log</c>.
    /// </summary>
    public static DateTimeOffset? OpenedAt(string path)
    {
        var name = Path.GetFileName(path);

        // "Journal." is eight characters; the stamp is the seventeen after it.
        if (name.Length < 25)
        {
            return null;
        }

        // Elite stamps the journal filename in the machine's LOCAL time; only the timestamps inside the file
        // are UTC (#306).
        return DateTimeOffset.TryParseExact(
            name.Substring(8, 17),
            "yyyy-MM-dd'T'HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var opened)
            ? opened
            : null;
    }
}
