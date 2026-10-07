using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>Once per specimen, the moment the Commander is the genus' range from every specimen in the open set.</summary>
public sealed class SamplingRangeCallout : ICallout
{
    public string Id => "sampling-range";

    private (long System, int Body, string Genus, DateTimeOffset SeenAt)? _said;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming
            || context.State?.Sampling.MostRecent is not { } body
            || context.Status is not { HasPosition: true, PlanetRadius: { } radius } status)
        {
            yield break;
        }

        var here = new SurfaceFix(status.Latitude!.Value, status.Longitude!.Value, radius);

        foreach (var genus in body.InProgress)
        {
            // Every specimen needs a position, and all on the body the Commander stands on.
            if (genus.Taken is < 1 or >= 3
                || genus.Specimens.Count != genus.Taken
                || genus.Specimens.Any(point => Math.Abs(point.RadiusMetres - radius) > 1)
                || ExobiologyCatalogue.ColonyDistance(genus.Genus) is not { } range)
            {
                continue;
            }

            var key = (body.SystemAddress, body.BodyId, genus.Genus, genus.SeenAt);

            if (_said == key || genus.Specimens.Any(point => point.MetresTo(here) < range))
            {
                continue;
            }

            _said = key;

            yield return new Announcement(
                $"sampling-range.{body.SystemAddress}.{body.BodyId}.{genus.Genus}.{genus.SeenAt.Ticks}",
                $"Far enough for the next {genus.Species ?? genus.Genus} sample.");
        }
    }
}
