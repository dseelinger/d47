using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>Choosing a sensible voice for each core, once, in the background (Phase 11, #33).</summary>
public static class VoicePairing
{
    /// <summary>The revision of the named-default repair this build carries.</summary>
    public const int RepairRevision = 1;

    /// <summary>A voice for each core that does not already have one.</summary>
    /// <param name="voices">What the provider offers.</param>
    private static readonly (string Provider, string Persona, string Voice)[] Named =
    [
        (TtsProviderCatalog.ElevenLabsId, "warden", "George"),
    ];

    /// <summary>
    /// <param name="existing">Pairings already made, by persona id.</param> <param name="ttsProvider">
    /// Which voice provider the list came from, for the named defaults above.
    /// </summary>
    /// <param name="existing">Pairings already made, by persona id.</param>
    /// <param name="ttsProvider">
    /// Which voice provider the list came from, for the named defaults above.
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

        // No model, no pairing beyond the named defaults.
        if (provider is null || voices.Count == 0 || unpaired.Length == 0)
        {
            return paired.Count == existing.Count ? existing : paired;
        }

        // One call for all of them rather than one per core.
        var chosen = await AskAsync(voices, unpaired, provider, model, spend, prices, logger, cancellationToken)
            .ConfigureAwait(false);

        var taken = new HashSet<string>(paired.Values, StringComparer.OrdinalIgnoreCase);

        foreach (var persona in unpaired)
        {
            // The model's answer where it gave one and nothing else has it, and the nearest unused voice
            // otherwise (remediation.md 11, item 13). Leaving a core unpaired is what made two of them
            // sound alike. A voice already spoken for is refused when the answer is read, which is right
            // — and the core it was meant for was then left with nothing, and a core with no pairing speaks
            // in the provider's default.
            if (chosen.GetValueOrDefault(persona.Id) is not { } wanted)
            {
                // The model said nothing usable about this core — an invented voice, or one whose sex its
                // description refuses.
                continue;
            }

            if (taken.Add(wanted))
            {
                paired[persona.Id] = wanted;
                continue;
            }

            if (Spare(voices, taken, persona) is { } instead)
            {
                taken.Add(instead);
                paired[persona.Id] = instead;
            }
        }

        logger?.LogInformation("Paired {Count} personas to voices", paired.Count - existing.Count);

        return paired;
    }

    /// <summary>
    /// A voice nobody has yet, for a core the model could not be given one for (remediation.md 11, item
    /// 13).
    /// </summary>
    private static string? Spare(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlySet<string> taken,
        Persona persona)
    {
        var free = voices.Where(voice => !taken.Contains(voice.Id)).ToArray();

        return free.FirstOrDefault(voice => persona.VoiceHint.Admits(voice.Gender))?.Id;
    }

    /// <summary>
    /// The pairings above, plus any named default this provider carries for a core that has none.
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

            if (TheVoiceCalled(name, voices, persona, paired.Values) is { } match)
            {
                paired[persona] = match.Id;
            }
        }

        return paired;
    }

    /// <summary>The voice a named default means, in what this provider actually offers.</summary>
    private static VoiceInfo? TheVoiceCalled(
        string name,
        IReadOnlyList<VoiceInfo> voices,
        string persona,
        IEnumerable<string> taken)
    {
        var spokenFor = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var free = voices
            .Where(voice =>
                !spokenFor.Contains(voice.Id)
                && PersonaCatalog.Resolve(persona).VoiceHint.Admits(voice.Gender))
            .ToArray();

        return free.FirstOrDefault(voice => voice.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? free.FirstOrDefault(voice => WithoutDescriptor(voice.Name).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The name in front of whatever an account has appended to it.</summary>
    private static string WithoutDescriptor(string name)
    {
        var cut = name.IndexOfAny(['-', '–', '—', '(', ',']);

        return (cut < 0 ? name : name[..cut]).Trim();
    }

    /// <summary>
    /// The pairings, with every named default this provider carries put where it belongs (Phase 11,
    /// #33).
    /// </summary>
    public static IReadOnlyDictionary<string, string> WithNamedDefaultsRestored(
        IReadOnlyDictionary<string, string> paired,
        IReadOnlyList<VoiceInfo> voices,
        string? ttsProvider,
        ILogger? logger = null)
    {
        if (ttsProvider is null)
        {
            return paired;
        }

        var put = new Dictionary<string, string>(paired, StringComparer.Ordinal);
        var moved = false;

        foreach (var (provider, persona, name) in Named)
        {
            if (!string.Equals(provider, ttsProvider, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TheVoiceCalled(name, voices, persona, []) is not { } wanted)
            {
                continue;
            }

            // Taken off everyone else whether or not the named core already holds it.
            foreach (var other in put
                .Where(pair => pair.Value == wanted.Id && pair.Key != persona)
                .Select(pair => pair.Key)
                .ToArray())
            {
                logger?.LogInformation(
                    "Taking {Voice} off {Persona}: it is {Named}'s named default", wanted.Id, other, persona);

                put.Remove(other);
            }

            if (put.GetValueOrDefault(persona) != wanted.Id)
            {
                logger?.LogInformation("Restoring {Persona} to {Voice}, its named default", persona, wanted.Id);
                put[persona] = wanted.Id;
            }

            moved = moved || put.Count != paired.Count || put[persona] != paired.GetValueOrDefault(persona);
        }

        return moved ? put : paired;
    }

    /// <summary>The pairings, less any that gives a core a voice of the wrong gender (Phase 11, #33).</summary>
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
    /// A voice for one core, asked for at the moment it is needed — the Commander has just selected a
    /// core that has none (Phase 11, #33).
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

        // Filtered rather than checked afterwards, because this path asks about one core and so can.
        var offered = voices
            .Where(voice => !used.Contains(voice.Id) && persona.VoiceHint.Admits(voice.Gender))
            .ToArray();

        // A named default is not a judgement, so it is answered here rather than asked of a model — and
        // answered even when there is none.
        if (Named.FirstOrDefault(named =>
                string.Equals(named.Provider, ttsProvider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(named.Persona, persona.Id, StringComparison.Ordinal)) is { Voice: { } wanted }
            && TheVoiceCalled(wanted, offered, persona.Id, []) is { } named)
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

    /// <summary>One repair's result: the pairings to store, and whether the repair finished.</summary>
    /// <param name="Voices">Every core that had a pairing, still holding one.</param>
    /// <param name="Complete">
    /// False when at least one core kept a voice the repair wanted to take off it, because nothing
    /// could be chosen to replace it.
    /// </param>
    public sealed record VoiceRepair(IReadOnlyDictionary<string, string> Voices, bool Complete);

    /// <summary>Gives a voice back to every core a repair took one off (Phase 11, #33).</summary>
    /// <param name="before">The pairings as they stood, before the repair removed anything.</param>
    /// <param name="after">The repair's output.</param>
    public static async Task<VoiceRepair> WithReplacementsAsync(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        IReadOnlyList<VoiceInfo> voices,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        string? ttsProvider = null,
        CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(after, before))
        {
            return new VoiceRepair(after, Complete: true);
        }

        var repaired = new Dictionary<string, string>(after, StringComparer.Ordinal);
        var complete = true;

        foreach (var id in before.Keys.Where(id => !after.ContainsKey(id)))
        {
            // Asked one at a time, and against what has been assigned so far rather than against the starting
            // set: two cores repaired in the same pass must not both be handed the same replacement.
            var voice = await ChooseOneAsync(
                PersonaCatalog.Resolve(id),
                voices,
                repaired.Values,
                provider,
                model,
                spend,
                prices,
                logger,
                ttsProvider,
                cancellationToken).ConfigureAwait(false);

            if (voice is null)
            {
                logger?.LogInformation(
                    "Leaving {Persona} on {Voice}: nothing else could be chosen for it", id, before[id]);

                repaired[id] = before[id];
                complete = false;
                continue;
            }

            logger?.LogInformation("{Persona} takes {Voice} instead", id, voice);

            // The line the log line was always claiming.
            repaired[id] = voice;
        }

        return new VoiceRepair(repaired, complete);
    }

    /// <summary>The model's answer, as persona id to voice id.</summary>
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

        // Capped.
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

            // No persona block.
            persona: null,
            aboutMe: null,
            request.ToString(),
            gameState: null,
            spend,
            prices,
            logger,
            cancellationToken,

            // Cold (#98).
            sampling: LlmSampling.VoiceCasting).ConfigureAwait(false);

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

            // Both halves checked against what was actually offered.
            if (!PersonaCatalog.Knows(personaId)
                || !byId.TryGetValue(voiceId, out var voice)
                || chosen.ContainsValue(voice.Id))
            {
                continue;
            }

            // The stated gender, enforced rather than asked for — the same rule as the voice id itself.
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
