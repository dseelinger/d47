using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Organic sampling, spoken on the surface (Phase 18, "Exobiology sampling").</summary>
public sealed class SamplingCallout : ICallout
{
    public string Id => "sampling";

    /// <summary>Where an analysis is priced and the unsold total kept; null says nothing about value.</summary>
    public ExobiologyLedger? Ledger { get; init; }

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            // Nothing to prime.
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "ScanOrganic"
                || journalEvent.Named("Genus") is not { } genusName
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("Body") is not { } bodyId)
            {
                continue;
            }

            if (state.Sampling.On(systemAddress, bodyId)?.Genera.GetValueOrDefault(genusName)
                is not { } genus)
            {
                continue;
            }

            var line = journalEvent.String("ScanType") == "Analyse"
                ? $"{genus.Species ?? genusName} analysed. That run is complete." + Value(journalEvent, state)
                : Progress(genus, genusName);

            yield return new Announcement($"sampling.{journalEvent.Timestamp.Ticks}", line);
        }
    }

    private string Value(JournalEvent journalEvent, CommanderGameState state)
    {
        if (Ledger?.Price(journalEvent) is not { } analysis)
        {
            return string.Empty;
        }

        var line = analysis switch
        {
            { Worth: not { } } => $" I have no value for {analysis.Species}.",
            { FirstFootfall: true } => $" Worth {BiologyCallout.Credits(analysis.Worth.Value)} with the first footfall bonus.",
            { FirstFootfall: false } => $" Worth {BiologyCallout.Credits(analysis.Worth.Value)}. No first footfall bonus.",
            _ => $" Worth {BiologyCallout.Credits(analysis.Worth.Value)}; whether the first footfall bonus applies is not known.",
        };

        // Only once the older journals are folded, since before then the total leaves out what they hold.
        if (!Ledger.HistoryFolded)
        {
            return line;
        }

        var unsold = Ledger.Unsold(state.Identity.FrontierId);
        var unpriced = unsold.Unpriced.Count;

        return line + (unpriced == 0
            ? $" {BiologyCallout.Credits(unsold.Total)} unsold."
            : $" {BiologyCallout.Credits(unsold.Total)} unsold, not counting {unpriced} sample{(unpriced == 1 ? "" : "s")} I have no value for.");
    }

    private static string Progress(GenusProgress genus, string genusName)
    {
        var line = $"{genus.Species ?? genusName}, {genus.Taken} of {GenusProgress.Required}.";

        // The gap just travelled, where d47 had a position at both ends.
        if (genus.LastGapMetres is { } gap)
        {
            line += $" {OrganicSampling.Metres(gap)} from the last one.";
        }

        return line + (genus.Remaining == 0
            ? " Analyse when you are ready."
            : $" {genus.Remaining} to go.");
    }
}
