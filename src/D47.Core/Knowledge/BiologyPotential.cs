using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>What a body's biology could reach, drawn from the species its conditions admit.</summary>
/// <param name="BestCase">The sum of the most valuable possible species in the highest-valued genera.</param>
/// <param name="Genera">The genera behind <paramref name="BestCase"/>, most valuable first.</param>
/// <param name="Low">With genera named, the sum of each named genus's least valuable species; otherwise null.</param>
/// <param name="High">With genera named, the sum of each named genus's most valuable species; otherwise null.</param>
/// <param name="Named">With genera named, one entry per genus, most valuable first; otherwise empty.</param>
public sealed record BiologyEstimate(
    long BestCase,
    IReadOnlyList<string> Genera,
    long? Low,
    long? High,
    IReadOnlyList<GenusCandidates> Named);

/// <summary>A named genus's value range and the species the body's conditions admit, most valuable first.</summary>
/// <param name="Candidates">Empty where the range comes from the whole genus because no species was admitted.</param>
public sealed record GenusCandidates(string Genus, long Low, long High, IReadOnlyList<ExobiologyEntry> Candidates);

/// <summary>The value range a body's biology could fall in, one species per genus.</summary>
public static class BiologyPotential
{
    private const double StandardGravity = 9.80665;
    private const double StandardAtmosphere = 101325;

    /// <summary>
    /// The estimate for <paramref name="scan"/> holding <paramref name="biologicalCount"/> biological signals, or
    /// null where the scan lacks the gravity or temperature the table is matched on.
    /// </summary>
    /// <param name="namedGenera">
    /// Genera a surface scan named. A named genus with no species admitted by the conditions draws on every
    /// species of that genus in the table.
    /// </param>
    /// <param name="region">The Commander's current galactic region, or null to consider every region.</param>
    public static BiologyEstimate? For(
        BodyScan scan, int biologicalCount, IReadOnlyList<string> namedGenera, string? region = null)
    {
        if (scan.SurfaceGravity is not { } gravity || scan.SurfaceTemperature is not { } temperature)
        {
            return null;
        }

        var possible = ExobiologyCatalogue.Possible(
            new BodyConditions
            {
                PlanetClass = scan.PlanetClass,
                Atmosphere = scan.Atmosphere,
                Volcanism = scan.Volcanism,
                Gravity = gravity / StandardGravity,
                Temperature = temperature,
                Pressure = (scan.SurfacePressure ?? 0) / StandardAtmosphere,
            },
            region);

        if (namedGenera.Count == 0)
        {
            var best = possible
                .GroupBy(entry => entry.Genus, StringComparer.OrdinalIgnoreCase)
                .Select(genus => (Genus: genus.Key, Value: genus.Max(entry => entry.Value)))
                .OrderByDescending(genus => genus.Value)
                .Take(Math.Max(biologicalCount, 0))
                .ToList();

            return new BiologyEstimate(best.Sum(genus => genus.Value), [.. best.Select(genus => genus.Genus)], null, null, []);
        }

        var named = namedGenera
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(genus =>
            {
                var candidates = possible
                    .Where(entry => Same(entry.Genus, genus))
                    .OrderByDescending(entry => entry.Value)
                    .ToList();

                var range = candidates.Count > 0
                    ? candidates
                    : [.. ExobiologyCatalogue.All.Where(entry => Same(entry.Genus, genus))];

                return (Genus: genus, Range: range, Candidates: candidates);
            })
            .Where(genus => genus.Range.Count > 0)
            .Select(genus => new GenusCandidates(
                genus.Genus,
                genus.Range.Min(entry => entry.Value),
                genus.Range.Max(entry => entry.Value),
                genus.Candidates))
            .OrderByDescending(genus => genus.High)
            .ToList();

        var high = named.Sum(genus => genus.High);

        return new BiologyEstimate(
            high, [.. named.Select(genus => genus.Genus)], named.Sum(genus => genus.Low), high, named);
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
