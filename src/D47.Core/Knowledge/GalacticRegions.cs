using System.Globalization;
using System.Reflection;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

public sealed record RegionRun(double X0, double X1, double Z0, double Z1, int RegionId);

/// <summary>A straight piece of a region border, in light years.</summary>
public readonly record struct RegionEdge(double X0, double Z0, double X1, double Z1);

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

    private const double CellLy = (double)GridsPerLy / Scale;

    private static readonly Lazy<Map> Loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<IReadOnlyList<RegionRun>> AllRuns = new(BuildRuns, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<IReadOnlyList<RegionEdge>> AllEdges = new(BuildEdges, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The galaxy the grid covers, in light years.</summary>
    public static (double X0, double Z0, double X1, double Z1) Extent
    {
        get
        {
            var map = Loaded.Value;
            var width = map.Rows.Count == 0 ? 0 : map.Rows.Max(row => row.Sum(run => run.Span));

            return (X0, Z0, X0 + width * CellLy, Z0 + map.Rows.Count * CellLy);
        }
    }

    /// <summary>Every run of cells with a region, row by row.</summary>
    public static IReadOnlyList<RegionRun> Runs() => AllRuns.Value;

    /// <summary>Every cell edge where the region changes, outside the grid counting as no region.</summary>
    public static IReadOnlyList<RegionEdge> Borders() => AllEdges.Value;

    /// <summary>The centre of the region's cell nearest the mean of its cells, or null for an unknown number.</summary>
    public static StarPosition? LabelAt(int regionId)
    {
        double sumX = 0, sumZ = 0, count = 0;

        foreach (var run in AllRuns.Value.Where(run => run.RegionId == regionId))
        {
            var cells = (run.X1 - run.X0) / CellLy;

            sumX += (run.X0 + run.X1) / 2 * cells;
            sumZ += (run.Z0 + run.Z1) / 2 * cells;
            count += cells;
        }

        if (count == 0)
        {
            return null;
        }

        var meanX = sumX / count;
        var meanZ = sumZ / count;
        StarPosition? best = null;
        var bestDistance = double.MaxValue;

        foreach (var run in AllRuns.Value.Where(run => run.RegionId == regionId))
        {
            var cz = (run.Z0 + run.Z1) / 2;
            var firstCell = Math.Clamp(Math.Round((meanX - run.X0) / CellLy - 0.5), 0, Math.Round((run.X1 - run.X0) / CellLy) - 1);
            var cx = run.X0 + (firstCell + 0.5) * CellLy;
            var distance = (cx - meanX) * (cx - meanX) + (cz - meanZ) * (cz - meanZ);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new StarPosition(cx, 0, cz);
            }
        }

        return best;
    }

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

    /// <summary>The name of the codex region with this number, or null for an unknown number.</summary>
    public static string? NameOf(int id) => Loaded.Value.Names.GetValueOrDefault(id);

    /// <summary>The number of the codex region with this name, without regard to case, or null.</summary>
    public static int? IdOf(string name) =>
        Loaded.Value.Names
            .Where(pair => string.Equals(pair.Value, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(pair => (int?)pair.Key)
            .FirstOrDefault();

    private static IReadOnlyList<RegionRun> BuildRuns()
    {
        var runs = new List<RegionRun>();
        var rows = Loaded.Value.Rows;

        for (var pz = 0; pz < rows.Count; pz++)
        {
            var rx = 0;

            foreach (var (span, regionId) in rows[pz])
            {
                if (regionId != 0)
                {
                    runs.Add(new RegionRun(
                        X0 + rx * CellLy, X0 + (rx + span) * CellLy,
                        Z0 + pz * CellLy, Z0 + (pz + 1) * CellLy,
                        regionId));
                }

                rx += span;
            }
        }

        return runs;
    }

    private static IReadOnlyList<RegionEdge> BuildEdges()
    {
        var rows = Loaded.Value.Rows;
        var width = rows.Count == 0 ? 0 : rows.Max(row => row.Sum(run => run.Span));
        var edges = new List<RegionEdge>();
        var below = new int[width];
        var above = new int[width];

        for (var pz = 0; pz <= rows.Count; pz++)
        {
            Array.Clear(above);

            if (pz < rows.Count)
            {
                var rx = 0;

                foreach (var (span, regionId) in rows[pz])
                {
                    Array.Fill(above, regionId, rx, Math.Min(span, width - rx));
                    rx += span;
                }
            }

            var z = Z0 + pz * CellLy;
            var start = -1;

            for (var px = 0; px <= width; px++)
            {
                var differs = px < width && below[px] != above[px];

                if (differs && start < 0)
                {
                    start = px;
                }
                else if (!differs && start >= 0)
                {
                    edges.Add(new RegionEdge(X0 + start * CellLy, z, X0 + px * CellLy, z));
                    start = -1;
                }
            }

            if (pz < rows.Count)
            {
                for (var px = 0; px <= width; px++)
                {
                    var left = px == 0 ? 0 : above[px - 1];
                    var right = px == width ? 0 : above[px];

                    if (left != right)
                    {
                        var x = X0 + px * CellLy;
                        edges.Add(new RegionEdge(x, z, x, z + CellLy));
                    }
                }
            }

            (below, above) = (above, below);
        }

        return edges;
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
