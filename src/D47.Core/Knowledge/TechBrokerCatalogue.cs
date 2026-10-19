namespace D47.Core.Knowledge;

/// <summary>A tech broker unlock and the module symbols that carry it.</summary>
public sealed record TechBrokerRow(Blueprint Blueprint, string BrokerType, IReadOnlyList<string> Symbols)
{
    public string Name => Blueprint.Name;
}

/// <summary>Joins each tech broker row of <see cref="BlueprintCatalogue"/> to the module symbols it unlocks.</summary>
public static class TechBrokerCatalogue
{
    public const string Guardian = "guardian";

    public const string Human = "human";

    private static readonly Lazy<IReadOnlyList<TechBrokerRow>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, TechBrokerRow>> BySymbol = new(
        () => Loaded.Value
            .SelectMany(row => row.Symbols.Select(symbol => (symbol, row)))
            .ToDictionary(pair => pair.symbol, pair => pair.row, StringComparer.Ordinal),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Dictionary<string, int> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Small"] = 1,
        ["Medium"] = 2,
        ["Large"] = 3,
    };

    private static readonly string[] Mounts = ["Fixed", "Gimballed", "Turreted"];

    /// <summary>Every tech broker row, joined or not.</summary>
    public static IReadOnlyList<TechBrokerRow> Rows => Loaded.Value;

    /// <summary>The rows that join to at least one module symbol.</summary>
    public static IReadOnlyList<TechBrokerRow> Joined => [.. Loaded.Value.Where(row => row.Symbols.Count > 0)];

    /// <summary>The rows that join to nothing.</summary>
    public static IReadOnlyList<TechBrokerRow> Unjoined => [.. Loaded.Value.Where(row => row.Symbols.Count == 0)];

    /// <summary>The row a module symbol unlocks, or null.</summary>
    public static TechBrokerRow? ForSymbol(string? symbol) =>
        symbol is null ? null : BySymbol.Value.GetValueOrDefault(Normalise(symbol));

    /// <summary>A journal module name lower-cased, without the <c>$…_name;</c> wrapper a stored module has.</summary>
    public static string Normalise(string symbol)
    {
        var text = symbol.Trim().ToLowerInvariant().TrimStart('$').TrimEnd(';');

        return text.EndsWith("_name", StringComparison.Ordinal) ? text[..^5] : text;
    }

    private static IReadOnlyList<TechBrokerRow> Load() =>
    [
        .. BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind == BlueprintKind.TechBroker)
            .Select(blueprint => new TechBrokerRow(
                blueprint,
                string.Equals(blueprint.Module, "Guardian", StringComparison.Ordinal) ? Guardian : Human,
                SymbolsFor(blueprint.Name))),
    ];

    /// <summary>
    /// The symbols of the modules named <c>Base</c> or <c>Base (Mount, Size)</c> / <c>Base (Mount)</c> /
    /// <c>Base (4E)</c>; none when the base name or any qualifier matches nothing.
    /// </summary>
    private static List<string> SymbolsFor(string name)
    {
        var open = name.IndexOf(" (", StringComparison.Ordinal);
        var baseName = open < 0 ? name : name[..open];
        string[] qualifiers = open < 0 || !name.EndsWith(')')
            ? []
            : [.. name[(open + 2)..^1].Split(',', StringSplitOptions.TrimEntries)];

        string? mount = null;
        int? size = null;
        string? rating = null;

        foreach (var qualifier in qualifiers)
        {
            if (Mounts.Contains(qualifier, StringComparer.OrdinalIgnoreCase))
            {
                mount = qualifier;
            }
            else if (Sizes.TryGetValue(qualifier, out var named))
            {
                size = named;
            }
            else if (qualifier.Length == 2 && char.IsDigit(qualifier[0]) && char.IsLetter(qualifier[1]))
            {
                size = qualifier[0] - '0';
                rating = qualifier[1..];
            }
            else
            {
                return [];
            }
        }

        return
        [
            .. EliteSpecifications.Modules
                .Where(module => string.Equals(module.Name, baseName, StringComparison.OrdinalIgnoreCase))
                .Where(module => mount is null || string.Equals(module.Mount, mount, StringComparison.OrdinalIgnoreCase))
                .Where(module => size is null || module.Class == size)
                .Where(module => rating is null || string.Equals(module.Rating, rating, StringComparison.OrdinalIgnoreCase))
                .Select(module => module.Symbol.ToLowerInvariant())
                .Order(StringComparer.Ordinal),
        ];
    }
}
