using System.Globalization;
using System.Reflection;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One geological surface feature and the body conditions it has been surveyed under.</summary>
public sealed record GeologyFeature(
    long EntryId,
    string Name,
    IReadOnlySet<string> PlanetTypes,
    IReadOnlySet<string> Atmospheres,
    IReadOnlySet<string> Volcanism);

public sealed class GeologyTable
{
    private readonly IReadOnlyList<GeologyFeature> features;

    private GeologyTable(IReadOnlyList<GeologyFeature> features) => this.features = features;

    public static GeologyTable Parse(TextReader reader)
    {
        var features = new List<GeologyFeature>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("entry_id\t", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length < 2 || !long.TryParse(cells[0], CultureInfo.InvariantCulture, out var entryId))
            {
                continue;
            }

            features.Add(new GeologyFeature(entryId, cells[1], Set(cells, 2), Set(cells, 3), Set(cells, 4)));
        }

        return new GeologyTable(features);
    }

    public static GeologyTable Parse(string tsv)
    {
        using var reader = new StringReader(tsv);

        return Parse(reader);
    }

    public IReadOnlyList<GeologyFeature> All => features;

    /// <summary>The features whose row admits this planet class, atmosphere and volcanism, in table order.</summary>
    public IReadOnlyList<GeologyFeature> Likely(string? planetClass, string? atmosphere, string? volcanism)
    {
        var planetType = BodyConditionFold.NormalisePlanetType(planetClass);
        var gas = BodyConditionFold.NormaliseAtmosphere(atmosphere);
        var volcanic = BodyConditionFold.NormaliseVolcanism(volcanism);

        return
        [
            .. features.Where(feature =>
                feature.PlanetTypes.Contains(planetType)
                && feature.Atmospheres.Contains(gas)
                && feature.Volcanism.Contains(volcanic)),
        ];
    }

    private static HashSet<string> Set(string[] cells, int index) =>
        index < cells.Length
            ? [.. cells[index].Split(';', StringSplitOptions.RemoveEmptyEntries)]
            : [];
}

/// <summary>Which geological features a scanned body could hold, learned from spansh.co.uk's surveyed bodies.</summary>
public static class GeologyCatalogue
{
    private const string ResourceName = "D47.Core.Geology";

    private static readonly Lazy<GeologyTable> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static GeologyTable Table => Loaded.Value;

    public static IReadOnlyList<GeologyFeature> Likely(BodyScan scan) =>
        Table.Likely(scan.PlanetClass, scan.Atmosphere, scan.Volcanism);

    private static GeologyTable Load()
    {
        using var stream = typeof(GeologyCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return GeologyTable.Parse(string.Empty);
        }

        using var reader = new StreamReader(stream);

        return GeologyTable.Parse(reader);
    }
}
