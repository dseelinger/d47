using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One body the Commander mapped with the DSS and has not yet sold.</summary>
/// <param name="Value">The <see cref="CartographicValue"/> estimate, or null where no scan gave the body's class and mass.</param>
public sealed record HeldMap(DateTimeOffset At, string BodyName, string? System, bool Efficient, long? Value);

/// <summary>The mapped bodies a Commander is carrying and has not sold.</summary>
public sealed record UnsoldCartography(IReadOnlyList<HeldMap> Held)
{
    public static readonly UnsoldCartography Empty = new([]);

    /// <summary>The sum of every held map that has a value.</summary>
    public long Total => Held.Sum(map => map.Value ?? 0);

    public int Efficient => Held.Count(map => map.Efficient);

    /// <summary>Held maps left out of <see cref="Total"/> for want of a scan.</summary>
    public IReadOnlyList<HeldMap> Unpriced => [.. Held.Where(map => map.Value is null)];
}

/// <summary>
/// Bodies mapped with the DSS and not yet sold, per Commander, across sessions (#527). A map is held until a
/// sale of exploration data names its system, the Commander dies, or the Commander resets the total.
/// </summary>
public sealed class CartographyLedger(string? path, ILogger logger)
{
    private const string ResetProperty = "CartographyResetAt";

    private sealed record Mapping(DateTimeOffset At, long System, int Body, string BodyName, bool Efficient);

    private sealed record Sale(DateTimeOffset At, IReadOnlySet<string> Systems);

    /// <summary>A planet's class and mass from its latest scan, and every discovery flag each scan reported.</summary>
    private sealed class Body
    {
        public string? PlanetClass { get; set; }

        public string? TerraformState { get; set; }

        public double? MassEm { get; set; }

        public Dictionary<DateTimeOffset, (bool Discovered, bool Mapped)> Flags { get; } = [];
    }

    private sealed class Book
    {
        public Dictionary<string, Mapping> Mappings { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Sale> Sales { get; } = new(StringComparer.Ordinal);

        public HashSet<DateTimeOffset> Deaths { get; } = [];
    }

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    private readonly Dictionary<(long System, int Body), Body> _bodies = [];

    private readonly Dictionary<long, string> _systemNames = [];

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
            logger.LogWarning(ex, "Could not read {Path}; the unsold cartography total counts from the last sale or death", path);
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
    public UnsoldCartography Unsold(string? commander = null)
    {
        lock (_gate)
        {
            if ((string.IsNullOrEmpty(commander) ? _current : commander) is not { } fid
                || !_books.TryGetValue(fid, out var book))
            {
                return UnsoldCartography.Empty;
            }

            var reset = _resets.TryGetValue(fid, out var resetAt) ? resetAt : DateTimeOffset.MinValue;

            // At one timestamp a mapping folds before the sale or death that ends it.
            var timeline = book.Mappings.Values.Select(mapping => (mapping.At, Order: 0, Item: (object)mapping))
                .Concat(book.Sales.Values.Select(sale => (sale.At, Order: 1, Item: (object)sale)))
                .Concat(book.Deaths.Select(at => (At: at, Order: 1, Item: (object)at)))
                .Where(entry => entry.At > reset)
                .OrderBy(entry => entry.At)
                .ThenBy(entry => entry.Order);

            // A body mapped twice before a sale is sold once.
            var held = new Dictionary<(long, int), Mapping>();

            foreach (var (_, _, item) in timeline)
            {
                switch (item)
                {
                    case Mapping mapping:
                        held[(mapping.System, mapping.Body)] = mapping;
                        break;

                    case Sale sale:
                        foreach (var key in held.Where(entry => sale.Systems.Contains(SystemName(entry.Value.System) ?? string.Empty))
                                     .Select(entry => entry.Key)
                                     .ToList())
                        {
                            held.Remove(key);
                        }

                        break;

                    default:
                        held.Clear();
                        break;
                }
            }

            return new UnsoldCartography([.. held.Values.OrderBy(mapping => mapping.At).Select(Priced)]);
        }
    }

