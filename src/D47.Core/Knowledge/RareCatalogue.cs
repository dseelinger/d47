using System.Globalization;
using System.Reflection;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One rare good and the one station that sells it.</summary>
public sealed record RareEntry
{
    /// <summary>The symbol <c>Materials.tsv</c> keys it under, lower case.</summary>
    public required string Symbol { get; init; }

    public required string Name { get; init; }

    public required string System { get; init; }

    public required string Station { get; init; }

    /// <summary>Frontier's own identifier for the station.</summary>
    public required long MarketId { get; init; }

    /// <summary>The system's galactic position, as the journal's <c>StarPos</c> writes it.</summary>
    public required StarPosition Position { get; init; }

    /// <summary>The sell price most stations far from the source report; null when fewer than three agree.</summary>
    public int? FarSellPrice { get; init; }

    /// <summary>The station's distance from its star, in light seconds.</summary>
    public double? DistanceToArrival { get; init; }
}

/// <summary>Where each rare good is sold (#115).</summary>
public static class RareCatalogue
{
    private const string ResourceName = "D47.Core.Rares";

    private static readonly Lazy<IReadOnlyDictionary<string, RareEntry>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyCollection<RareEntry> All => [.. Loaded.Value.Values];

    /// <summary>A rare good by its journal symbol, or by the name a Commander says.</summary>
    public static RareEntry? Find(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var entries = Loaded.Value;
        var wanted = spoken.Trim();

        if (entries.TryGetValue(wanted.ToLowerInvariant(), out var bySymbol))
        {
            return bySymbol;
        }

        var names = entries.Values.Select(entry => entry.Name).ToArray();

        return Catalogue.Match(names, wanted) is { } name
            ? entries.Values.First(entry => entry.Name == name)
            : null;
    }

    private static IReadOnlyDictionary<string, RareEntry> Load()
    {
        using var stream = typeof(RareCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            // Nothing can be answered without it, and answering anyway is the failure the whole
            // table exists to avoid.
            return new Dictionary<string, RareEntry>(StringComparer.Ordinal);
        }

        using var reader = new StreamReader(stream);

        var entries = new Dictionary<string, RareEntry>(StringComparer.Ordinal);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("symbol\t", StringComparison.Ordinal))
            {
                continue;
            }

            if (Read(line.Split('\t')) is { } entry)
            {
                entries[entry.Symbol] = entry;
            }
        }

        return entries;
    }

    /// <summary>One table row, split on tabs; null when a required cell is missing.</summary>
    internal static RareEntry? Read(string[] cells)
    {
        if (Text(cells, 0) is not { } symbol
            || Text(cells, 1) is not { } name
            || Text(cells, 2) is not { } system
            || Text(cells, 3) is not { } station
            || Text(cells, 4) is not { } marketId
            || !long.TryParse(marketId, CultureInfo.InvariantCulture, out var id)
            || Number(cells, 5) is not { } x
            || Number(cells, 6) is not { } y
            || Number(cells, 7) is not { } z)
        {
            return null;
        }

        return new RareEntry
        {
            Symbol = symbol,
            Name = name,
            System = system,
            Station = station,
            MarketId = id,
            Position = new StarPosition(x, y, z),
            FarSellPrice = Text(cells, 8) is { } price
                && int.TryParse(price, NumberStyles.None, CultureInfo.InvariantCulture, out var credits)
                    ? credits
                    : null,
            DistanceToArrival = Number(cells, 9),
        };
    }

    private static double? Number(string[] cells, int index) =>
        Text(cells, index) is { } text
        && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;
}
