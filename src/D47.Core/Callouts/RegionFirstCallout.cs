using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// A new organic codex entry that the shipped Canonn table does not list for the region, said once per
/// entry and region.
/// </summary>
public sealed class RegionFirstCallout : ICallout
{
    private const string Organic = "$Codex_SubCategory_Organic_Structures;";

    private readonly HashSet<(long EntryId, int RegionId)> _said = [];

    public string Id => "region-first";

    /// <summary>The table to check against.</summary>
    public Func<CodexTable> Table { get; set; } = () => CodexCatalogue.Table;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "CodexEntry"
                || journalEvent.String("SubCategory") != Organic
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

            var regions = Table().Entry(entryId)?.Regions;
            var region = journalEvent.String("Region_Localised");

            string? line;

            if (regions is not { Count: > 0 })
            {
                line = $"No Commander has reported {name} anywhere. It may be a first in the galaxy.";
            }
            else if (!regions.ContainsKey(regionId) && region is { Length: > 0 })
            {
                line = $"No Commander has reported {name} in the {region}. It may be a first for the region.";
            }
            else
            {
                continue;
            }

            yield return new Announcement($"region-first.{entryId}.{regionId}", line);
        }
    }
}
