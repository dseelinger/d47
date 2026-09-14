using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>What a body's biology could reach, drawn from the species its conditions admit.</summary>
/// <param name="BestCase">The sum of the most valuable possible species in the highest-valued genera.</param>
/// <param name="Genera">The genera behind <paramref name="BestCase"/>, most valuable first.</param>
/// <param name="Low">With genera named, the sum of each named genus's least valuable species; otherwise null.</param>
/// <param name="High">With genera named, the sum of each named genus's most valuable species; otherwise null.</param>
public sealed record BiologyEstimate(long BestCase, IReadOnlyList<string> Genera, long? Low, long? High);

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
    public static BiologyEstimate? For(BodyScan scan, int biologicalCount, IReadOnlyList<string> namedGenera)
    {
        if (scan.SurfaceGravity is not { } gravity || scan.SurfaceTemperature is not { } temperature)
        {
            return null;
        }

        var possible = ExobiologyCatalogue.Possible(new BodyConditions
        {
            PlanetClass = scan.PlanetClass,
            Atmosphere = scan.Atmosphere,
            Volcanism = scan.Volcanism,
            Gravity = gravity / StandardGravity,
            Temperature = temperature,
            Pressure = (scan.SurfacePressure ?? 0) / StandardAtmosphere,
        });

        if (namedGenera.Count == 0)
        {
            var best = possible
                .GroupBy(entry => entry.Genus, StringComparer.OrdinalIgnoreCase)
                .Select(genus => (Genus: genus.Key, Value: genus.Max(entry => entry.Value)))
                .OrderByDescending(genus => genus.Value)
                .Take(Math.Max(biologicalCount, 0))
                .ToList();

            return new BiologyEstimate(best.Sum(genus => genus.Value), [.. best.Select(genus => genus.Genus)], null, null);
        }

        var named = namedGenera
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(genus =>
            {
                var species = possible.Where(entry => Same(entry.Genus, genus)).ToList();

                if (species.Count == 0)
                {
                    species = [.. ExobiologyCatalogue.All.Where(entry => Same(entry.Genus, genus))];
                }

                return (Genus: genus, Species: species);
            })
            .Where(genus => genus.Species.Count > 0)
            .Select(genus => (
                genus.Genus,
                Low: genus.Species.Min(entry => entry.Value),
                High: genus.Species.Max(entry => entry.Value)))
            .OrderByDescending(genus => genus.High)
            .ToList();

        var high = named.Sum(genus => genus.High);

        return new BiologyEstimate(high, [.. named.Select(genus => genus.Genus)], named.Sum(genus => genus.Low), high);
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
