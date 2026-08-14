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
/// The model does the matching, because "a clipped, precise woman" against a list of voice
/// names is a judgement rather than a lookup. With no model configured nothing is paired at
/// all: a substring match over voice names is not that judgement done worse, it is a different
/// question answered confidently, and the cores are better off keeping the voice in force until
/// there is something that can actually read the description.
/// </para>
/// <para>
/// Two entry points, one job. <see cref="ChooseAsync"/> covers the cast in one call when the
/// voice list first arrives; <see cref="ChooseOneAsync"/> covers the core the Commander has
/// just selected and that pass never reached.
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
    /// <summary>
    /// Pairings that are not a judgement call, and so are made whether or not there is a model:
    /// one core, on one voice provider, by the name of the voice.
    /// <para>
    /// The named voice is looked up in what the provider actually offers rather than stored as
    /// an id, because an id is an account's copy of a voice and the name is the thing that is
    /// the same on every account.
    /// </para>
    /// </summary>
    private static readonly (string Provider, string Persona, string Voice)[] Named =
    [
        (TtsProviderCatalog.ElevenLabsId, "warden", "George"),
    ];

    /// <param name="existing">Pairings already made, by persona id.</param>
    /// <param name="ttsProvider">
    /// Which voice provider the list came from, for the named defaults above. The voices
    /// themselves carry no provider, and "George" means an ElevenLabs voice and nothing on Edge.
    /// </param>
    public static async Task<IReadOnlyDictionary<string, string>> ChooseAsync(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyDictionary<string, string> existing,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        string? ttsProvider = null,
        CancellationToken cancellationToken = default)
    {
        var paired = WithNamedDefaults(voices, existing, ttsProvider);
        var unpaired = PersonaCatalog.All.Where(p => !paired.ContainsKey(p.Id)).ToArray();

        // No model, no pairing beyond the named defaults. Matching "a clipped, precise woman"
        // against three hundred voice names is a judgement, and the keyword fallback that used
        // to stand in for one dealt every core a voice on the strength of a substring — not the
        // same job done worse, a different job. Nothing is written instead, so the cores keep
        // the voice in force and the first model configured pairs them properly.
        if (provider is null || voices.Count == 0 || unpaired.Length == 0)
        {
            return paired.Count == existing.Count ? existing : paired;
        }

        // One call for all of them rather than one per core. Eleven round trips to answer a
        // question nobody asked is not a background task, it is a bill.
        var chosen = await AskAsync(voices, unpaired, provider, model, spend, prices, logger, cancellationToken)
            .ConfigureAwait(false);

        foreach (var persona in unpaired)
        {
            if (chosen.GetValueOrDefault(persona.Id) is { } voice)
            {
                paired[persona.Id] = voice;
            }
        }

        logger?.LogInformation("Paired {Count} personas to voices", paired.Count - existing.Count);

        return paired;
    }

    /// <summary>
    /// The pairings above, plus any named default this provider carries for a core that has
    /// none. Never overwrites: a named default is where a core starts, not where it is held.
    /// </summary>
    private static Dictionary<string, string> WithNamedDefaults(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyDictionary<string, string> existing,
        string? ttsProvider)
    {
        var paired = new Dictionary<string, string>(existing, StringComparer.Ordinal);

        if (ttsProvider is null)
        {
            return paired;
        }

        foreach (var (provider, persona, name) in Named)
        {
            if (!string.Equals(provider, ttsProvider, StringComparison.OrdinalIgnoreCase)
                || paired.ContainsKey(persona))
            {
                continue;
            }

            var match = voices.FirstOrDefault(voice =>
                voice.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && !paired.ContainsValue(voice.Id));

            if (match is not null)
            {
                paired[persona] = match.Id;
            }
        }

        return paired;
    }

    /// <summary>
    /// The pairings, less any that gives a core a voice of the wrong gender (list.md Phase 11,
    /// #33).
    /// <para>
    /// A pairing is never re-derived, because nothing distinguishes one the Commander chose from
    /// one an earlier pass made — but that rule protects a <em>choice</em>, and a core written
    /// as a man speaking in a woman's voice is not one. It is this pass having answered before
    /// the gender was stated to it. Dropped rather than replaced, so the core is paired again by
    /// the path that pairs every other unpaired core, with a model, in the ordinary way.
    /// </para>
    /// <para>
    /// A voice this provider does not offer is left alone. It may be another provider's, and
    /// which pairings belong to the provider in force is a question with an owner already.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, string> WithoutMiscastVoices(
        IReadOnlyDictionary<string, string> paired,
        IReadOnlyList<VoiceInfo> voices,
        ILogger? logger = null)
    {
        var byId = new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var voice in voices)
        {
            byId[voice.Id] = voice;
        }

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (personaId, voiceId) in paired)
        {
            if (byId.TryGetValue(voiceId, out var voice)
                && PersonaCatalog.Knows(personaId)
                && !PersonaCatalog.Resolve(personaId).VoiceHint.Admits(voice.Gender))
            {
                logger?.LogInformation(
                    "Dropping the voice {Voice} paired to {Persona}: the core is written {Gender}",
                    voiceId,
                    personaId,
                    PersonaCatalog.Resolve(personaId).VoiceHint.Gender);

                continue;
            }

            kept[personaId] = voiceId;
        }

        return kept.Count == paired.Count ? paired : kept;
    }

    /// <summary>
    /// A voice for one core, asked for at the moment it is needed — the Commander has just
    /// selected a core that has none (list.md Phase 11, #33).
    /// <para>
    /// The lazy half of the same job. The pass above runs once and covers the cast; this covers
    /// the core that pass never saw, because it ran before a model was configured, or before
    /// the provider's voice list had arrived, or because this core's pairing was cleared. Null
    /// when there is no model, when the call failed, or when nothing it named was on offer —
    /// all of which mean the same thing to the caller: leave the voice alone.
    /// </para>
    /// </summary>
    /// <param name="taken">Voices already spoken for, so two cores do not end up sharing one.</param>
    public static async Task<string?> ChooseOneAsync(
        Persona persona,
        IReadOnlyList<VoiceInfo> voices,
        IEnumerable<string> taken,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        string? ttsProvider = null,
        CancellationToken cancellationToken = default)
    {
        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Filtered rather than checked afterwards, because this path asks about one core and so
        // can. A core written as a man is not offered a woman's voice at all, which is a
        // narrower question for the model and one it cannot get wrong.
        var offered = voices
            .Where(voice => !used.Contains(voice.Id) && persona.VoiceHint.Admits(voice.Gender))
            .ToArray();

        // A named default is not a judgement, so it is answered here rather than asked of a
        // model — and answered even when there is none.
        if (Named.FirstOrDefault(named =>
                string.Equals(named.Provider, ttsProvider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(named.Persona, persona.Id, StringComparison.Ordinal)) is { Voice: { } wanted }
            && offered.FirstOrDefault(voice =>
                voice.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)) is { } named)
        {
            logger?.LogInformation("Voice for {Persona}: {Voice}, its named default", persona.Id, named.Id);
            return named.Id;
        }

        if (provider is null || offered.Length == 0)
        {
            return null;
        }

        var chosen = await AskAsync(
            offered, [persona], provider, model, spend, prices, logger, cancellationToken).ConfigureAwait(false);

        var voice = chosen.GetValueOrDefault(persona.Id);

        logger?.LogInformation(
            "Voice for {Persona}: {Voice}", persona.Id, voice ?? "none the model would name");

        return voice;
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
            + "Each character states the gender its voice must have; that part is not a "
            + "judgement call and an answer that ignores it is discarded. "
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
            var gender = persona.VoiceHint.Gender == VoiceGender.Unspecified
                ? string.Empty
                : $"{persona.VoiceHint.Gender.ToString().ToLowerInvariant()} voice. ";

            request.AppendLine($"  {persona.Id} — {gender}{persona.VoiceHint.Description}");
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
            if (!PersonaCatalog.Knows(personaId)
                || !byId.TryGetValue(voiceId, out var voice)
                || chosen.ContainsValue(voice.Id))
            {
                continue;
            }

            // The stated gender, enforced rather than asked for — the same rule as the voice
            // id itself. This pass puts every core in one prompt, so it cannot hand each of
            // them a filtered list the way the lazy path does; a miscast answer is dropped
            // instead, and that core is paired properly the next time it is selected.
            if (!PersonaCatalog.Resolve(personaId).VoiceHint.Admits(voice.Gender))
            {
                logger?.LogInformation(
                    "Not pairing {Persona} to {Voice}: the core is written {Gender}",
                    personaId,
                    voice.Id,
                    PersonaCatalog.Resolve(personaId).VoiceHint.Gender);

                continue;
            }

            chosen[personaId] = voice.Id;
        }

        return chosen;
    }

}
