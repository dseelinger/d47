using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>On approach to a body with biology that nobody has walked, once per body.</summary>
public sealed class FootfallApproachCallout : ICallout
{
    public string Id => "footfall-approach";

    private readonly HashSet<(long System, int Body)> _said = [];

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
                || state.Scans.For(systemAddress, bodyId) is not { WasFootfalled: false } scan
                || state.Bodies.Named(scan.BodyName) is not { HasBiology: true }
                || !_said.Add((systemAddress, bodyId)))
            {
                continue;
            }

            yield return new Announcement(
                $"footfall-approach.{systemAddress}.{bodyId}",
                $"Nobody has set foot on {scan.BodyName}. First footfall bonus is there.");
        }
    }
}
