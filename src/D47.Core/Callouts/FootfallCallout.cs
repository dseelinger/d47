using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The first footfall on a body, marked in its scan by "Keep each body's scan in game state" (#202).</summary>
public sealed class FootfallCallout : ICallout
{
    public string Id => "footfall";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "Disembark"
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("BodyID") is not { } bodyId
                || state.Scans.For(systemAddress, bodyId) is not { } scan
                || scan.FootfallTakenAt != journalEvent.Timestamp)
            {
                continue;
            }

            yield return new Announcement(
                $"footfall.{systemAddress}.{bodyId}",
                $"First footfall on {scan.BodyName}.");
        }
    }
}
