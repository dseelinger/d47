using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>One codex entry and the regions it has been reported in, each with its count of distinct systems.</summary>
public sealed record CodexEntry(
    long EntryId,
    string Name,
    string Category,
    string SubCategory,
    IReadOnlyDictionary<int, int> Regions)
{
    /// <summary>The Spansh body fields that select a star or planet entry from <c>CodexBodies.tsv</c>; null for a <c>Codex.tsv</c> entry.</summary>
    public CodexBodyType? Body { get; init; }
}

/// <summary>
/// The Spansh body fields a <c>CodexBodies.tsv</c> entry is counted from: the sub-type, the luminosity classes as Roman
/// numerals ("IV,V,VI"; "0" is a hypergiant), and whether it is terraformable and has an atmosphere ("yes" or "no").
/// A blank field matches any value.
/// </summary>
public sealed record CodexBodyType(string SubType, string Luminosity, string Terraformable, string Atmosphere);

/// <summary>An entry reported in a region and how many systems reported it there.</summary>
public readonly record struct CodexRegionEntry(CodexEntry Entry, int Systems);

/// <summary>Codex entries parsed from a <c>Codex.tsv</c> or <c>CodexBodies.tsv</c> table.</summary>
public sealed class CodexTable
{
    private readonly Dictionary<long, CodexEntry> _entries;
    private readonly Dictionary<int, List<CodexRegionEntry>> _byRegion;

    private const string DatePrefix = "# Table date: ";

    private CodexTable(Dictionary<long, CodexEntry> entries, string? date)
    {
        _entries = entries;
        Date = date;
        _byRegion = [];

        foreach (var entry in entries.Values)
        {
            foreach (var (region, systems) in entry.Regions)
            {
                if (!_byRegion.TryGetValue(region, out var list))
                {
                    _byRegion[region] = list = [];
                }

                list.Add(new CodexRegionEntry(entry, systems));
            }
        }
    }

    public static CodexTable Parse(TextReader reader)
    {
        var entries = new Dictionary<long, CodexEntry>();
        string? date = null;

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(DatePrefix, StringComparison.Ordinal))
            {
                date ??= DateOnly.TryParseExact(line.AsSpan(DatePrefix.Length).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null;
                continue;
            }

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var cells = line.Split('\t');

            if (!long.TryParse(cells[0], CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            if (cells.Length == 5)
            {
                entries[id] = new CodexEntry(id, cells[1], cells[2], cells[3], ParseRegions(cells[4]));
            }
            else if (cells.Length == 9)
            {
                entries[id] = new CodexEntry(id, cells[1], cells[2], cells[3], ParseRegions(cells[8]))
                {
                    Body = new CodexBodyType(cells[4], cells[5], cells[6], cells[7]),
                };
            }
        }

        return new CodexTable(entries, date);
    }

    public static CodexTable Parse(string tsv)
    {
        using var reader = new StringReader(tsv);
        return Parse(reader);
    }

    public IReadOnlyCollection<CodexEntry> All => _entries.Values;

    /// <summary>The first <c># Table date: yyyy-MM-dd</c> line, as written; null when there is none.</summary>
    public string? Date { get; }

    /// <summary>The numbers of the regions with at least one entry.</summary>
    public IReadOnlyCollection<int> Regions => _byRegion.Keys;

    public CodexEntry? Entry(long entryId) => _entries.GetValueOrDefault(entryId);

    /// <summary>The entries reported in the region, most systems first.</summary>
    public IReadOnlyList<CodexRegionEntry> InRegion(int regionId) =>
        _byRegion.TryGetValue(regionId, out var list)
            ? list.OrderByDescending(found => found.Systems).ThenBy(found => found.Entry.EntryId).ToList()
            : [];

    private static Dictionary<int, int> ParseRegions(string cell)
    {
        var regions = new Dictionary<int, int>();

        foreach (var pair in cell.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(':');

            if (parts.Length == 2
                && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var region)
                && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var systems))
            {
                regions[region] = systems;
            }
        }

        return regions;
    }
}

/// <summary>
/// The shipped codex tables as one: <c>Codex.tsv</c> from <c>tools/gen-codex.py</c> and <c>CodexBodies.tsv</c> from
/// <c>tools/gen-codex-bodies.py</c>.
/// </summary>
public static class CodexCatalogue
{
    public const string ResourceName = "D47.Core.Codex";

    public const string BodiesResourceName = "D47.Core.CodexBodies";

    private static readonly Lazy<CodexTable> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static CodexTable Table => Loaded.Value;

    /// <summary>The text of one embedded table, or empty when it is not embedded.</summary>
    public static string Text(string resourceName)
    {
        using var stream = typeof(CodexCatalogue).GetTypeInfo().Assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static CodexTable Load() => CodexTable.Parse(Text(ResourceName) + "\n" + Text(BodiesResourceName));
}
