using System.Globalization;
using System.Text.Json;
using D47.Core.Knowledge;
using D47.Core.Storage;
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
public sealed class ExobiologyLedger(string? path, IFileSystem fileSystem, ILogger logger)
    : UnsoldLedger(path, fileSystem, logger, "ExobiologyResetAt", "exobiology", "organic data")
{

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

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    /// <summary>Every <c>WasFootfalled</c> a scan has reported, per body, with when it was reported.</summary>
    private readonly Dictionary<(long System, int Body), Dictionary<DateTimeOffset, bool>> _footfalls = [];

    private readonly HashSet<long> _populated = [];

    /// <summary>What <paramref name="commander"/> is carrying unsold, or the Commander last seen where null.</summary>
    public UnsoldExobiology Unsold(string? commander = null)
    {
        lock (Gate)
        {
            if ((string.IsNullOrEmpty(commander) ? Current : commander) is not { } fid
                || !_books.TryGetValue(fid, out var book))
            {
                return UnsoldExobiology.Empty;
            }

            var reset = ResetFloor(fid);

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

        lock (Gate)
        {
            return Priced(analysis);
        }
    }

    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "Commander", "NewCommander", "LoadGame", "Died", "FSDJump", "Location", "CarrierJump", "SellOrganicData");

    /// <summary>The text test, before any JSON is touched.</summary>
    protected override bool Relevant(string line) =>
        Reads.Matches(line)
        || line.Contains("\"ScanType\":\"Analyse\"", StringComparison.Ordinal)
        || (line.Contains("\"event\":\"Scan\"", StringComparison.Ordinal)
            && line.Contains("\"WasFootfalled\"", StringComparison.Ordinal));

    protected override void Fold(JournalEvent journalEvent, ref string? commander)
    {
        if (FoldCommander(journalEvent, ref commander))
        {
            return;
        }

        switch (journalEvent.Kind)
        {
            case "FSDJump" or "Location" or "CarrierJump":
                if (BodyScans.PopulationOf(journalEvent) is { } entry)
                {
                    _ = entry.Populated ? _populated.Add(entry.SystemAddress) : _populated.Remove(entry.SystemAddress);
                }

                return;

            case "Scan":
                if (journalEvent.Long("SystemAddress") is { } system
                    && journalEvent.Int("BodyID") is { } body
                    && BodyScans.FootfallFlag(journalEvent, _populated.Contains(system)) is { } taken)
                {
                    if (!_footfalls.TryGetValue((system, body), out var seen))
                    {
                        seen = [];
                        _footfalls[(system, body)] = seen;
                    }

                    // Two scans in one second keep the true, as FirstFootfall does across seconds.
                    seen[journalEvent.Timestamp] = taken
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

    private Book Open(string commander) => LedgerBooks.Open(_books, commander);
}
