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
            if (!IsUndiscoveredArrivalStar(journalEvent) || journalEvent.Long("SystemAddress") is not { } systemAddress)
            {
                continue;
            }

            yield return new Announcement(
                $"discovery.{systemAddress}",
                "Undiscovered system. Nobody has sold data on this star yet.");
        }
    }

    /// <summary>An arrival star's autoscan that says nobody has discovered it.</summary>
    public static bool IsUndiscoveredArrivalStar(JournalEvent journalEvent) =>
        journalEvent.Kind == "Scan"
        && journalEvent.String("ScanType") == "AutoScan"
        && journalEvent.String("StarType") is { Length: > 0 }
        && journalEvent.Double("DistanceFromArrivalLS") == 0
        && !journalEvent.Bool("WasDiscovered");
}
