using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>One codex entry, by its <see cref="CodexKeys">key</see>, and the regions it has been reported in, each with its count of distinct systems.</summary>
public sealed record CodexEntry(
    string Key,
    string Name,
    string Category,
    string SubCategory,
    IReadOnlyDictionary<int, int> Regions)
{
    /// <summary>The journal EntryID of a Canonn entry; null for a d47 kind.</summary>
    public long? EntryId => CodexKeys.EntryIdOf(Key);

    /// <summary>A d47 star or planet kind's place in the kind tree and its Spansh body fields; null for a Canonn entry.</summary>
    public CodexKind? Kind { get; init; }
}

/// <summary>
/// A <c>CodexBodies.tsv</c> row: its parent's key (null at the top of a family) and the Spansh body fields of the row —
/// subType, luminosity band ("dwarf", "giant", "supergiant", "hypergiant", "white dwarf"), terraformable ("yes" or "no"),
/// atmosphere, ring types ("Icy,Rocky", or "None") — and, on a banded star's subType row, the band most of its bodies
/// have. A field the row does not fix is null.
/// </summary>
public sealed record CodexKind(
    string? ParentKey,
    string SubType,
    string? Band,
    string? Terraformable,
    string? Atmosphere,
    string? Rings,
    string? UsualBand);

/// <summary>An entry reported in a region and how many systems reported it there.</summary>
public readonly record struct CodexRegionEntry(CodexEntry Entry, int Systems);

/// <summary>Codex entries parsed from a <c>Codex.tsv</c> or <c>CodexBodies.tsv</c> table.</summary>
public sealed class CodexTable
{
    private readonly Dictionary<string, CodexEntry> _entries;
    private readonly Dictionary<int, List<CodexRegionEntry>> _byRegion;

    private const string DatePrefix = "# Table date: ";

    /// <summary>The category of every d47 kind.</summary>
    public const string AstronomicalBodies = "Astronomical Bodies";

    private CodexTable(Dictionary<string, CodexEntry> entries, string? date)
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
        var entries = new Dictionary<string, CodexEntry>(StringComparer.Ordinal);
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

            if (cells.Length == 11 && CodexKeys.IsKind(cells[0]))
            {
                entries[cells[0]] = new CodexEntry(cells[0], cells[1], AstronomicalBodies, cells[2], ParseRegions(cells[10]))
                {
                    Kind = new CodexKind(Cell(cells[3]), cells[4], Cell(cells[5]), Cell(cells[6]), Cell(cells[7]), Cell(cells[8]), Cell(cells[9])),
                };
                continue;
            }

            if (cells.Length == 5 && long.TryParse(cells[0], CultureInfo.InvariantCulture, out var id))
            {
                var key = CodexKeys.Of(id);
                entries[key] = new CodexEntry(key, cells[1], cells[2], cells[3], ParseRegions(cells[4]));
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

    /// <summary>The entry of a journal EntryID.</summary>
    public CodexEntry? Entry(long entryId) => Entry(CodexKeys.Of(entryId));

    public CodexEntry? Entry(string key) => _entries.GetValueOrDefault(key);

    /// <summary>The entries reported in the region, most systems first, then Canonn entries by EntryID, then kinds by key.</summary>
    public IReadOnlyList<CodexRegionEntry> InRegion(int regionId) =>
        _byRegion.TryGetValue(regionId, out var list)
            ? list
                .OrderByDescending(found => found.Systems)
                .ThenBy(found => found.Entry.EntryId ?? long.MaxValue)
                .ThenBy(found => found.Entry.Key, StringComparer.Ordinal)
                .ToList()
            : [];

    private static string? Cell(string cell) => cell.Length == 0 ? null : cell;

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
