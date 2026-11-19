using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// A new codex entry that the shipped Canonn table does not list for the region, said once per entry and
/// region, ending with the offer to report it to Canonn. Always reworded, and said ahead of the discovery line.
/// </summary>
public sealed class RegionFirstCallout : ICallout
{
    /// <summary>Every region-first key starts with this; <see cref="RewordChance"/> always rewords them.</summary>
    public const string KeyPrefix = "region-first.";

    private readonly HashSet<(long EntryId, int RegionId)> _said = [];

    public string Id => "region-first";

    /// <summary>The table to check against.</summary>
    public Func<CodexTable> Table { get; set; } = () => CodexCatalogue.Table;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "CodexEntry"
                || !journalEvent.Bool("IsNewEntry")
                || journalEvent.Long("EntryID") is not { } entryId
                || CodexLedger.RegionIdOf(journalEvent.String("Region")) is not { } regionId
                || !_said.Add((entryId, regionId)))
            {
                continue;
            }

            if (context.IsPriming
                || context.State?.CodexLedger is not { IsWhole: true } ledger
                || ledger.Entries.Any(entry =>
                    entry.EntryId == entryId && entry.RegionId == regionId && entry.At < journalEvent.Timestamp)
                || journalEvent.String("Name_Localised") is not { Length: > 0 } name)
            {
                continue;
            }

            var region = journalEvent.String("Region_Localised");

            string? line;

            switch (CodexFirsts.Judge(Table(), entryId, regionId, journalEvent.String("SubCategory")))
            {
                case CodexFirst.Galaxy:
                    line = $"No Commander has reported {name} anywhere. It may be a first in the galaxy. {RegionFirstReporting.Offer}";
                    break;
                case CodexFirst.Region when region is { Length: > 0 }:
                    line = $"No Commander has reported {name} in the {region}. It may be a first for the region. {RegionFirstReporting.Offer}";
                    break;
                default:
                    continue;
            }

            yield return new Announcement(
                $"{KeyPrefix}{entryId}.{regionId}", line, CalloutUrgency.Urgent)
            {
                Cue = AlertCue.RegionFirst,
            };
        }
    }
}
