using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>An undiscovered F or G arrival star is worth a full scan for organics.</summary>
public sealed class OrganicsStarCallout : ICallout
{
    private readonly HashSet<long> _said = [];

    public string Id => "organicsStars";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (!DiscoveryCallout.IsUndiscoveredArrivalStar(journalEvent)
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || ClassLetter(journalEvent.String("StarType")) is not { } letter
                || !_said.Add(systemAddress))
            {
                continue;
            }

            yield return new Announcement(
                $"organicsStars.{systemAddress}",
                $"Class {letter} star. Worth a full scan for organics.");
        }
    }

    private static string? ClassLetter(string? starType) => starType switch
    {
        "F" or "F_WhiteSuperGiant" => "F",
        "G" or "G_WhiteSuperGiant" => "G",
        _ => null,
    };
}
