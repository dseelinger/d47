using System.Globalization;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One analysed organism the Commander has not yet sold.</summary>
/// <param name="BaseValue">The species' value from <see cref="ExobiologyCatalogue"/>, or null where it has none.</param>
/// <param name="FirstFootfall">
/// Whether the body's scan said nobody had walked there; null where no scan before the analysis said either.
/// </param>
public sealed record HeldAnalysis(DateTimeOffset At, string Species, long? BaseValue, bool? FirstFootfall)
{
    /// <summary>Five times the base value with the first footfall bonus, the base value otherwise.</summary>
    public long? Worth => BaseValue * (FirstFootfall == true ? 5 : 1);
}

/// <summary>The organic data a Commander is carrying and has not sold.</summary>
public sealed record UnsoldExobiology(IReadOnlyList<HeldAnalysis> Held)
{
    public static readonly UnsoldExobiology Empty = new([]);

    /// <summary>The sum of every held analysis that has a value.</summary>
    public long Total => Held.Sum(analysis => analysis.Worth ?? 0);

    public int WithBonus => Held.Count(analysis => analysis.BaseValue is not null && analysis.FirstFootfall == true);

    /// <summary>Priced at the base value because no scan said whether the bonus applies.</summary>
    public int BonusUnknown => Held.Count(analysis => analysis.BaseValue is not null && analysis.FirstFootfall is null);

    /// <summary>Held analyses left out of <see cref="Total"/> for want of a value.</summary>
    public IReadOnlyList<HeldAnalysis> Unpriced => [.. Held.Where(analysis => analysis.BaseValue is null)];
}

/// <summary>
/// Organic data analysed and not yet sold, per Commander, across sessions (#526). An analysis is held until a
/// <c>SellOrganicData</c> names its species, the Commander dies, or the Commander resets the total.
/// </summary>
public sealed class ExobiologyLedger(string? path, ILogger logger)
{
    private const string ResetProperty = "ExobiologyResetAt";

    private static readonly Lazy<IReadOnlyDictionary<string, long>> Values = new(() =>
        ExobiologyCatalogue.All
            .GroupBy(entry => entry.Species, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase));

    private sealed record Analysis(DateTimeOffset At, long System, int Body, string? Symbol, string Species);

    private sealed record Sale(DateTimeOffset At, IReadOnlyList<(string? Symbol, string? Species)> Sold);

    private sealed class Book
    {
        public Dictionary<string, Analysis> Analyses { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Sale> Sales { get; } = new(StringComparer.Ordinal);

        public HashSet<DateTimeOffset> Deaths { get; } = [];
    }

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    /// <summary>Every <c>WasFootfalled</c> a scan has reported, per body, with when it was reported.</summary>
    private readonly Dictionary<(long System, int Body), Dictionary<DateTimeOffset, bool>> _footfalls = [];

    private readonly Dictionary<string, DateTimeOffset> _resets = new(StringComparer.Ordinal);

    private string? _current;

    private bool _historyFolded;

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

    /// <summary>Reads the reset times from <c>path</c>.</summary>
    public void Load()
    {
        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            var resets = UnsoldDataFile.Read(path, ResetProperty);

            lock (_gate)
            {
                foreach (var (fid, at) in resets)
                {
                    _resets[fid] = at;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; the unsold exobiology total counts from the last sale or death", path);
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
    }

    /// <summary>Folds journal files oldest first, on the calling thread; files already folded fold to no change.</summary>
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
    }

    /// <summary>What <paramref name="commander"/> is carrying unsold, or the Commander last seen where null.</summary>
    public UnsoldExobiology Unsold(string? commander = null)
    {
        lock (_gate)
        {
            if ((string.IsNullOrEmpty(commander) ? _current : commander) is not { } fid
                || !_books.TryGetValue(fid, out var book))
            {
                return UnsoldExobiology.Empty;
            }

            var reset = _resets.TryGetValue(fid, out var resetAt) ? resetAt : DateTimeOffset.MinValue;

            // At one timestamp an analysis folds before the sale or death that ends it.
            var timeline = book.Analyses.Values.Select(analysis => (analysis.At, Order: 0, Item: (object)analysis))
                .Concat(book.Sales.Values.Select(sale => (sale.At, Order: 1, Item: (object)sale)))
                .Concat(book.Deaths.Select(at => (At: at, Order: 1, Item: (object)at)))
                .Where(entry => entry.At > reset)
                .OrderBy(entry => entry.At)
                .ThenBy(entry => entry.Order);

            var held = new List<Analysis>();

            foreach (var (_, _, item) in timeline)
            {
                switch (item)
                {
                    case Analysis analysis:
                        held.Add(analysis);
                        break;

                    case Sale sale:
                        held.RemoveAll(analysis => sale.Sold.Any(sold => Same(analysis, sold)));
                        break;

                    default:
                        held.Clear();
                        break;
                }
            }

            return new UnsoldExobiology([.. held.Select(Priced)]);
        }
    }

    /// <summary>The value of the analysis a <c>ScanOrganic</c> Analyse reports, or null for any other event.</summary>
    public HeldAnalysis? Price(JournalEvent journalEvent)
    {
        if (AnalysisOf(journalEvent) is not { } analysis)
        {
            return null;
        }

        lock (_gate)
        {
            return Priced(analysis);
        }
    }

    /// <summary>Clears <paramref name="commander"/>'s total as of <paramref name="at"/>, and writes the time to <c>path</c>.</summary>
    public void Reset(string commander, DateTimeOffset at)
    {
        Dictionary<string, DateTimeOffset> resets;

        lock (_gate)
        {
            _resets[commander] = at;
            resets = new Dictionary<string, DateTimeOffset>(_resets, StringComparer.Ordinal);
        }

        if (path is null)
        {
            return;
        }

        try
        {
            UnsoldDataFile.Write(path, ResetProperty, resets);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write {Path}; the reset holds until d47 restarts", path);
        }
    }

    /// <summary>The text test, before any JSON is touched.</summary>
    private static bool Relevant(string line) =>
        line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Died\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SellOrganicData\"", StringComparison.Ordinal)
        || line.Contains("\"ScanType\":\"Analyse\"", StringComparison.Ordinal)
        || (line.Contains("\"event\":\"Scan\"", StringComparison.Ordinal)
            && line.Contains("\"WasFootfalled\"", StringComparison.Ordinal));

