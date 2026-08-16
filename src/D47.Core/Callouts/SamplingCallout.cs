using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Organic sampling, spoken on the surface (list.md Phase 18, "Exobiology sampling").
/// <para>
/// <b>The distance is the whole feature, because it is the number nobody can eyeball.</b> A
/// Commander in an SRV can see the genus and can count to three; what they cannot do is judge four
/// hundred metres across a ridge, and getting it wrong wastes the sample. So d47 says how far they
/// have moved since the last specimen and how many they have taken, and says it when the sample
/// lands rather than making them ask.
/// </para>
/// <para>
/// <b>What it will not say is whether that is far enough</b>, and that is a sourcing decision rather
/// than a gap. The required distance is published in the species' own Codex entry in-game, and every
/// machine-readable copy outside the game is a community wiki — which this phase's plan already ruled
/// on: what web search finds stays a sentence, and the same sentence copied into a shipped table is
/// d47 laundering somebody's forum post into its own voice. The Codex is two clicks away and is
/// authoritative; a table here would be neither.
/// </para>
/// </summary>
public sealed class SamplingCallout : ICallout
{
    public string Id => "sampling";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            // Nothing to prime. The sampling state is folded by the spine either way; this callout
            // only ever reacts to an event arriving, so the backlog leaves it nothing to say.
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

        // The gap just travelled, where d47 had a position at both ends. Said rather than judged:
        // whether it was enough is in the Codex, and the sample landing is itself the proof.
        if (genus.LastGapMetres is { } gap)
        {
            line += $" {OrganicSampling.Metres(gap)} from the last one.";
        }

        return line + (genus.Remaining == 0
            ? " Analyse when you are ready."
            : $" {genus.Remaining} to go.");
    }
}
