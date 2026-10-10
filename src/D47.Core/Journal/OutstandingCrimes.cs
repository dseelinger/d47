using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>What the Commander owes one faction, on one ship or on foot.</summary>
/// <param name="ShipId">The ship the debt is attached to, or null for an on-foot debt.</param>
public sealed record CrimeDebt(string Faction, int? ShipId, long Fines, long Bounties, DateTimeOffset LastCrime)
{
    public long Total => Fines + Bounties;
}

/// <summary>
/// Unpaid fines and bounties per Commander, across sessions (#639): a ledger per ship, keyed by <c>ShipID</c>
/// and faction, and an on-foot ledger keyed by faction. A payment clears the entry it names; it never
/// subtracts its <c>Amount</c>, which includes the broker's percentage.
/// </summary>
public sealed class OutstandingCrimes(IFileSystem fileSystem, ILogger logger)
{
    /// <summary><c>PayFines</c> and <c>PayBounties</c> name a suit loadout with a <c>ShipID</c> at or above this.</summary>
    public const long SuitLoadoutIds = 4_293_000_000;

    private enum Kind
    {
        Crime,
        Fines,
        Bounties,
        Death,
        Sold,
    }

    /// <summary>A crime or a clearing, in journal order. A null <see cref="ShipId"/> on a crime is on foot.</summary>
    private sealed record Entry(DateTimeOffset At, int Order, Kind Kind, string? Faction, long? ShipId, long Fine, long Bounty, bool All);

    private sealed class Book
    {
        public Dictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>What the walk tracks as it reads: whose journal, the ship flown, and how often each key has been met.</summary>
    private sealed class Reading
    {
        public string? Commander { get; set; }

        public int? Ship { get; set; }

        public Dictionary<string, int> Seen { get; } = new(StringComparer.Ordinal);
    }

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    private readonly Reading _live = new();

    private bool _historyFolded;

    private int _order;

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

    /// <summary>The ship the live journal last put the Commander in, or null before one is seen.</summary>
    public int? FlownShip
    {
        get
        {
            lock (_gate)
            {
                return _live.Ship;
            }
        }
    }

    /// <summary>Folds a tick's events, in order, attributing them to <paramref name="commander"/> until one names another.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events, string? commander = null)
    {
        lock (_gate)
        {
            _live.Commander ??= commander;

            foreach (var journalEvent in events)
            {
                Fold(journalEvent, _live);
            }
        }
    }

    /// <summary>Folds journal files oldest first, on the calling thread; events already folded fold to no change.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var reading = new Reading();

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

            // Each file starts the occurrence count again, as the live reading does for the file it follows.
            reading.Seen.Clear();

            lock (_gate)
            {
                foreach (var journalEvent in events)
                {
                    Fold(journalEvent, reading);
                }
            }
        }