    private void Fold(JournalEvent journalEvent, ref string? commander)
    {
        switch (journalEvent.Kind)
        {
            case "Commander" or "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } fid)
                {
                    commander = fid;
                }

                return;

            case "Scan":
                if (journalEvent.Long("SystemAddress") is { } system
                    && journalEvent.Int("BodyID") is { } body
                    && journalEvent.Raw.TryGetProperty("WasFootfalled", out var flag)
                    && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    if (!_footfalls.TryGetValue((system, body), out var seen))
                    {
                        seen = [];
                        _footfalls[(system, body)] = seen;
                    }

                    // Two scans in one second keep the true, as FirstFootfall does across seconds.
                    seen[journalEvent.Timestamp] = flag.ValueKind == JsonValueKind.True
                        || seen.GetValueOrDefault(journalEvent.Timestamp);
                }

                return;
        }

        if (commander is null)
        {
            return;
        }

        switch (journalEvent.Kind)
        {
            case "ScanOrganic":
                if (AnalysisOf(journalEvent) is { } analysis)
                {
                    Open(commander).Analyses.TryAdd(
                        Key(analysis.At, analysis.System.ToString(CultureInfo.InvariantCulture),
                            analysis.Body.ToString(CultureInfo.InvariantCulture), analysis.Symbol ?? analysis.Species),
                        analysis);
                }

                return;

            case "SellOrganicData":
                var sold = journalEvent.Raw.Items("BioData")
                    .Select(entry => (entry.String("Species"), entry.String("Species_Localised")))
                    .ToList();

                Open(commander).Sales.TryAdd(
                    Key(journalEvent.Timestamp, journalEvent.Long("MarketID")?.ToString(CultureInfo.InvariantCulture)),
                    new Sale(journalEvent.Timestamp, sold));

                return;

            case "Died":
                Open(commander).Deaths.Add(journalEvent.Timestamp);
                return;
        }
    }

    private static Analysis? AnalysisOf(JournalEvent journalEvent) =>
        journalEvent.Kind == "ScanOrganic"
        && journalEvent.String("ScanType") == "Analyse"
        && journalEvent.Named("Species") is { Length: > 0 } species
        && journalEvent.Long("SystemAddress") is { } system
        && journalEvent.Int("Body") is { } body
            ? new Analysis(journalEvent.Timestamp, system, body, journalEvent.String("Species"), species)
            : null;

    private HeldAnalysis Priced(Analysis analysis) =>
        new(
            analysis.At,
            analysis.Species,
            Values.Value.TryGetValue(analysis.Species, out var value) ? value : null,
            FirstFootfall(analysis));

    /// <summary>
    /// From the scans at or before the analysis: any that said the body had been walked on rules the bonus
    /// out, since a footfall is not undone and an AutoScan and a Detailed scan of one body can disagree.
    /// </summary>
    private bool? FirstFootfall(Analysis analysis)
    {
        if (!_footfalls.TryGetValue((analysis.System, analysis.Body), out var seen))
        {
            return null;
        }

        var before = seen.Where(scan => scan.Key <= analysis.At).Select(scan => scan.Value).ToList();

        return before.Count == 0 ? null : !before.Contains(true);
    }

    /// <summary>Matched on the symbol, which does not change with the game's language, where both carry one.</summary>
    private static bool Same(Analysis analysis, (string? Symbol, string? Species) sold) =>
        analysis.Symbol is { Length: > 0 } symbol && sold.Symbol is { Length: > 0 }
            ? string.Equals(symbol, sold.Symbol, StringComparison.OrdinalIgnoreCase)
            : string.Equals(analysis.Species, sold.Species, StringComparison.OrdinalIgnoreCase);

    private static string Key(DateTimeOffset at, params string?[] parts) =>
        at.ToString("O", CultureInfo.InvariantCulture) + "|" + string.Join('|', parts);

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
            logger.LogWarning(ex, "Could not read {File} for organic data", file);
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
