using System.Globalization;
using System.Reflection;

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

    /// <summary>Names close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> Near(string spoken) =>
        Catalogue.Near([.. Loaded.Value.Values.Select(entry => entry.Name)], spoken);

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

    private static RareEntry? Read(string[] cells)
    {
        if (Text(cells, 0) is not { } symbol
            || Text(cells, 1) is not { } name
            || Text(cells, 2) is not { } system
            || Text(cells, 3) is not { } station
            || Text(cells, 4) is not { } marketId
            || !long.TryParse(marketId, CultureInfo.InvariantCulture, out var id))
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
        };
    }

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;
}
