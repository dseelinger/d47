using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>A set of one or two specimens lost to a specimen of a different species.</summary>
public sealed class AbandonedSamplesCallout : ICallout
{
    public string Id => "sampling-abandoned";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State?.Sampling.Abandoned is not { } abandoned)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "ScanOrganic" || journalEvent.Timestamp != abandoned.At)
            {
                continue;
            }

            foreach (var set in abandoned.Sets)
            {
                yield return new Announcement(
                    $"sampling-abandoned.{abandoned.At.Ticks}.{set.Genus}",
                    $"That abandoned your {set.Taken} {set.Species ?? set.Genus} sample{(set.Taken == 1 ? "" : "s")}.");
            }

            yield break;
        }
    }
}
