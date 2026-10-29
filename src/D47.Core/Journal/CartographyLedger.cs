using System.Globalization;
using System.Text.Json;
using D47.Core.Storage;
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
public sealed class CartographyLedger(string? path, IFileSystem fileSystem, ILogger logger)
    : UnsoldLedger(path, fileSystem, logger, "CartographyResetAt", "cartography", "exploration data")
{

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

    private readonly Dictionary<string, Book> _books = new(StringComparer.Ordinal);

    private readonly Dictionary<(long System, int Body), Body> _bodies = [];

    private readonly Dictionary<long, string> _systemNames = [];

    /// <summary>What <paramref name="commander"/> is carrying unsold, or the Commander last seen where null.</summary>
    public UnsoldCartography Unsold(string? commander = null)
    {
        lock (Gate)
        {
            if ((string.IsNullOrEmpty(commander) ? Current : commander) is not { } fid
                || !_books.TryGetValue(fid, out var book))
            {
                return UnsoldCartography.Empty;
            }

            var reset = ResetFloor(fid);

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

        lock (Gate)
        {
            return Priced(mapping);
        }
    }

    /// <summary>What a planet would pay if the Commander mapped it efficiently now; null without a scan giving its class and mass.</summary>
    public long? IfMapped(long system, int bodyId)
    {
        lock (Gate)
        {
            if (!_bodies.TryGetValue((system, bodyId), out var body)
                || body.PlanetClass is not { } planetClass
                || body.MassEm is not { } mass)
            {
                return null;
            }

            return CartographicValue.Planet(
                planetClass,
                body.TerraformState,
                mass,
                wasDiscovered: body.Flags.Count == 0 || body.Flags.Values.Any(flags => flags.Discovered),
                wasMapped: body.Flags.Count == 0 || body.Flags.Values.Any(flags => flags.Mapped),
                mapped: true,
                efficient: true);
        }
    }

    /// <summary>The text test, before any JSON is touched.</summary>
    protected override bool Relevant(string line) =>
        line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"NewCommander\"", StringComparison.Ordinal)
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

    protected override void Fold(JournalEvent journalEvent, ref string? commander)
    {
        if (FoldCommander(journalEvent, ref commander))
        {
            return;
        }

        switch (journalEvent.Kind)
        {
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

    private Book Open(string commander) => LedgerBooks.Open(_books, commander);
}