        lock (_gate)
        {
            _historyFolded = true;
        }
    }

    /// <summary>
    /// Every unpaid debt <paramref name="commander"/> holds on <paramref name="shipId"/> and on foot, or the
    /// Commander last seen where null.
    /// </summary>
    public IReadOnlyList<CrimeDebt> Owed(string? commander, int? shipId) =>
        [.. Unpaid(commander).Where(debt => debt.ShipId is null || debt.ShipId == shipId).OrderByDescending(debt => debt.Total)];

    /// <summary>How many ships other than <paramref name="shipId"/> carry an unpaid debt.</summary>
    public int OtherShipsOwing(string? commander, int? shipId) =>
        Unpaid(commander).Where(debt => debt.ShipId is not null && debt.ShipId != shipId).Select(debt => debt.ShipId).Distinct().Count();

    private List<CrimeDebt> Unpaid(string? commander)
    {
        lock (_gate)
        {
            if ((string.IsNullOrEmpty(commander) ? _live.Commander : commander) is not { } fid
                || !_books.TryGetValue(fid, out var book))
            {
                return [];
            }

            var debts = new Dictionary<(long? Ship, string Faction), CrimeDebt>();

            foreach (var entry in book.Entries.Values.OrderBy(entry => entry.At).ThenBy(entry => entry.Order))
            {
                switch (entry.Kind)
                {
                    case Kind.Crime:
                        var key = (entry.ShipId, entry.Faction!);
                        var debt = debts.GetValueOrDefault(key)
                                   ?? new CrimeDebt(entry.Faction!, (int?)entry.ShipId, 0, 0, entry.At);

                        debts[key] = debt with
                        {
                            Fines = debt.Fines + entry.Fine,
                            Bounties = debt.Bounties + entry.Bounty,
                            LastCrime = entry.At,
                        };

                        break;

                    case Kind.Fines:
                        Clear(debts, entry, fines: true, bounties: false);
                        break;

                    case Kind.Bounties:
                        Clear(debts, entry, fines: false, bounties: true);
                        break;

                    case Kind.Death or Kind.Sold:
                        foreach (var owed in debts.Keys.Where(owed => owed.Ship == entry.ShipId).ToList())
                        {
                            debts.Remove(owed);
                        }

                        break;
                }
            }

            return [.. debts.Values.Where(debt => debt.Total > 0)];
        }
    }

    private static void Clear(Dictionary<(long? Ship, string Faction), CrimeDebt> debts, Entry payment, bool fines, bool bounties)
    {
        var suit = payment.ShipId >= SuitLoadoutIds;

        foreach (var (key, debt) in debts.ToList())
        {
            var onFoot = key.Ship is null;
            var named = payment.Faction is not null && string.Equals(key.Faction, payment.Faction, StringComparison.OrdinalIgnoreCase);

            if (payment.All)
            {
                // AllFines from a suit loadout clears every on-foot fine; from a ship, every fine on that ship.
                if (suit ? onFoot : key.Ship == payment.ShipId)
                {
                    debts[key] = debt with { Fines = 0 };
                }

                continue;
            }

            if (!named)
            {
                continue;
            }

            if (onFoot)
            {
                // An on-foot debt follows the Commander, so a payment from any ship clears it. Paying a
                // faction's bounties also clears its on-foot fines.
                debts[key] = debt with
                {
                    Fines = fines || bounties ? 0 : debt.Fines,
                    Bounties = bounties ? 0 : debt.Bounties,
                };
            }
            else if (key.Ship == payment.ShipId)
            {
                debts[key] = debt with
                {
                    Fines = fines ? 0 : debt.Fines,
                    Bounties = bounties ? 0 : debt.Bounties,
                };
            }
        }
    }

    /// <summary>The text test, before any JSON is touched.</summary>
    private static bool Relevant(string line) =>
        line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Loadout\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"ShipyardSwap\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"ShipyardNew\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"ShipyardSell\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"CommitCrime\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"PayFines\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"PayBounties\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Died\"", StringComparison.Ordinal);

    private void Fold(JournalEvent journalEvent, Reading reading)
    {
        switch (journalEvent.Kind)
        {
            case "Commander" or "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } fid)
                {
                    reading.Commander = fid;
                }

                if (journalEvent.Kind == "LoadGame" && journalEvent.Int("ShipID") is { } loaded)
                {
                    reading.Ship = loaded;
                }

                return;

            case "Loadout" or "ShipyardSwap":
                reading.Ship = journalEvent.Int("ShipID") ?? reading.Ship;
                return;

            case "ShipyardNew":
                reading.Ship = journalEvent.Int("NewShipID") ?? reading.Ship;
                return;
        }

        if (reading.Commander is not { } commander || EntryOf(journalEvent, reading.Ship) is not { } entry)
        {
            return;
        }

        // The same event folded by the history walk and by the live tick has the same key, and is kept once.
        // Identical events in one second are told apart by how many came before them in the file.
        var key = journalEvent.Timestamp.UtcTicks + "|" + journalEvent.Raw.GetRawText();
        var seen = reading.Seen.GetValueOrDefault(key);
        reading.Seen[key] = seen + 1;

        if (!_books.TryGetValue(commander, out var book))
        {
            book = new Book();
            _books[commander] = book;
        }

        book.Entries.TryAdd(key + "|" + seen, entry);
    }

    private Entry? EntryOf(JournalEvent journalEvent, int? ship)
    {
        var at = journalEvent.Timestamp;

        switch (journalEvent.Kind)
        {
            case "CommitCrime" when journalEvent.String("Faction") is { Length: > 0 } faction:
                var onFoot = journalEvent.String("CrimeType")?.StartsWith("onFoot_", StringComparison.Ordinal) == true;

                if (!onFoot && ship is null)
                {
                    return null;
                }

                return new Entry(
                    at,
                    ++_order,
                    Kind.Crime,
                    faction,
                    onFoot ? null : ship,
                    journalEvent.Long("Fine") ?? 0,
                    journalEvent.Long("Bounty") ?? 0,
                    All: false);

            case "PayFines" or "PayBounties" when journalEvent.Long("ShipID") is { } paidFrom:
                var all = journalEvent.Bool("AllFines") && journalEvent.Kind == "PayFines";
                var named = journalEvent.String("Faction") is { Length: > 0 } paid ? paid : null;

                if (!all && named is null)
                {
                    return null;
                }

                return new Entry(
                    at,
                    ++_order,
                    journalEvent.Kind == "PayFines" ? Kind.Fines : Kind.Bounties,
                    all ? null : named,
                    paidFrom,
                    0,
                    0,
                    all);

            case "Died" when ship is not null:
                return new Entry(at, ++_order, Kind.Death, null, ship, 0, 0, All: false);

            case "ShipyardSell" when journalEvent.Long("SellShipID") is { } sold:
                return new Entry(at, ++_order, Kind.Sold, null, sold, 0, 0, All: false);

            default:
                return null;
        }
    }

    private IEnumerable<string> Lines(string file)
    {
        Stream stream;

        try
        {
            stream = fileSystem.OpenRead(file) ?? throw new FileNotFoundException("The journal file is missing.", file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for fines and bounties", file);
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
