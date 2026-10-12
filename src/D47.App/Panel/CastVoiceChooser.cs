using D47.App.Controls;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>What the panel may read and do about a story character's voice. Panel controls only; no tool reaches it.</summary>
public sealed record CastVoiceSurface(
    Func<string, StoryCastMember?> Member,
    Action<string, string?, string?, string?> Choose,
    Func<string, CancellationToken, Task<VoiceCatalogue>> Voices,
    Action<string, PinnedVoice> PlaySample,
    Func<string, string?> Failure,
    Func<D47Settings>? Settings = null,
    Func<string, bool>? KeyStored = null,
    Func<string, string, CancellationToken, Task>? Preview = null);

/// <summary>Change voice: a provider, then one of the voices it lists, for one story character.</summary>
public static class CastVoiceChooser
{
    private const string CrewVoice = "the crew voice";

    private static string DefaultDisplay(StoryCastMember member) =>
        $"{TtsProviderCatalog.Selected(member.Pinned.ProviderId).Name} · {(member.Pinned.IsCrewVoice ? CrewVoice : member.Pinned.VoiceId)}";

    /// <summary>The words a row and a message show for the voice a member speaks in.</summary>
    public static string Describe(StoryCastMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var provider = TtsProviderCatalog.Selected(member.Speaks.ProviderId);
        var voice = member.SpeaksName
                    ?? (member.Speaks.IsCrewVoice ? CrewVoice
                        : provider.VoiceIdsAreOpaque ? $"a {provider.Name} voice" : member.Speaks.VoiceId);

        return member.Speaks.Key is null
            ? $"{provider.Name} · {voice} (the story's voice)"
            : $"{provider.Name} · {voice}";
    }

    /// <summary>Opens the provider picker, then the voice picker, and calls <paramref name="done"/> once a choice is stored.</summary>
    public static void Open(PanelPrompts prompts, CastVoiceSurface surface, string key, Action done)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(surface);

        if (surface.Member(key) is not { } member)
        {
            return;
        }

        var providers = TtsProviderCatalog.All.Where(provider => provider.Speaks).ToList();

        prompts.Pick(
            $"story.voice.{key}.provider",
            "Provider",
            new PickerRequest
            {
                Prompt = $"Who speaks for {member.Shown.Name}",
                Help = "Any provider, and next any voice it lists. The story's text is not rewritten to match the voice.",
                Choices = [.. providers.Select(provider => provider.Id)],
                Describe = id => TtsProviderCatalog.Selected(id) is var provider && provider.Id == member.Pinned.ProviderId
                    ? $"{provider.Name} (the story's provider)"
                    : TtsProviderCatalog.Selected(id).Name,
                Current = member.Speaks.ProviderId,
                DefaultDisplay = DefaultDisplay(member),
                Status = id => surface.KeyStored is { } stored
                    ? ProviderStatus.Of(TtsProviderCatalog.Selected(id), stored)
                    : null,
            },
            result =>
            {
                if (result.Value is null)
                {
                    surface.Choose(key, null, null, null);
                    done();
                    return;
                }

                _ = PickVoiceAsync(prompts, surface, member, result.Value, done);
            });
    }

    private static async Task PickVoiceAsync(PanelPrompts prompts, CastVoiceSurface surface, StoryCastMember member, string providerId, Action done)
    {
        var provider = TtsProviderCatalog.Selected(providerId);
        VoiceCatalogue listed;

        try
        {
            listed = await Task.Run(() => surface.Voices(providerId, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is TtsException or HttpRequestException or IOException)
        {
            listed = VoiceCatalogue.Unreachable(ex.Message);
        }

        var pinnedHere = string.Equals(providerId, member.Pinned.ProviderId, StringComparison.OrdinalIgnoreCase);
        var ids = listed.Voices.Select(voice => voice.Id).ToList();

        if (pinnedHere && !member.Pinned.IsCrewVoice && !ids.Contains(member.Pinned.VoiceId, StringComparer.OrdinalIgnoreCase))
        {
            ids.Insert(0, member.Pinned.VoiceId);
        }

        prompts.Pick(
            $"story.voice.{member.Key}.voice",
            "Voice",
            new PickerRequest
            {
                Prompt = $"{member.Shown.Name}'s {provider.Name} voice",
                Choices = ids,
                Describe = id =>
                {
                    var label = listed.LabelFor(id, provider);
                    return pinnedHere && string.Equals(id, member.Pinned.VoiceId, StringComparison.OrdinalIgnoreCase)
                        ? $"{label} (the story's voice)"
                        : label;
                },
                Current = string.Equals(member.Speaks.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) ? member.Speaks.VoiceId : null,
                DefaultDisplay = DefaultDisplay(member),
                WhyEmpty = listed.WhyEmpty(provider.Name),
                Facet = SpeechCapability.GenderFacet(listed),
                Sorts = [PickerSort.Name],
                Audition = AuditionOf(surface, member, provider, providerId, listed),
            },
            result =>
            {
                if (result.Value is null
                    || (pinnedHere && string.Equals(result.Value, member.Pinned.VoiceId, StringComparison.OrdinalIgnoreCase)))
                {
                    surface.Choose(member.Key, null, null, null);
                }
                else
                {
                    var name = listed.Voices.FirstOrDefault(voice => string.Equals(voice.Id, result.Value, StringComparison.OrdinalIgnoreCase))?.Name;
                    surface.Choose(member.Key, providerId, result.Value, name);
                }

                done();
            });
    }

    private static PickerAudition AuditionOf(
        CastVoiceSurface surface, StoryCastMember member, TtsProviderInfo provider, string providerId, VoiceCatalogue listed)
    {
        var settings = surface.Settings?.Invoke();
        var hasKey = provider.KeySecretName is not { } secret || surface.KeyStored?.Invoke(secret) != false;

        return new PickerAudition
        {
            Play = (id, _) =>
            {
                surface.PlaySample(member.Shown.Name, member.Pinned with { ProviderId = providerId, VoiceId = id });
                return Task.CompletedTask;
            },
            Preview = surface.Preview is { } preview ? (id, token) => preview(providerId, id, token) : null,
            HasPreview = listed.HasPreview,
            Cost = settings is null ? "A sample costs nothing." : SpeechCapability.AuditionCost(settings, providerId),
            LineCost = settings is null ? null : SpeechCapability.LineCost(settings, providerId),
            Unavailable = SpeechCapability.AuditionUnavailable(providerId, hasKey),
        };
    }
}
