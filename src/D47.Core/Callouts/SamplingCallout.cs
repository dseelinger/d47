using System.Globalization;
using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Knowledge;

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
                ? $"{genus.Species ?? genusName} analysed. That run is complete." + Value(journalEvent, state, systemAddress, bodyId)
                : Progress(genus, genusName) + RunValue(journalEvent);

            yield return new Announcement($"sampling.{journalEvent.Timestamp.Ticks}", line);
        }
    }

    private string Value(JournalEvent journalEvent, CommanderGameState state, long systemAddress, int bodyId)
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

        var done = Ledger.AnalysedOn(systemAddress, bodyId, state.Identity.FrontierId).ToList();

        if (!done.Any(held => string.Equals(held.Species, analysis.Species, StringComparison.OrdinalIgnoreCase)))
        {
            done.Add(analysis);
        }

        var signals = state.Biology.For(systemAddress, bodyId)?.BiologicalCount ?? 0;

        if (signals > 0 && done.Count == signals)
        {
            var missing = done.Count(held => held.Worth is null);
            var total = $" Every species on this body is done: {done.Count} species, "
                + $"{BiologyCallout.Credits(done.Sum(held => held.Worth ?? 0))} in total";

            return line + (missing == 0 ? total + "." : total + $", not counting {missing} I have no value for.");
        }

        var unsold = Ledger.Unsold(state.Identity.FrontierId);
        var unpriced = unsold.Unpriced.Count;

        line += unpriced == 0
            ? $" {BiologyCallout.Credits(unsold.Total)} unsold."
            : $" {BiologyCallout.Credits(unsold.Total)} unsold, not counting {unpriced} sample{(unpriced == 1 ? "" : "s")} I have no value for.";

        return signals > done.Count ? line + Left(signals - done.Count) : line;
    }

    private static string Left(int count)
    {
        var words = SpokenNumbers.Expand(count.ToString(CultureInfo.InvariantCulture));

        return $" {char.ToUpperInvariant(words[0])}{words[1..]} species left on this body.";
    }

    private string RunValue(JournalEvent journalEvent)
    {
        if (Ledger?.PriceRun(journalEvent) is not { } run)
        {
            return string.Empty;
        }

        return run switch
        {
            { Worth: not { } } => $" I have no value for {run.Species}.",
            { FirstFootfall: true } => $" Worth {BiologyCallout.Credits(run.Worth.Value)} once analysed, with the first footfall bonus.",
            { FirstFootfall: false } => $" Worth {BiologyCallout.Credits(run.Worth.Value)} once analysed. No first footfall bonus.",
            _ => $" Worth {BiologyCallout.Credits(run.Worth.Value)} once analysed; whether the first footfall bonus applies is not known.",
        };
    }

    private static string Progress(GenusProgress genus, string genusName)
    {
        var line = $"{genus.Species ?? genusName}, {genus.Taken} of {GenusProgress.Required}.";

        // The gap just travelled, where d47 had a position at both ends.
        if (genus.LastGapMetres is { } gap)
        {
            line += $" {OrganicSampling.Metres(gap)} from the last one.";
        }

        if (genus.Remaining == 0)
        {
            return line;
        }

        return ExobiologyCatalogue.ColonyDistance(genus.Genus) is { } range
            ? line + $" {genus.Remaining} to go, {OrganicSampling.Metres(range)} from here."
            : line + $" {genus.Remaining} to go.";
    }
}
