using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// A landable body's likely geological features that are not yet in the Commander's codex for the current
/// region, said once per body when both its scan and its geological count are known.
/// </summary>
public sealed class GeologyCallout : ICallout
{
    private const int MostFeatures = 3;

    private readonly HashSet<(long SystemAddress, int BodyId)> _said = [];

    public string Id => "geology";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind is not ("Scan" or "FSSBodySignals" or "SAASignalsFound")
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("BodyID") is not { } bodyId)
            {
                continue;
            }

            var key = (systemAddress, bodyId);

            // A body seen in the backlog has already been seen by the Commander.
            if (context.IsPriming)
            {
                _said.Add(key);
                continue;
            }

            if (_said.Contains(key)
                || !state.CodexLedger.IsWhole
                || state.Scans.For(systemAddress, bodyId) is not { Landable: true } scan
                || state.Bodies.Named(scan.BodyName) is not { GeologicalCount: > 0 })
            {
                continue;
            }

            var likely = GeologyCatalogue.Likely(scan);

            if (likely.Count == 0)
            {
                _said.Add(key);
                continue;
            }

            var regionId = state.Location.StarPos is { } starPos && GalacticRegions.Find(starPos) is { } region
                ? GalacticRegions.IdOf(region)
                : null;
            var logged = state.CodexLedger.Logged;
            var missing = likely.Where(feature => !logged.Contains((feature.EntryId, regionId))).ToList();

            _said.Add(key);

            if (missing.Count == 0)
            {
                continue;
            }

            var body = BiologyCallout.Short(scan.BodyName, journalEvent.String("StarSystem") ?? state.Location.StarSystem);

            yield return new Announcement(
                $"geology.{systemAddress}.{bodyId}",
                $"{body} could have {Features(missing)}, not in your codex for this region.");
        }
    }

    private static string Features(List<GeologyFeature> missing)
    {
        var names = missing.Take(MostFeatures).Select(feature => feature.Name.ToLowerInvariant() + "s").ToList();

        if (missing.Count > MostFeatures)
        {
            var rest = missing.Count - MostFeatures;

            return string.Join(", ", names) + " and " + (rest == 1 ? "one" : rest == 2 ? "two" : rest.ToString()) + " more";
        }

        return names.Count == 1 ? names[0] : string.Join(", ", names[..^1]) + " or " + names[^1];
    }
}
