using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>Choosing a voice for each core, the carrier captain and the tower, from a provider's list.</summary>
public static class VoicePairing
{
    /// <summary>The most voices one request to the model lists.</summary>
    public const int VoicesPerRequest = 120;

    /// <summary>
    /// The completion ceiling for one casting request. Matching a dozen characters against a list is
    /// reasoning, and a thinking model spends a remark's ceiling before it writes the answer.
    /// </summary>
    public const int CastingTokens = 8000;

    /// <summary>One voice to be chosen, and how it should sound.</summary>
    public sealed record Slot(string Id, VoiceHint Hint);

    public static Slot CarrierCaptain { get; } = new(
        "carrier-captain",
        new VoiceHint(
            "The captain of the Commander's fleet carrier: announcements to the crew and the Commander, "
            + "jump countdowns, calm command over a ship's tannoy."));

    public static Slot Tower { get; } = new(
        "tower",
        new VoiceHint(
            "The fleet carrier's tower control: docking clearances and traffic instructions, brisk and "
            + "procedural over the radio."));

    /// <summary>The two carrier roles.</summary>
    public static IReadOnlyList<Slot> CarrierRoles { get; } = [CarrierCaptain, Tower];

    /// <summary>Every core, as a slot.</summary>
    public static IReadOnlyList<Slot> Cores => [.. PersonaCatalog.All.Select(SlotFor)];

    /// <summary>One core as a slot; only Cora and Analyst Prime are bound by gender.</summary>
    public static Slot SlotFor(Persona persona) => new(
        persona.Id,
        persona.VoiceHint with
        {
            Gender = persona.Id == PersonaCatalog.Cora.Id || persona.Id == PersonaCatalog.AnalystPrime.Id
                ? persona.VoiceHint.Gender
                : VoiceGender.Unspecified,
        });