    /// <summary>The value of the body an <c>SAAScanComplete</c> reports, from the scans folded so far; null for any other event.</summary>
    public HeldMap? Price(JournalEvent journalEvent)
    {
        if (MappingOf(journalEvent) is not { } mapping)
        {
            return null;
        }

        lock (_gate)
        {
            return Priced(mapping);
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
        || line.Contains("\"event\":\"SAAScanComplete\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MultiSellExplorationData\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SellExplorationData\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"FSDJump\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Location\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"CarrierJump\"", StringComparison.Ordinal)
        || (line.Contains("\"event\":\"Scan\"", StringComparison.Ordinal)
            && line.Contains("\"PlanetClass\"", StringComparison.Ordinal));

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

            case "FSDJump" or "Location" or "CarrierJump":
                Name(journalEvent);
                return;

            case "Scan":
                Name(journalEvent);
                Scanned(journalEvent);
                return;
        }

        if (commander is null)
        {
            return;
        }

        switch (journalEvent.Kind)
        {
            case "SAAScanComplete":
                if (MappingOf(journalEvent) is { } mapping)
                {
                    Open(commander).Mappings.TryAdd(
                        Key(mapping.At, mapping.System.ToString(CultureInfo.InvariantCulture),
                            mapping.Body.ToString(CultureInfo.InvariantCulture)),
                        mapping);
                }

                return;

            case "MultiSellExplorationData" or "SellExplorationData":
                var systems = journalEvent.Items("Discovered")
                    .Select(entry => entry.String("SystemName"))
                    .Concat(journalEvent.Items("Systems").Select(entry =>
                        entry.ValueKind == JsonValueKind.String ? entry.GetString() : null))
                    .OfType<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                Open(commander).Sales.TryAdd(
                    Key(journalEvent.Timestamp, string.Join('|', systems.Order(StringComparer.OrdinalIgnoreCase))),
                    new Sale(journalEvent.Timestamp, systems));

                return;

            case "Died":
                Open(commander).Deaths.Add(journalEvent.Timestamp);
                return;
        }
    }

    private void Name(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SystemAddress") is { } system && journalEvent.String("StarSystem") is { Length: > 0 } name)
        {
            _systemNames[system] = name;
        }
    }

    private void Scanned(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SystemAddress") is not { } system
            || journalEvent.Int("BodyID") is not { } bodyId
            || journalEvent.String("PlanetClass") is not { Length: > 0 } planetClass)
        {
            return;
        }

        if (!_bodies.TryGetValue((system, bodyId), out var body))
        {
            body = new Body();
            _bodies[(system, bodyId)] = body;
        }

        body.PlanetClass = planetClass;
        body.TerraformState = journalEvent.String("TerraformState");
        body.MassEm = journalEvent.Double("MassEM") ?? body.MassEm;

        // A missing flag reads as true, which claims no bonus. Two scans in one second keep a true.
        var discovered = Flag(journalEvent, "WasDiscovered");
        var mapped = Flag(journalEvent, "WasMapped");

        if (body.Flags.TryGetValue(journalEvent.Timestamp, out var seen))
        {
            discovered |= seen.Discovered;
            mapped |= seen.Mapped;
        }

        body.Flags[journalEvent.Timestamp] = (discovered, mapped);
    }

    private static bool Flag(JournalEvent journalEvent, string property) =>
        !journalEvent.Raw.TryGetProperty(property, out var flag) || flag.ValueKind != JsonValueKind.False;

    private static Mapping? MappingOf(JournalEvent journalEvent) =>
        journalEvent.Kind == "SAAScanComplete"
        && journalEvent.Long("SystemAddress") is { } system
        && journalEvent.Int("BodyID") is { } body
            ? new Mapping(
                journalEvent.Timestamp,
                system,
                body,
                journalEvent.String("BodyName") ?? string.Empty,
                journalEvent.Int("ProbesUsed") is { } probes
                && journalEvent.Int("EfficiencyTarget") is { } target
                && probes <= target)
            : null;

    /// <summary>
    /// From the scans at or before the mapping: any that said the body was discovered or mapped rules that bonus
    /// out, since neither is undone and the Detailed scan written after a DSS mapping can say false where earlier
    /// scans said true.
    /// </summary>
    private HeldMap Priced(Mapping mapping)
    {
        long? value = null;

        if (_bodies.TryGetValue((mapping.System, mapping.Body), out var body)
            && body.PlanetClass is { } planetClass
            && body.MassEm is { } mass)
        {
            var before = body.Flags.Where(scan => scan.Key <= mapping.At).Select(scan => scan.Value).ToList();

            if (before.Count == 0)
            {
                before = [.. body.Flags.Values];
            }

            value = CartographicValue.Planet(
                planetClass,
                body.TerraformState,
                mass,
                wasDiscovered: before.Count == 0 || before.Any(flags => flags.Discovered),
                wasMapped: before.Count == 0 || before.Any(flags => flags.Mapped),
                mapped: true,
                mapping.Efficient);
        }

        return new HeldMap(mapping.At, mapping.BodyName, SystemName(mapping.System), mapping.Efficient, value);
    }

    private string? SystemName(long system) => _systemNames.GetValueOrDefault(system);

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
            logger.LogWarning(ex, "Could not read {File} for exploration data", file);
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
