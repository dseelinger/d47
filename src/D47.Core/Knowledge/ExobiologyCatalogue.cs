using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace D47.Core.Knowledge;

/// <summary>One species' sale value and the surveyed range of conditions it has been found on.</summary>
public sealed record ExobiologyEntry
{
    public required string Species { get; init; }

    public required string Genus { get; init; }

    /// <summary>Taken from the survey, never computed.</summary>
    public required long Value { get; init; }

    /// <summary>How many surveyed bodies the ranges below were learned from.</summary>
    public required int BodiesSampled { get; init; }

    /// <summary>Normalised planet classes this species has been found on.</summary>
    public required IReadOnlyList<string> PlanetTypes { get; init; }

    /// <summary>Normalised atmospheres this species has been found under.</summary>
    public required IReadOnlyList<string> Atmospheres { get; init; }

    /// <summary>Normalised volcanism this species has been found with.</summary>
    public required IReadOnlyList<string> Volcanism { get; init; }

    public required double GravityLow { get; init; }

    public required double GravityHigh { get; init; }

    public required double TemperatureLow { get; init; }

    public required double TemperatureHigh { get; init; }

    public required double PressureLow { get; init; }

    public required double PressureHigh { get; init; }

    /// <summary>Every galactic region a surveyed body carrying this species sits in.</summary>
    public required IReadOnlyList<string> Regions { get; init; }
}

/// <summary>
/// What a body's own scan says about it, in the units <see cref="ExobiologyCatalogue.Possible"/> compares
/// against the table.
/// </summary>
public sealed record BodyConditions
{
    public string? PlanetClass { get; init; }

    public string? Atmosphere { get; init; }

    public string? Volcanism { get; init; }

    /// <summary>In g — the journal's <c>SurfaceGravity</c> (m/s²) divided by 9.80665.</summary>
    public required double Gravity { get; init; }

    public required double Temperature { get; init; }

    /// <summary>In atm — the journal's <c>SurfacePressure</c> (Pa) divided by 101325.</summary>
    public required double Pressure { get; init; }
}

/// <summary>
/// Which exobiology species a body's scanned conditions could carry, learned from spansh.co.uk's
/// surveyed-body index rather than any community rule set (#204).
/// </summary>
public static class ExobiologyCatalogue
{
    private const string ResourceName = "D47.Core.Exobiology";

    private static readonly Lazy<IReadOnlyList<ExobiologyEntry>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<ExobiologyEntry> All => Loaded.Value;

    /// <summary>
    /// Every species whose surveyed columns and ranges contain the body's conditions. With
    /// <paramref name="region"/> given, also drops a species whose surveyed regions do not include it.
    /// </summary>
    public static IReadOnlyList<ExobiologyEntry> Possible(BodyConditions body, string? region = null)
    {
        var planetType = NormalisePlanetType(body.PlanetClass);
        var atmosphere = NormaliseAtmosphere(body.Atmosphere);
        var volcanism = NormaliseVolcanism(body.Volcanism);

        return
        [
            .. Loaded.Value.Where(entry =>
                entry.PlanetTypes.Contains(planetType, StringComparer.Ordinal)
                && entry.Atmospheres.Contains(atmosphere, StringComparer.Ordinal)
                && entry.Volcanism.Contains(volcanism, StringComparer.Ordinal)
                && body.Gravity >= entry.GravityLow && body.Gravity <= entry.GravityHigh
                && body.Temperature >= entry.TemperatureLow && body.Temperature <= entry.TemperatureHigh
                && body.Pressure >= entry.PressureLow && body.Pressure <= entry.PressureHigh
                && (region is null || entry.Regions.Contains(region, StringComparer.Ordinal))),
        ];
    }

    // ---------------------------------------------------------- normalisation
    //
    // The one fold shared with tools/gen-exobiology.py's normalise_planet_type, normalise_atmosphere and
    // normalise_volcanism. Spansh and the journal name the same condition in different words; this is
    // applied to Spansh's words when the table is generated and to the journal's words here, so neither
    // side has to agree with the other's spelling.

    private static readonly Regex Letters = new("[a-z]+", RegexOptions.Compiled);

    private static string NormalisePlanetType(string? raw) =>
        string.Concat(Letters.Matches((raw ?? string.Empty).ToLowerInvariant())
            .Select(match => match.Value == "world" ? "body" : match.Value));

    private static readonly HashSet<string> AtmosphereStopwords =
        new(StringComparer.Ordinal) { "thin", "thick", "hot", "atmosphere" };

    private static string NormaliseAtmosphere(string? raw)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (text.Length == 0 || text == "no atmosphere")
        {
            return "none";
        }

        text = text.Replace("sulphur", "sulfur", StringComparison.Ordinal);

        return string.Concat(Letters.Matches(text).Select(match => match.Value)
            .Where(word => !AtmosphereStopwords.Contains(word)));
    }

    private static string NormaliseVolcanism(string? raw)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (text.Length == 0 || text == "no volcanism")
        {
            return "none";
        }

        return string.Concat(Letters.Matches(text).Select(match => match.Value)
            .Where(word => word != "volcanism"));
    }

    // -------------------------------------------------------------- loading

    private static IReadOnlyList<ExobiologyEntry> Load()
    {
        using var stream = typeof(ExobiologyCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            // Nothing can be predicted without it, and predicting anyway is the failure the whole table
            // exists to avoid.
            return [];
        }

        using var reader = new StreamReader(stream);

        var entries = new List<ExobiologyEntry>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("species\t", StringComparison.Ordinal))
            {
                continue;
            }

            if (Read(line.Split('\t')) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static ExobiologyEntry? Read(string[] cells)
    {
        if (Text(cells, 0) is not { } species
            || Text(cells, 1) is not { } genus
            || Integer(cells, 3) is not { } sampled
            || Number(cells, 7) is not { } gravityLow
            || Number(cells, 8) is not { } gravityHigh
            || Number(cells, 9) is not { } temperatureLow
            || Number(cells, 10) is not { } temperatureHigh
            || Number(cells, 11) is not { } pressureLow
            || Number(cells, 12) is not { } pressureHigh
            || Text(cells, 2) is not { } valueText
            || !long.TryParse(valueText, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return new ExobiologyEntry
        {
            Species = species,
            Genus = genus,
            Value = value,
            BodiesSampled = sampled,
            PlanetTypes = List(cells, 4),
            Atmospheres = List(cells, 5),
            Volcanism = List(cells, 6),
            GravityLow = gravityLow,
            GravityHigh = gravityHigh,
            TemperatureLow = temperatureLow,
            TemperatureHigh = temperatureHigh,
            PressureLow = pressureLow,
            PressureHigh = pressureHigh,
            Regions = List(cells, 13),
        };
    }

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;

    private static int? Integer(string[] cells, int index) =>
        Text(cells, index) is { } text && int.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static double? Number(string[] cells, int index) =>
        Text(cells, index) is { } text
        && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static IReadOnlyList<string> List(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(';', StringSplitOptions.RemoveEmptyEntries)];
}