    /// <summary>
    /// A voice for every core not already in <paramref name="existing"/>, merged with it. The same
    /// instance comes back when nothing was added.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ChooseAsync(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyDictionary<string, string> existing,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        Random? random = null,
        CancellationToken cancellationToken = default)
    {
        var unpaired = Cores.Where(slot => !existing.ContainsKey(slot.Id)).ToArray();

        var chosen = await ChooseForAsync(
            voices, unpaired, existing.Values, provider, model, spend, prices, logger, random, cancellationToken)
            .ConfigureAwait(false);

        if (chosen.Count == 0)
        {
            return existing;
        }

        var paired = new Dictionary<string, string>(existing, StringComparer.Ordinal);

        foreach (var (id, voice) in chosen)
        {
            paired[id] = voice;
        }

        return paired;
    }

    /// <summary>
    /// A voice for each slot, by slot id: the model's choice where there is a model, otherwise a free
    /// voice whose metadata fits, otherwise a free voice at random. Voices in <paramref name="taken"/>
    /// and voices already handed out are reused only once no free one fits.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ChooseForAsync(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyList<Slot> slots,
        IEnumerable<string> taken,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        Random? random = null,
        CancellationToken cancellationToken = default)
    {
        var chosen = new Dictionary<string, string>(StringComparer.Ordinal);

        if (slots.Count == 0 || voices.Count == 0)
        {
            return chosen;
        }

        random ??= Random.Shared;

        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var english = voices.Where(SpeaksEnglish).ToArray();
        IReadOnlyList<VoiceInfo> offered = english.Length > 0 ? english : voices;

        if (provider is not null)
        {
            var free = offered.Where(voice => !used.Contains(voice.Id)).ToArray();
            var answered = await AskInRoundsAsync(
                free, slots, provider, model, spend, prices, logger, cancellationToken).ConfigureAwait(false);

            foreach (var slot in slots)
            {
                if (answered.GetValueOrDefault(slot.Id) is { } wanted && used.Add(wanted))
                {
                    chosen[slot.Id] = wanted;
                }
            }
        }

        // Gender-bound slots first, so an unbound one does not take the only voice that fits them.
        foreach (var slot in slots
            .Where(slot => !chosen.ContainsKey(slot.Id))
            .OrderBy(slot => slot.Hint.Gender == VoiceGender.Unspecified))
        {
            if ((Matched(offered, used, slot, random) ?? Matched(voices, used, slot, random)) is { } voice)
            {
                used.Add(voice);
                chosen[slot.Id] = voice;
            }
        }

        logger?.LogInformation(
            "Paired {Count} of {Slots} voices ({Model})",
            chosen.Count,
            slots.Count,
            provider is null ? "no model" : "model");

        return chosen;
    }

    /// <summary>
    /// A voice for a slot without the model: a free voice whose labelled gender fits, then a free voice
    /// with no gender label, then any voice that fits even if already used.
    /// </summary>
    private static string? Matched(IReadOnlyList<VoiceInfo> pool, IReadOnlySet<string> used, Slot slot, Random random)
    {
        var fitting = pool.Where(voice => slot.Hint.Admits(voice.Gender)).ToArray();
        var free = fitting.Where(voice => !used.Contains(voice.Id)).ToArray();

        var tiers = slot.Hint.Gender == VoiceGender.Unspecified
            ? new[] { free, fitting }
            : [
                [.. free.Where(voice => VoiceHint.Read(voice.Gender) == slot.Hint.Gender)],
                free,
                fitting,
            ];

        return tiers.FirstOrDefault(tier => tier.Length > 0) is { } candidates
            ? candidates[random.Next(candidates.Length)].Id
            : null;
    }

    /// <summary>
    /// Whether a voice is English or not tagged with a language at all. An accent label such as
    /// "british", or "multilingual", is not a language tag.
    /// </summary>
    private static bool SpeaksEnglish(VoiceInfo voice)
    {
        var language = voice.Locale.Split('-', '_')[0];

        return language.Length is not (2 or 3) || language.Equals("en", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The pairings, less any that gives Cora or Analyst Prime a voice of the wrong gender.</summary>
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
                && SlotFor(PersonaCatalog.Resolve(personaId)).Hint is var hint
                && !hint.Admits(voice.Gender))
            {
                logger?.LogInformation(
                    "Dropping the voice {Voice} paired to {Persona}: the core is written {Gender}",
                    voiceId,
                    personaId,
                    hint.Gender);

                continue;
            }

            kept[personaId] = voiceId;
        }

        return kept.Count == paired.Count ? paired : kept;
    }

    /// <summary>A voice for one core, chosen at the moment it is needed.</summary>
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
        Random? random = null,
        CancellationToken cancellationToken = default)
    {
        var chosen = await ChooseForAsync(
            voices, [SlotFor(persona)], taken, provider, model, spend, prices, logger, random, cancellationToken)
            .ConfigureAwait(false);

        var voice = chosen.GetValueOrDefault(persona.Id);

        logger?.LogInformation("Voice for {Persona}: {Voice}", persona.Id, voice ?? "none");

        return voice;
    }

    /// <summary>One repair's result: the pairings to store, and whether the repair finished.</summary>
    /// <param name="Voices">Every core that had a pairing, still holding one.</param>
    /// <param name="Complete">
    /// False when at least one core kept a voice the repair wanted to take off it, because nothing
    /// could be chosen to replace it.
    /// </param>
    public sealed record VoiceRepair(IReadOnlyDictionary<string, string> Voices, bool Complete);

    /// <summary>Gives a voice back to every core a repair took one off.</summary>
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
        Random? random = null,
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
                random,
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

            repaired[id] = voice;
        }

        return new VoiceRepair(repaired, complete);
    }

    /// <summary>
    /// The recorded pairing with every entry that actually changed between <paramref name="before"/>
    /// and <paramref name="after"/> added or refreshed — a hand-picked voice that passed through a
    /// repair untouched is not one of them (#85).
    /// </summary>
    public static IReadOnlyDictionary<string, string> WithPairingsRecorded(
        IReadOnlyDictionary<string, string> recorded,
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after)
    {
        var merged = new Dictionary<string, string>(recorded, StringComparer.Ordinal);

        foreach (var (id, voice) in after)
        {
            if (!string.Equals(before.GetValueOrDefault(id), voice, StringComparison.Ordinal))
            {
                merged[id] = voice;
            }
        }

        return merged;
    }

    /// <summary>
    /// The model's answer over a list of any length: where the list is longer than one request holds,
    /// each part is asked for a shortlist and the shortlists are asked again.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> AskInRoundsAsync(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyList<Slot> slots,
        ILlmProvider provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var pool = voices;

        while (pool.Count > VoicesPerRequest)
        {
            var parts = pool.Chunk(VoicesPerRequest).ToArray();

            // The parts are independent, so they are asked at once.
            var answers = await Task.WhenAll(parts.Select(part =>
                AskAsync(part, slots, provider, model, spend, prices, logger, cancellationToken))).ConfigureAwait(false);

            List<VoiceInfo> shortlist =
            [
                .. parts.Zip(answers).SelectMany(pair => pair.First.Where(voice =>
                    pair.Second.Values.Contains(voice.Id, StringComparer.OrdinalIgnoreCase))),
            ];

            // A round that did not shorten the list would repeat forever.
            if (shortlist.Count == 0 || shortlist.Count >= pool.Count)
            {
                return new Dictionary<string, string>();
            }

            pool = shortlist;
        }

        return await AskAsync(pool, slots, provider, model, spend, prices, logger, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The model's answer, as slot id to voice id.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> AskAsync(
        IReadOnlyList<VoiceInfo> offered,
        IReadOnlyList<Slot> slots,
        ILlmProvider provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var chosen = new Dictionary<string, string>(StringComparer.Ordinal);

        if (offered.Count == 0)
        {
            return chosen;
        }

        var request = new System.Text.StringBuilder();
        request.AppendLine(
            "Pick the most fitting voice for each character below, from the voice list. "
            + "Where a character states the gender its voice must have, that part is not a "
            + "judgement call and an answer that ignores it is discarded. "
            + "Answer with one line per character, exactly `id = voiceId`, and nothing else. "
            + "Use each voice at most once. If none fits, leave that character out.");
        request.AppendLine();
        request.AppendLine("Voices:");

        foreach (var voice in offered)
        {
            request.AppendLine(voice.Description is { Length: > 0 } description
                ? $"  {voice.Id} — {voice.Label}. {description}"
                : $"  {voice.Id} — {voice.Label}");
        }

        request.AppendLine();
        request.AppendLine("Characters:");

        foreach (var slot in slots)
        {
            var gender = slot.Hint.Gender == VoiceGender.Unspecified
                ? string.Empty
                : $"{slot.Hint.Gender.ToString().ToLowerInvariant()} voice. ";

            request.AppendLine($"  {slot.Id} — {gender}{slot.Hint.Description}");
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

            maxOutputTokens: CastingTokens,

            // Cold (#98).
            sampling: LlmSampling.VoiceCasting).ConfigureAwait(false);

        if (answer is null)
        {
            return chosen;
        }

        var bySlot = slots.ToDictionary(slot => slot.Id, StringComparer.Ordinal);
        var byId = new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var voice in offered)
        {
            byId.TryAdd(voice.Id, voice);
        }

        foreach (var line in answer.Split('\n'))
        {
            var parts = line.Split('=', 2);

            if (parts.Length != 2)
            {
                continue;
            }

            var slotId = parts[0].Trim().Trim('`', '*', '-', ' ');
            var voiceId = parts[1].Trim().Trim('`', '*', ' ');

            // Both halves checked against what was actually asked and offered.
            if (!bySlot.TryGetValue(slotId, out var slot)
                || !byId.TryGetValue(voiceId, out var voice)
                || chosen.ContainsValue(voice.Id))
            {
                continue;
            }

            if (!slot.Hint.Admits(voice.Gender))
            {
                logger?.LogInformation(
                    "Not pairing {Slot} to {Voice}: the voice must be {Gender}", slotId, voice.Id, slot.Hint.Gender);

                continue;
            }

            chosen[slotId] = voice.Id;
        }

        return chosen;
    }
}
