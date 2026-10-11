using D47.Core.Audio;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using Microsoft.Extensions.Logging;
using VoiceGender = D47.Core.Persona.VoiceGender;

namespace D47.Core.Stories;

/// <summary>A voice on the ship's provider for each cast member a story did not pin, chosen as the cores' are.</summary>
public static class StoryCastPairing
{
    /// <summary>The pairings on <paramref name="provider"/>, keyed as <see cref="D47Settings.StoryVoices"/>; null for none.</summary>
    public static IReadOnlyDictionary<string, StoryVoiceChoice>? On(D47Settings settings, string provider)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.StoryVoicePairings
            .FirstOrDefault(pair => string.Equals(pair.Key, provider, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    /// <summary>The settings with <paramref name="added"/> merged into the pairings on <paramref name="provider"/>.</summary>
    public static D47Settings WithPairings(D47Settings settings, string provider, IReadOnlyDictionary<string, StoryVoiceChoice> added)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(added);

        if (added.Count == 0)
        {
            return settings;
        }

        var merged = new Dictionary<string, StoryVoiceChoice>(On(settings, provider) ?? new Dictionary<string, StoryVoiceChoice>(), StringComparer.Ordinal);

        foreach (var (picture, choice) in added)
        {
            merged[picture] = choice;
        }

        var all = settings.StoryVoicePairings
            .Where(pair => !string.Equals(pair.Key, provider, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        all[provider] = merged;

        return settings with { StoryVoicePairings = all };
    }

    /// <summary>The gender of the voice a story wrote a member for, from that voice's own catalogue; Unspecified where it gives none.</summary>
    /// <param name="chatterbox">Chatterbox's voices, whose rows carry their gender.</param>
    public static VoiceGender GenderOf(string provider, string voice, IEnumerable<VoiceInfo> chatterbox)
    {
        ArgumentNullException.ThrowIfNull(chatterbox);

        if (string.Equals(provider, StorySpeaker.Kokoro, StringComparison.OrdinalIgnoreCase))
        {
            // Kokoro ids are <language><f|m>_<name>: af_heart, bm_george.
            return voice.Length > 3 && voice[2] == '_'
                ? char.ToLowerInvariant(voice[1]) switch
                {
                    'f' => VoiceGender.Female,
                    'm' => VoiceGender.Male,
                    _ => VoiceGender.Unspecified,
                }
                : VoiceGender.Unspecified;
        }

        if (string.Equals(provider, StorySpeaker.Chatterbox, StringComparison.OrdinalIgnoreCase))
        {
            return chatterbox.FirstOrDefault(row => string.Equals(row.Id, voice, StringComparison.OrdinalIgnoreCase)) is { } row
                   && VoiceHint.Read(row.Gender) is { } gender
                ? gender
                : VoiceGender.Unspecified;
        }

        return VoiceGender.Unspecified;
    }

    /// <summary>The members of <paramref name="secret"/> the story did not pin, as a Commander of <paramref name="gender"/> meets them.</summary>
    public static IReadOnlyList<(StorySpeaker Member, StorySpeakerShown Shown)> Unpinned(StorySecret secret, string? gender)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return [.. secret.Cast
            .Select(member => (Member: member, Shown: member.Shown(secret.Id, gender)))
            .Where(pair => !pair.Shown.Pinned)];
    }

    /// <summary>
    /// A voice from <paramref name="voices"/> for each unpinned member of <paramref name="secret"/> with no pairing on
    /// <paramref name="provider"/>, keyed as <see cref="D47Settings.StoryVoices"/>. Avoids <paramref name="taken"/> and the
    /// voices this story's members already have on that provider, paired or chosen, until no free voice fits.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, StoryVoiceChoice>> ChooseAsync(
        StorySecret secret,
        string title,
        string? gender,
        string provider,
        D47Settings settings,
        IEnumerable<string> taken,
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyList<VoiceInfo> chatterbox,
        ILlmProvider? llm,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        Random? random = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(voices);

        var paired = On(settings, provider) ?? new Dictionary<string, StoryVoiceChoice>();
        var unpaired = Unpinned(secret, gender)
            .Where(pair => !paired.ContainsKey(pair.Shown.Picture))
            .ToList();

        if (unpaired.Count == 0 || voices.Count == 0)
        {
            return new Dictionary<string, StoryVoiceChoice>();
        }

        var pictures = secret.Cast
            .SelectMany(member => new[] { CommanderGender.Man, CommanderGender.Woman }.Select(asked => member.Shown(secret.Id, asked).Picture))
            .ToHashSet(StringComparer.Ordinal);

        var held = paired.Concat(settings.StoryVoices)
            .Where(pair => pictures.Contains(pair.Key) && string.Equals(pair.Value.Provider, provider, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.Voice);

        // Slot ids of their own: a picture could be read as another slot's id, or lose characters the parser trims.
        var slots = unpaired
            .Select((pair, index) => new VoicePairing.Slot(
                $"cast{index + 1}",
                new VoiceHint($"{pair.Shown.Name}: {pair.Member.Who}", GenderOf(pair.Shown.Provider, pair.Shown.Voice, chatterbox))))
            .ToList();

        var chosen = await VoicePairing.ChooseForAsync(
            voices, slots, taken.Concat(held), llm, model, spend, prices, logger, random, cancellationToken)
            .ConfigureAwait(false);

        var names = voices
            .GroupBy(voice => voice.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

        var pairings = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal);

        for (var index = 0; index < slots.Count; index++)
        {
            if (chosen.GetValueOrDefault(slots[index].Id) is { } voice)
            {
                var shown = unpaired[index].Shown;

                pairings[shown.Picture] = new StoryVoiceChoice(provider, voice)
                {
                    Story = title,
                    Character = shown.Name,
                    VoiceName = names.GetValueOrDefault(voice),
                };
            }
        }

        return pairings;
    }
}
