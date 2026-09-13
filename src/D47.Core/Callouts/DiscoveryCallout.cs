using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The arrival star's autoscan, when nobody has sold data on it yet (#201).</summary>
public sealed class DiscoveryCallout : ICallout
{
    public string Id => "discovery";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "Scan"
                || journalEvent.String("ScanType") != "AutoScan"
                || journalEvent.String("StarType") is not { Length: > 0 }
                || journalEvent.Double("DistanceFromArrivalLS") != 0
                || journalEvent.Bool("WasDiscovered")
                || journalEvent.Long("SystemAddress") is not { } systemAddress)
            {
                continue;
            }

            yield return new Announcement(
                $"discovery.{systemAddress}",
                "Undiscovered system. Nobody has sold data on this star yet.");
        }
    }
}
