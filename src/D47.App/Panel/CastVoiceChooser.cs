using D47.App.Controls;
using D47.Core.Audio;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>What the panel may read and do about a story character's voice. Panel controls only; no tool reaches it.</summary>
public sealed record CastVoiceSurface(
    Func<string, StoryCastMember?> Member,
    Action<string, string?, string?, string?> Choose,
    Func<string, CancellationToken, Task<VoiceCatalogue>> Voices,
    Action<string, PinnedVoice> PlaySample,
    Func<string, string?> Failure);

/// <summary>Change voice: a provider, then one of the voices it lists, for one story character.</summary>
public static class CastVoiceChooser
{
    /// <summary>The words a row and a message show for the voice a member speaks in.</summary>
    public static string Describe(StoryCastMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var provider = TtsProviderCatalog.Selected(member.Speaks.ProviderId);
        var voice = member.SpeaksName
                    ?? (provider.VoiceIdsAreOpaque ? $"a {provider.Name} voice" : member.Speaks.VoiceId);

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
                DefaultDisplay = $"{TtsProviderCatalog.Selected(member.Pinned.ProviderId).Name} · {member.Pinned.VoiceId}",
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

        if (pinnedHere && !ids.Contains(member.Pinned.VoiceId, StringComparer.OrdinalIgnoreCase))
        {
            ids.Insert(0, member.Pinned.VoiceId);
        }

        prompts.Pick(
            $"story.voice.{member.Key}.voice",
            "Voice",
            new PickerRequest
            {
                Prompt = $"{member.Shown.Name}'s {provider.Name} voice",
                Help = "Every voice the provider lists, of either gender.",
                Choices = ids,
                Describe = id =>
                {
                    var label = listed.LabelFor(id, provider);
                    return pinnedHere && string.Equals(id, member.Pinned.VoiceId, StringComparison.OrdinalIgnoreCase)
                        ? $"{label} (the story's voice)"
                        : label;
                },
                Current = string.Equals(member.Speaks.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) ? member.Speaks.VoiceId : null,
                DefaultDisplay = $"{TtsProviderCatalog.Selected(member.Pinned.ProviderId).Name} · {member.Pinned.VoiceId}",
                WhyEmpty = listed.WhyEmpty(provider.Name),
                Audition = new PickerAudition
                {
                    Play = (id, _) =>
                    {
                        surface.PlaySample(member.Shown.Name, member.Pinned with { ProviderId = providerId, VoiceId = id });
                        return Task.CompletedTask;
                    },
                    Cost = provider.Billed
                        ? $"A sample is spoken by {provider.Name} and billed like any line."
                        : "A sample costs nothing.",
                },
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
}
