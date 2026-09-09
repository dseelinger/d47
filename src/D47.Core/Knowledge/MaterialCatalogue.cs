using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>Which inventory a thing lives in, and therefore what a "unit" of it means.</summary>
public enum MaterialLedger
{
    /// <summary>Ship materials — Raw, Manufactured, Encoded.</summary>
    Material,

    /// <summary>Odyssey ship-locker goods, assets, data and consumables.</summary>
    ShipLocker,

    /// <summary>Ordinary market commodities, measured in tonnes.</summary>
    Cargo,

    /// <summary>Rare commodities, bought at one station and allocation-limited.</summary>
    RareCargo,

    /// <summary>A ledger the table does not recognise.</summary>
    Unknown,
}

/// <summary>One thing a Commander can be holding, whichever inventory it sits in.</summary>
public sealed record MaterialEntry
{
    /// <summary>The symbol the journal writes, lower case — <c>carbon</c>, <c>tg_weaponparts</c>.</summary>
    public required string Symbol { get; init; }

    /// <summary>The name a Commander hears and reads — "Proto Radiolic Alloys".</summary>
    public required string Name { get; init; }

    public MaterialLedger Ledger { get; init; } = MaterialLedger.Unknown;

    /// <summary>
    /// Raw, Manufactured or Encoded for a ship material; the market or locker grouping for everything
    /// else.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>1 to 5 for a ship material, null for anything that has no grade.</summary>
    public int? Grade { get; init; }

    /// <summary>One column of the material trader's grid, e.g.</summary>
    public string? Line { get; init; }

    /// <summary>Where it is found, as EDEngineer's sourcing table puts it.</summary>
    public IReadOnlyList<string> Origins { get; init; } = [];

    /// <summary>Which kinds of settlement hold it — High Tech, Industrial, Research, Tourist, or ALL.</summary>
    public IReadOnlyList<string> Settlements { get; init; } = [];

    /// <summary>
    /// Which buildings within a settlement hold it, as Frontier's own 16 codes — AGRI, LAB, PROC and
    /// the rest.
    /// </summary>
    public IReadOnlyList<string> Buildings { get; init; } = [];

    /// <summary>Which lockers and data ports inside those buildings hold it.</summary>
    public IReadOnlyList<string> Containers { get; init; } = [];

    /// <summary>What the Bartender gives for handing one over.</summary>
    public int? BarterValue { get; init; }

    /// <summary>What the Bartender charges in barter value for one of these.</summary>
    public int? BarterCost { get; init; }

    /// <summary>Whether a material trader will deal in it.</summary>
    public bool IsTradeable => Ledger == MaterialLedger.Material && Line is not null;

    /// <summary>Whether the Bartender will exchange it.</summary>
    public bool IsBarterable => BarterValue is not null && BarterCost is not null;
}

/// <summary>
/// Every material, micro-resource and commodity, with the grade, the trader line and the ledger the
/// journal does not carry (Phase 14, <c>#102</c>).
/// </summary>
public static class MaterialCatalogue
{
    private const string ResourceName = "D47.Core.Materials";

    private static readonly Lazy<Tables> Loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed record Tables(
        IReadOnlyDictionary<string, MaterialEntry> BySymbol,
        IReadOnlyList<string> KnownButUnkeyed);

    public static IReadOnlyCollection<MaterialEntry> All => [.. Loaded.Value.BySymbol.Values];

    /// <summary>Things a source describes and no id list has a symbol for.</summary>
    public static IReadOnlyList<string> KnownButUnkeyed => Loaded.Value.KnownButUnkeyed;

    /// <summary>A material by the journal's symbol, or by the name a Commander says.</summary>
    public static MaterialEntry? Find(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var entries = Loaded.Value.BySymbol;
        var wanted = spoken.Trim();

        if (entries.TryGetValue(wanted.ToLowerInvariant(), out var bySymbol))
        {
            return bySymbol;
        }

        var names = entries.Values.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).ToArray();

        if (Catalogue.Match(names, wanted) is not { } name)
        {
            return null;
        }

        // "Wreckage Components" is two things — a Thargoid material and a salvage commodity.
        return entries.Values
            .Where(entry => entry.Name == name)
            .OrderBy(entry => entry.Ledger)
            .First();
    }

    /// <summary>Names close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> Near(string spoken) =>
        Catalogue.Near(
            [.. Loaded.Value.BySymbol.Values.Select(entry => entry.Name).Distinct(StringComparer.Ordinal)],
            spoken);

    /// <summary>Everything sharing a trader line, lowest grade first.</summary>
    public static IReadOnlyList<MaterialEntry> InLine(string? line) =>
        string.IsNullOrWhiteSpace(line)
            ? []
            : [.. Loaded.Value.BySymbol.Values
                .Where(entry => string.Equals(entry.Line, line.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Grade)];

    /// <summary>Every trader line the table knows, in a stable order.</summary>
    public static IReadOnlyList<string> Lines =>
        [.. Loaded.Value.BySymbol.Values
            .Select(entry => entry.Line)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static Tables Load()
    {
        using var stream = typeof(MaterialCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            // Nothing can be answered without it, and answering anyway is the failure the whole table exists
            // to avoid.
            return new Tables(new Dictionary<string, MaterialEntry>(StringComparer.Ordinal), []);
        }

        using var reader = new StreamReader(stream);

        var entries = new Dictionary<string, MaterialEntry>(StringComparer.Ordinal);
        var unkeyed = new List<string>();
        var unkeyedSection = false;

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("symbol\t", StringComparison.Ordinal))
            {
                continue;
            }

            if (line[0] == '[')
            {
                unkeyedSection = line == "[known-but-unkeyed]";
                continue;
            }

            if (unkeyedSection)
            {
                unkeyed.Add(line);
                continue;
            }

            var entry = Read(line.Split('\t'));
            entries[entry.Symbol] = entry;
        }

        return new Tables(entries, unkeyed);
    }

    private static MaterialEntry Read(string[] cells) => new()
    {
        Symbol = Text(cells, 0) ?? "unknown",
        Name = Text(cells, 1) ?? "an unnamed material",
        Ledger = Ledgers.GetValueOrDefault(Text(cells, 2) ?? string.Empty, MaterialLedger.Unknown),
        Category = Text(cells, 3),
        Grade = Integer(cells, 4),
        Line = Text(cells, 5),
        Origins = Origins(cells, 6),
        Settlements = Origins(cells, 7),
        Buildings = Origins(cells, 8),
        Containers = Origins(cells, 9),
        BarterValue = Barter(cells, 0),
        BarterCost = Barter(cells, 1),
    };

    /// <summary>One half of the <c>value/cost</c> cell.</summary>
    private static int? Barter(string[] cells, int half) =>
        Text(cells, 10)?.Split('/') is { Length: 2 } parts
        && int.TryParse(parts[half], CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>The four inventories, spelled as the generator writes them.</summary>
    private static readonly Dictionary<string, MaterialLedger> Ledgers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["material"] = MaterialLedger.Material,
        ["ship-locker"] = MaterialLedger.ShipLocker,
        ["cargo"] = MaterialLedger.Cargo,
        ["rare-cargo"] = MaterialLedger.RareCargo,
    };

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;

    private static int? Integer(string[] cells, int index) =>
        Text(cells, index) is { } text && int.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static IReadOnlyList<string> Origins(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(';').Select(part => part.Trim()).Where(part => part.Length > 0)];
}
