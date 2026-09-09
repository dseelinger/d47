using System.Globalization;
using System.Reflection;
using D47.Core.Lore;

namespace D47.Core.Knowledge;

/// <summary>
/// The shipped table of systems that mean something beyond their astrography (Phase 23, "Know which
/// systems carry lore").
/// </summary>
public static class LoreDirectory
{
    private const string ResourceName = "D47.Core.LoreTable";

    private static readonly Lazy<Dictionary<long, LoreEntry>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Every shipped row, in the table's order — which is alphabetical by name.</summary>
    public static IReadOnlyCollection<LoreEntry> All => Loaded.Value.Values;

    /// <summary>The row for a system, or null for the overwhelming majority of the galaxy.</summary>
    public static LoreEntry? ByAddress(long systemAddress) => Loaded.Value.GetValueOrDefault(systemAddress);

    /// <summary>
    /// The row for a system named rather than jumped into — what a Commander asking about somewhere
    /// reaches.
    /// </summary>
    public static LoreEntry? ByName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : Loaded.Value.Values.FirstOrDefault(
                entry => string.Equals(entry.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static Dictionary<long, LoreEntry> Load()
    {
        using var stream = typeof(LoreDirectory).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return new Dictionary<long, LoreEntry>();
        }

        using var reader = new StreamReader(stream);

        var rows = new Dictionary<long, LoreEntry>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("systemAddress\t", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length < 3
                || !long.TryParse(cells[0], CultureInfo.InvariantCulture, out var address)
                || cells[1].Length == 0
                || cells[2].Length == 0)
            {
                continue;
            }

            rows[address] = new LoreEntry(address, cells[1], cells[2]) { Tier = LoreTier.Shipped };
        }

        return rows;
    }
}
