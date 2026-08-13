using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// Choosing a sensible voice for each core, once, in the background (list.md Phase 11, #33).
/// <para>
/// The problem this solves is stated in the checklist: picking a character should not also mean
/// auditioning potentially hundreds of voices. Edge Neural offers several hundred and an
/// ElevenLabs account offers whatever it offers, and a Commander who wants to try Sentinel
/// wants to hear Sentinel, not to open a picker.
/// </para>
/// <para>
/// The model does the matching when there is one, because "a clipped, precise woman" against a
/// list of voice names is a judgement rather than a lookup. Without a model it falls back to
/// the keywords each hint carries — worse, and still better than every core sharing one voice.
/// </para>
/// </summary>
public static class VoicePairing
{
    /// <summary>
    /// A voice for each core that does not already have one. Never overwrites an existing
    /// pairing: a Commander who chose a voice by hand should not have it re-derived on the next
    /// launch, and nothing here distinguishes their choice from an earlier run of this.
    /// </summary>
    /// <param name="voices">
    /// What the provider offers. An empty list produces an empty result rather than an error —
    /// the provider may be unreachable, and pairing is a convenience.
    /// </param>
    /// <param name="existing">Pairings already made, by persona id.</param>
    public static async Task<IReadOnlyDictionary<string, string>> ChooseAsync(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyDictionary<string, string> existing,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        var unpaired = PersonaCatalog.All.Where(p => !existing.ContainsKey(p.Id)).ToArray();

        if (voices.Count == 0 || unpaired.Length == 0)
        {
            return existing;
        }

        var paired = new Dictionary<string, string>(existing, StringComparer.Ordinal);

        // One call for all of them rather than one per core. Eleven round trips to answer a
        // question nobody asked is not a background task, it is a bill.
        var chosen = await AskAsync(voices, unpaired, provider, model, spend, prices, logger, cancellationToken)
            .ConfigureAwait(false);

        foreach (var persona in unpaired)
        {
            var voice = chosen.GetValueOrDefault(persona.Id) ?? Nearest(persona, voices, paired.Values);

            if (voice is not null)
            {
                paired[persona.Id] = voice;
            }
        }

        logger?.LogInformation(
            "Paired {Count} personas to voices ({Model})",
            paired.Count - existing.Count,
            provider is null ? "no model, matched on keywords" : "chosen by the model");

        return paired;
    }

    /// <summary>
    /// The model's answer, as persona id to voice id. Empty when there is no model, when the
    /// call failed, or when nothing it said parsed — all four are the same thing to the caller,
    /// which then falls back per core rather than wholesale.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> AskAsync(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyList<Persona> unpaired,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (provider is null)
        {
            return new Dictionary<string, string>();
        }

        // Capped. A provider offering four hundred voices would make this prompt longer than
        // every other prompt d47 sends put together, and the answer would not be better for it.
        var offered = voices.Take(120).ToArray();

        var request = new System.Text.StringBuilder();
        request.AppendLine(
            "Pick the most fitting voice for each character below, from the voice list. "
            + "Answer with one line per character, exactly `id = voiceId`, and nothing else. "
            + "Use each voice at most once. If none fits, leave that character out.");
        request.AppendLine();
        request.AppendLine("Voices:");

        foreach (var voice in offered)
        {
            request.AppendLine($"  {voice.Id} — {voice.Label}");
        }

        request.AppendLine();
        request.AppendLine("Characters:");

        foreach (var persona in unpaired)
        {
            request.AppendLine($"  {persona.Id} — {persona.VoiceHint.Description}");
        }

        var answer = await FlavourTurn.AskAsync(
            provider,
            model,

            // No persona block. This is d47 asking a question about its own configuration, not
            // a core speaking, and handing one of them the job of casting the others would be
            // the one thing the isolation model forbids.
            persona: null,
            request.ToString(),
            gameState: null,
            spend,
            prices,
            logger,
            cancellationToken).ConfigureAwait(false);

        var chosen = new Dictionary<string, string>(StringComparer.Ordinal);

        if (answer is null)
        {
            return chosen;
        }

        var byId = offered.ToDictionary(v => v.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var line in answer.Split('\n'))
        {
            var parts = line.Split('=', 2);

            if (parts.Length != 2)
            {
                continue;
            }

            var personaId = parts[0].Trim().Trim('`', '*', '-', ' ');
            var voiceId = parts[1].Trim().Trim('`', '*', ' ');

            // Both halves checked against what was actually offered. A model that invents a
            // voice id would otherwise write a pairing that fails at the first line spoken —
            // the same anti-invention rule the guardrails state, enforced rather than asked for.
            if (PersonaCatalog.Knows(personaId)
                && byId.ContainsKey(voiceId)
                && !chosen.ContainsValue(voiceId))
            {
                chosen[personaId] = byId[voiceId].Id;
            }
        }

        return chosen;
    }

    /// <summary>
    /// The model-free fallback: the first voice whose name or metadata matches one of the
    /// core's keywords and is not already spoken for. Crude, and it only has to beat "every
    /// core sounds identical".
    /// </summary>
    public static string? Nearest(Persona persona, IReadOnlyList<VoiceInfo> voices, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in persona.VoiceHint.Keywords)
        {
            var match = voices.FirstOrDefault(voice =>
                !used.Contains(voice.Id)
                && (voice.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || voice.Gender?.Equals(keyword, StringComparison.OrdinalIgnoreCase) == true));

            if (match is not null)
            {
                return match.Id;
            }
        }

        // Nothing matched, so anything unused. A core with no voice at all falls back to the
        // ship AI's, which would mean two cores sounding the same — the thing this exists to
        // avoid.
        return voices.FirstOrDefault(voice => !used.Contains(voice.Id))?.Id;
    }
}
