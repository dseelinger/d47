using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>On every approach to a scanned body, high surface gravity or heat.</summary>
public sealed class ApproachHazardCallout : ICallout
{
    private const double StandardGravity = 9.80665;

    public string Id => "approach-hazard";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "ApproachBody"
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("BodyID") is not { } bodyId
                || state.Scans.For(systemAddress, bodyId) is not { } scan)
            {
                continue;
            }

            var heavy = scan.SurfaceGravity / StandardGravity > 1.0;
            var hot = SurfaceHeat.IsHostile(scan.SurfaceTemperature, landable: true);
            if (!heavy && !hot)
            {
                continue;
            }

            var gravity = heavy ? TargetAnswer.GravityFromScan(scan.SurfaceGravity!.Value) : null;
            var heat = hot ? scan.SurfaceTemperature!.Value.ToString("N0", CultureInfo.InvariantCulture) + " K" : null;
            var text = (heavy, hot) switch
            {
                (true, true) => $"High gravity and a hot surface on {scan.BodyName}: {gravity}, {heat}.",
                (true, false) => $"High gravity on {scan.BodyName}: {gravity}.",
                _ => $"Hot surface on {scan.BodyName}: {heat}.",
            };

            yield return new Announcement($"approach-hazard.{systemAddress}.{bodyId}", text);
        }
    }
}
