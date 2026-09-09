using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Organic sampling, spoken on the surface (Phase 18, "Exobiology sampling").</summary>
public sealed class SamplingCallout : ICallout
{
    public string Id => "sampling";

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
                ? $"{genus.Species ?? genusName} analysed. That run is complete."
                : Progress(genus, genusName);

            yield return new Announcement($"sampling.{journalEvent.Timestamp.Ticks}", line);
        }
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
