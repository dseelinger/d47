using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A set of one or two specimens on the body being left, said on <c>LeaveBody</c> and on a hyperspace
/// <c>StartJump</c>, which a jump from orbital cruise writes in place of <c>LeaveBody</c>.
/// </summary>
public sealed class UnfinishedSamplesCallout : ICallout
{
    private const int MostSaid = 3;

    public string Id => "sampling-unfinished";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            long? systemAddress;
            int? bodyId;
            string? bodyName;

            switch (journalEvent.Kind)
            {
                // Location has already dropped the body by now, so the event names it.
                case "LeaveBody":
                    systemAddress = journalEvent.Long("SystemAddress");
                    bodyId = journalEvent.Int("BodyID");
                    bodyName = journalEvent.String("Body");
                    break;

                case "StartJump" when journalEvent.String("JumpType") == "Hyperspace":
                    systemAddress = state.Location.SystemAddress;
                    bodyId = state.Location.BodyId;
                    bodyName = state.Location.Body;
                    break;

                default:
                    continue;
            }

            if (systemAddress is not { } system
                || bodyId is not { } body
                || state.Sampling.On(system, body) is not { } sampling)
            {
                continue;
            }

            var open = sampling.InProgress.Where(genus => genus.Taken > 0).Take(MostSaid);

            foreach (var genus in open)
            {
                yield return new Announcement(
                    $"sampling-unfinished.{journalEvent.Timestamp.Ticks}.{genus.Genus}",
                    $"Leaving {bodyName ?? "this body"} with {genus.Species ?? genus.Genus} at {genus.Taken} of {GenusProgress.Required}.");
            }
        }
    }
}
