using System.Globalization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One sale of a commodity, with what the cargo had cost.</summary>
/// <param name="At">When Elite wrote it.</param>
/// <param name="Commodity">The journal's spelling, localised where it gives one.</param>
/// <param name="Count">Tonnes sold.</param>
/// <param name="TotalSale">What the station paid, as Elite reported it.</param>
/// <param name="CostBasis">
/// What those tonnes had cost. Elite's own <c>AvgPricePaid</c> where it is non-zero, else the
/// running average of this Commander's <c>MarketBuy</c> events for the commodity, else zero — in
/// which case the sale is gross, which is the honest figure for cargo that was never bought.
/// </param>
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
    /// The figure said out loud: "2.1 million up", "412,000 down", "level". Millions to one
    /// decimal, because that is how a Commander says a credit total; below a million the
    /// numerals stand, and the synthesis seam writes them out.
    /// </summary>
    public string Said
    {
        get
        {
            if (Net == 0)
            {
                return "level";
            }

            var size = Math.Abs(Net);
            var figure = size >= 1_000_000
                ? (size / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + " million"
                : size.ToString("N0", CultureInfo.InvariantCulture);

            return Net > 0 ? $"{figure} up" : $"{figure} down";
        }
    }
}

/// <summary>A stretch of time the ledger is asked about, and what to call it.</summary>
public sealed record LedgerWindow(DateTimeOffset From, DateTimeOffset To, string Label);

/// <summary>
/// What a commodity has made or lost, per Commander, across sessions
/// (<a href="https://github.com/dseelinger/d47/issues/296">#296</a>).
/// <para>
/// <b>Net of cost, which nothing else here was.</b> <see cref="SessionSummary"/> folds
/// <c>MarketSell</c> into gross trade earnings and resets on <c>LoadGame</c>; the logbook digest
/// sums sales per commodity per day. Neither knows what the cargo cost and neither spans days, so
/// a Palladium bought at 48,200 and sold at 51,000 showed as 51,000 earned. The cost basis is
/// Elite's own <c>AvgPricePaid</c> on the sale — the game tracks it across sessions and across
/// ships — and falls back to a running average of this Commander's purchases when Elite writes
/// zero, which it does for cargo that was never bought.
/// </para>
/// <para>
/// <b>The day and the week are on disk.</b> Elite keeps its journals, so "how have I done this
/// week" is answered by folding the files that cover the week at startup and the live events
/// after that — no file under <c>data\</c>, nothing to get out of step with the journal. Folding
/// the same event twice is harmless: every sale is keyed by what Elite wrote, so the startup fold
/// and the priming replay of the current file agree rather than double-count.
/// </para>
/// <para>
/// <b>"This week" means the Community Goal's own window</b> (ruling, 2026-09-05). The window
/// opens at the first <c>CommunityGoal</c> event that names the goal and closes at its
/// <c>Expiry</c>; the calendar week is the fallback when no goal is live.
/// </para>
/// <para>
/// No clock and no thread, like everything else in Core: <c>now</c> is handed in wherever a
/// window is measured from, and folding is whoever polls the journal.
/// </para>
/// </summary>
public sealed class CommodityLedger
{
    /// <summary>
    /// How far back the startup fold reaches, as a floor. A goal runs about a week; ten days
    /// covers one that started before the calendar week did.
    /// </summary>
    public static readonly TimeSpan Lookback = TimeSpan.FromDays(10);

    private sealed class Book
    {
        public List<CommoditySale> Sales { get; } = [];

        /// <summary>Cost and count of what was bought, by commodity, for the fallback basis.</summary>
        public Dictionary<string, (long Cost, int Count)> Bought { get; } = new(StringComparer.OrdinalIgnoreCase);

        public DateTimeOffset? SessionStartedAt { get; set; }
    }

    private sealed record Sighting(int Id, string Title, DateTimeOffset FirstSeen, DateTimeOffset? Expiry);

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    private readonly Dictionary<int, Sighting> _goals = [];

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

    /// <summary>
    /// Folds the journal files on disk that cover a window, oldest first. Files are named for the
    /// moment they were opened, so a file opened before the window may run into it; the one file
    /// before the first inside the window is folded too. Returns how many files were read.
    /// </summary>
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
        var start = new DateTimeOffset(now.Date, now.Offset);

