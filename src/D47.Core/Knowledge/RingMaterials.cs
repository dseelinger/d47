using System.Reflection;
using D47.Core.Mining;

namespace D47.Core.Knowledge;

/// <summary>How a ring material is mined: laser for a prospected rock, core for a motherlode.</summary>
public enum MiningMethod
{
    Laser,
    Core,
}

/// <summary>One ring type a mined material comes from; rank 1 is best, and types may share a rank.</summary>
public sealed record RingMaterial(string Symbol, string Name, string RingType, int Rank, MiningMethod Method);

/// <summary>The ring types each mined material comes from, generated from journals by <c>tools/gen-ring-materials.py</c>.</summary>
public static class RingMaterials
{
    private const string ResourceName = "D47.Core.RingMaterials";

    private static readonly Lazy<Dictionary<string, List<RingMaterial>>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The material's rows in rank order, matched on <see cref="MiningTarget.SymbolOf"/>; empty when it has none.</summary>
    public static IReadOnlyList<RingMaterial> For(string material) =>
        Loaded.Value.TryGetValue(MiningTarget.SymbolOf(material), out var rows) ? rows : [];

    private static Dictionary<string, List<RingMaterial>> Load()
    {
        var bySymbol = new Dictionary<string, List<RingMaterial>>(StringComparer.Ordinal);

        using var stream = typeof(RingMaterials).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return bySymbol;
        }

        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length != 5 || TsvCells.Integer(cells, 3) is not { } rank)
            {
                continue;
            }

            var method = cells[4] switch
            {
                "laser" => MiningMethod.Laser,
                "core" => MiningMethod.Core,
                _ => (MiningMethod?)null,
            };

            if (method is null)
            {
                continue;
            }

            if (!bySymbol.TryGetValue(cells[0], out var rows))
            {
                bySymbol[cells[0]] = rows = [];
            }

            rows.Add(new RingMaterial(cells[0], cells[1], cells[2], rank, method.Value));
        }

        return bySymbol;
    }
}
