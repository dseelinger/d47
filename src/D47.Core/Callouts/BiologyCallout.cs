using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>A landable body whose biology could reach the threshold, said on its <c>Scan</c>, once per body.</summary>
public sealed class BiologyCallout : ICallout
{
    public const long DefaultThreshold = 10_000_000;

    private readonly HashSet<(long SystemAddress, int BodyId)> _said = [];

    public string Id => "biology";

    /// <summary>The least best case, in credits, that is said.</summary>
    public Func<long> Threshold { get; set; } = () => DefaultThreshold;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "Scan"
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("BodyID") is not { } bodyId)
            {
                continue;
            }

            // A body scanned in the backlog has already been seen by the Commander.
            if (context.IsPriming)
            {
                _said.Add((systemAddress, bodyId));
                continue;
            }

            if (_said.Contains((systemAddress, bodyId))
                || state.Scans.For(systemAddress, bodyId) is not { Landable: true } scan
                || state.Bodies.Named(scan.BodyName) is not { BiologicalCount: > 0 } biology)
            {
                continue;
            }

            var genera = biology.Source == BodySignalSource.SurfaceScan ? biology.Genera : [];
            var region = state.Location.StarPos is { } starPos ? GalacticRegions.Find(starPos) : null;

            if (BiologyPotential.For(scan, biology.BiologicalCount, genera, region) is not { } estimate
                || estimate.Genera.Count == 0
                || estimate.BestCase < Threshold())
            {
                continue;
            }

            _said.Add((systemAddress, bodyId));

            yield return new Announcement(
                $"biology.{systemAddress}.{bodyId}",
                Line(Short(scan.BodyName, journalEvent.String("StarSystem")), estimate));
        }
    }

    private static string Line(string body, BiologyEstimate estimate)
    {
        var amount = estimate is { Low: { } low, High: { } high } && low < high
            ? $"{Credits(low)} to {Credits(high)}"
            : $"up to {Credits(estimate.BestCase)}";

        return $"{body} could hold {amount} in biology: {string.Join(", ", estimate.Genera)}.";
    }

    private static string Credits(long amount)
    {
        var banded = SpokenCredits.Band(amount);

        return amount < 1_000_000 ? banded + " Cr" : banded;
    }

    private static string Short(string bodyName, string? system) =>
        system is { Length: > 0 }
        && bodyName.Length > system.Length + 1
        && bodyName.StartsWith(system + " ", StringComparison.OrdinalIgnoreCase)
            ? bodyName[(system.Length + 1)..]
            : bodyName;
}
