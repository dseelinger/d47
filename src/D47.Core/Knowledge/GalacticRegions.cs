using System.Globalization;
using System.Reflection;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// Places a position in one of the 42 codex regions, ported from klightspeed/EliteDangerousRegionMap's
/// <c>FindRegion</c> (MIT); see NOTICE.
/// </summary>
public static class GalacticRegions
{
    private const string ResourceName = "D47.Core.Regions";

    private const double X0 = -49985;
    private const double Z0 = -24105;
    private const int Scale = 83;
    private const int GridsPerLy = 4096;

    private static readonly Lazy<Map> Loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The codex region this position falls in, or null outside the map.</summary>
    public static string? Find(StarPosition position)
    {
        var map = Loaded.Value;

        var px = (int)((position.X - X0) * Scale / GridsPerLy);
        var pz = (int)((position.Z - Z0) * Scale / GridsPerLy);

        if (px < 0 || pz < 0 || pz >= map.Rows.Count)
        {
            return null;
        }

        var rx = 0;

        foreach (var (span, regionId) in map.Rows[pz])
        {
            if (px < rx + span)
            {
                return regionId == 0 ? null : map.Names.GetValueOrDefault(regionId);
            }

            rx += span;
        }

        return null;
    }

    private sealed record Map(
        IReadOnlyDictionary<int, string> Names,
        IReadOnlyList<IReadOnlyList<(int Span, int RegionId)>> Rows);

    private static Map Load()
    {
        using var stream = typeof(GalacticRegions).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return new Map(new Dictionary<int, string>(), []);
        }

        using var reader = new StreamReader(stream);

        var names = new Dictionary<int, string>();
        var rows = new List<IReadOnlyList<(int Span, int RegionId)>>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length < 3)
            {
                continue;
            }

            switch (cells[0])
            {
                case "N" when int.TryParse(cells[1], CultureInfo.InvariantCulture, out var id):
                    names[id] = cells[2];
                    break;

                case "M" when int.TryParse(cells[1], CultureInfo.InvariantCulture, out var pz):
                    while (rows.Count <= pz)
                    {
                        rows.Add([]);
                    }

                    rows[pz] = ReadRuns(cells[2]);
                    break;
            }
        }

        return new Map(names, rows);
    }

    private static IReadOnlyList<(int Span, int RegionId)> ReadRuns(string cell)
    {
        var runs = new List<(int Span, int RegionId)>();

        foreach (var entry in cell.Split(','))
        {
            var split = entry.IndexOf(':');

            if (split > 0
                && int.TryParse(entry[..split], CultureInfo.InvariantCulture, out var span)
                && int.TryParse(entry[(split + 1)..], CultureInfo.InvariantCulture, out var regionId))
            {
                runs.Add((span, regionId));
            }
        }

        return runs;
    }
}
