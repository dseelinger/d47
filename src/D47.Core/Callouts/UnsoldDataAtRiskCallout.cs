using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>A warning when the Commander goes into danger carrying unsold exploration data (#638).</summary>
public sealed class UnsoldDataAtRiskCallout(CartographyLedger cartography, ExobiologyLedger exobiology) : ICallout
{
    public string Id => "unsold-data-at-risk";

    public const long Threshold = 5_000_000;

    public const string WarzoneKey = "unsold-data.warzone";

    public const string ExtractionSiteKey = "unsold-data.extraction-site";

    public const string HullKey = "unsold-data.hull";

    private const string HazardousExtractionSite = "$MULTIPLAYER_SCENARIO79_TITLE;";

    private const double HullHealthBelow = 0.6;

    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(30);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || !cartography.HistoryFolded || !exobiology.HistoryFolded)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            var (key, template) = Classify(journalEvent);

            if (key is null)
            {
                continue;
            }

            var commander = context.State?.Identity.FrontierId;
            var total = cartography.Unsold(commander).Total + exobiology.Unsold(commander).Total;

            if (total < Threshold)
            {
                continue;
            }

            yield return new Announcement(key, string.Format(template, SpokenCredits.Band(total)))
            {
                Cooldown = Cooldown,
            };
        }
    }

    private static (string? Key, string Template) Classify(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "SupercruiseDestinationDrop":
                var type = journalEvent.String("Type") ?? string.Empty;

                if (type.StartsWith("$Warzone_", StringComparison.Ordinal))
                {
                    return (WarzoneKey, "You are carrying {0} credits of unsold data into a fight.");
                }

                return type == HazardousExtractionSite
                    ? (ExtractionSiteKey, "You are carrying {0} credits of unsold data into a fight.")
                    : (null, string.Empty);

            case "HullDamage" when journalEvent.Bool("PlayerPilot")
                                   && !journalEvent.Bool("Fighter")
                                   && journalEvent.Double("Health") is < HullHealthBelow:
                return (HullKey, "Hull is failing with {0} credits of unsold data aboard.");

            default:
                return (null, string.Empty);
        }
    }
}