        return new LedgerWindow(start, start.AddDays(1), "today");
    }

    /// <summary>
    /// The live Community Goal's window — first sighting to expiry — or, with none live, the
    /// calendar week (Monday to Monday) <paramref name="now"/> falls in.
    /// </summary>
    public LedgerWindow Week(DateTimeOffset now)
    {
        lock (_gate)
        {
            var live = _goals.Values
                .Where(goal => goal.Expiry is { } expiry && expiry > now && goal.FirstSeen <= now)
                .OrderByDescending(goal => goal.FirstSeen)
                .FirstOrDefault();

            if (live is not null)
            {
                return new LedgerWindow(live.FirstSeen, live.Expiry!.Value, live.Title);
            }
        }

        var today = new DateTimeOffset(now.Date, now.Offset);
        var sinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-sinceMonday);

        return new LedgerWindow(monday, monday.AddDays(7), "this week");
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

    /// <summary>True when the event changed the ledger. Caller holds the gate.</summary>
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
                        // A fresh session wipes the session slate and nothing else: the day and
                        // the week are windows over the same sales.
                        Open(fid).SessionStartedAt = journalEvent.Timestamp;
                    }
                }

                return false;

            case "MarketBuy":
                if (_current is null || !Seen(journalEvent, journalEvent.Long("TotalCost")))
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
                if (_current is null || !Seen(journalEvent, journalEvent.Long("TotalSale")))
                {
                    return false;
                }

                if (journalEvent.Named("Type") is not { Length: > 0 } sold)
                {
                    return false;
                }

                var count = journalEvent.Int("Count") ?? 0;
                var owner = Open(_current);

                // Elite's own figure first. Zero means the game has no purchase to average — a
                // mission reward, a mined load — and the fallback is what this Commander paid.
                var basis = journalEvent.Long("AvgPricePaid") is { } paid && paid > 0
                    ? paid * count
                    : owner.Bought.TryGetValue(sold, out var stock) && stock.Count > 0
                        ? (long)Math.Round((double)stock.Cost / stock.Count * count)
                        : 0;

                owner.Sales.Add(new CommoditySale(
                    journalEvent.Timestamp,
                    sold,
                    count,
                    journalEvent.Long("TotalSale") ?? 0,
                    basis));

                return true;

            case "CommunityGoal":
                foreach (var entry in journalEvent.Items("CurrentGoals"))
                {
                    if (entry.Int("CGID") is not { } id || entry.String("Title") is not { } title)
                    {
                        continue;
                    }

                    var expiry = DateTimeOffset.TryParse(
                        entry.String("Expiry"),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal,
                        out var parsed)
                        ? parsed
                        : (DateTimeOffset?)null;

                    // The first sighting opens the window and stays; the title and expiry are
                    // whatever the latest board says.
                    _goals[id] = _goals.TryGetValue(id, out var known)
                        ? known with { Title = title, Expiry = expiry ?? known.Expiry }
                        : new Sighting(id, title, journalEvent.Timestamp, expiry);
                }

                return false;

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

    /// <summary>
    /// Whether this is the first time the event has been folded. Keyed by what Elite wrote —
    /// time, kind, market, commodity, count and total — so the startup fold and the live replay
    /// of the same file cannot count a sale twice.
    /// </summary>
    private bool Seen(JournalEvent journalEvent, long? total) =>
        _seen.Add(string.Join(
            '|',
            journalEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            journalEvent.Kind,
            journalEvent.Long("MarketID")?.ToString(CultureInfo.InvariantCulture),
            journalEvent.String("Type"),
            journalEvent.Int("Count")?.ToString(CultureInfo.InvariantCulture),
            total?.ToString(CultureInfo.InvariantCulture)));

    /// <summary>The moment a journal file was opened, read from its name: <c>Journal.2026-09-03T095127.01.log</c>.</summary>
    public static DateTimeOffset? OpenedAt(string path)
    {
        var name = Path.GetFileName(path);

        // "Journal." is eight characters; the stamp is the seventeen after it.
        if (name.Length < 25)
        {
            return null;
        }

        return DateTimeOffset.TryParseExact(
            name.Substring(8, 17),
            "yyyy-MM-dd'T'HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var opened)
            ? opened
            : null;
    }
}
