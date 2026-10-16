using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// A landable body whose biology could reach the threshold, said once per body: on the <c>SAASignalsFound</c> that
/// names its genera, with their candidate species, or else on its <c>Scan</c>.
/// </summary>
public sealed class BiologyCallout : ICallout
{
    public const long DefaultThreshold = 10_000_000;

    private const int MostGenera = 3;

    private readonly HashSet<(long SystemAddress, int BodyId)> _said = [];
    private readonly HashSet<(long SystemAddress, int BodyId)> _dssSaid = [];
    private readonly HashSet<(long SystemAddress, int BodyId)> _named = [];

    public string Id => "biology";

    /// <summary>The least best case, in credits, that is said.</summary>
    public Func<long> Threshold { get; set; } = () => DefaultThreshold;

    /// <summary>Whether another callout has already named the body.</summary>
    public Func<long, int, bool> AlreadySaid { get; set; } = (_, _) => false;

    /// <summary>Whether this callout has named the body. Tick thread only.</summary>
    public bool Named(long systemAddress, int bodyId) => _named.Contains((systemAddress, bodyId));

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            var isDss = journalEvent.Kind == "SAASignalsFound";

            if ((!isDss && journalEvent.Kind != "Scan")
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("BodyID") is not { } bodyId)
            {
                continue;
            }

            // A body scanned in the backlog has already been seen by the Commander.
            if (context.IsPriming)
            {
                _said.Add((systemAddress, bodyId));
                _dssSaid.Add((systemAddress, bodyId));
                continue;
            }

            if ((isDss ? _dssSaid : _said).Contains((systemAddress, bodyId))
                || AlreadySaid(systemAddress, bodyId)
                || state.Scans.For(systemAddress, bodyId) is not { Landable: true } scan
                || state.Bodies.Named(scan.BodyName) is not { BiologicalCount: > 0 } biology)
            {
                continue;
            }

            var named = biology.Source == BodySignalSource.SurfaceScan;

            if (isDss && (!named || biology.Genera.Count == 0))
            {
                continue;
            }

            var genera = named ? biology.Genera : [];
            var region = state.Location.StarPos is { } starPos ? GalacticRegions.Find(starPos) : null;

            if (BiologyPotential.For(scan, biology.BiologicalCount, genera, region) is not { } estimate
                || estimate.Genera.Count == 0
                || estimate.BestCase < Threshold())
            {
                continue;
            }

            _said.Add((systemAddress, bodyId));
            _named.Add((systemAddress, bodyId));

            if (isDss)
            {
                _dssSaid.Add((systemAddress, bodyId));
            }

            var body = Short(scan.BodyName, journalEvent.String("StarSystem") ?? state.Location.StarSystem);

            yield return new Announcement(
                $"biology.{systemAddress}.{bodyId}",
                isDss ? Line(body, estimate, Species(estimate)) : Line(body, estimate));
        }
    }

    /// <summary>At most three genera with their candidates, the rest counted; null where no genus was named.</summary>
    internal static string? Species(BiologyEstimate estimate)
    {
        if (estimate.Named.Count == 0)
        {
            return null;
        }

        var parts = estimate.Named.Take(MostGenera).Select(Candidates).ToList();

        if (estimate.Named.Count > MostGenera)
        {
            parts.Add($"{estimate.Named.Count - MostGenera} more");
        }

        return parts.Count == 1 ? parts[0] : string.Join(", ", parts[..^1]) + ", and " + parts[^1];
    }

    private static string Candidates(GenusCandidates genus)
    {
        var species = genus.Candidates;

        return species.Count switch
        {
            0 => genus.Genus,
            1 => species[0].Species,
            2 => species.All(entry => StartsWithGenus(entry.Species, genus.Genus))
                ? $"{genus.Genus} {species[0].Species[(genus.Genus.Length + 1)..]} or {species[1].Species[(genus.Genus.Length + 1)..]}"
                : $"{species[0].Species} or {species[1].Species}",
            _ => $"{genus.Genus}, one of {(species.Count == 3 ? "three" : species.Count.ToString())}",
        };
    }

    private static bool StartsWithGenus(string species, string genus) =>
        species.Length > genus.Length + 1 && species.StartsWith(genus + " ", StringComparison.OrdinalIgnoreCase);

    private static string Line(string body, BiologyEstimate estimate, string? species) =>
        species is null ? Line(body, estimate) : $"{Amount(body, estimate)}: {species}.";

    private static string Line(string body, BiologyEstimate estimate) =>
        $"{Amount(body, estimate)}: {string.Join(", ", estimate.Genera)}.";

    private static string Amount(string body, BiologyEstimate estimate)
    {
        var amount = estimate is { Low: { } low, High: { } high } && low < high
            ? $"{Credits(low)} to {Credits(high)}"
            : $"up to {Credits(estimate.BestCase)}";

        return $"{body} could hold {amount} in biology";
    }

    internal static string Credits(long amount)
    {
        var banded = SpokenCredits.Band(amount);

        return amount < 1_000_000 ? banded + " Cr" : banded;
    }

    internal static string Short(string bodyName, string? system) =>
        system is { Length: > 0 }
        && bodyName.Length > system.Length + 1
        && bodyName.StartsWith(system + " ", StringComparison.OrdinalIgnoreCase)
            ? bodyName[(system.Length + 1)..]
            : bodyName;
}
