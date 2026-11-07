using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using D47.Core.Listening;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Saving setup puts every voice slot on the voice chosen there.</summary>
public class EverySlotFollowsTheSetupVoiceTests
{
    private static readonly VoiceGroup[] Languageless =
        [VoiceGroup.PeopleYouKnow, VoiceGroup.DirectMessages, VoiceGroup.AnyoneInRange];

    private static SetupChoices Choices(string voice) => new(
        LlmProviderCatalog.NoneId, voice, SttProviderCatalog.LocalId, "F9", null, ListeningCapability.ToggleMode);

    private static D47Settings Migrated(D47Settings settings) => VoiceGroups.Migrated(settings);

    private static (TestSurface Surface, SettingsService Settings) Surface(string voice)
    {
        var surface = TestSurface.For(new MemoryInstall());

        if (TtsProviderCatalog.Selected(voice).KeySecretName is { } secret)
        {
            surface.Secrets.Set(secret, "test-key");
        }

        surface.Settings.Replace("edge pins", s => Migrated(s with { Speech = s.Speech with { Provider = TtsProviderCatalog.ElevenLabsId } }));
        surface.Settings.Replace("edge default", s => s with { Speech = s.Speech with { Provider = TtsProviderCatalog.EdgeId } });
        return (surface, surface.Settings);
    }

    [Theory]
    [InlineData(TtsProviderCatalog.ElevenLabsId)]
    [InlineData(TtsProviderCatalog.KokoroId)]
    [InlineData(TtsProviderCatalog.ChatterboxId)]
    [InlineData(TtsProviderCatalog.CartesiaId)]
    public void EverySlotSpeaksInTheChosenVoice(string voice)
    {
        var (_, settings) = Surface(voice);
        Assert.NotNull(settings.Current.Speech.GroupProviders);

        Assert.Empty(FirstRun.Apply(settings, Choices(voice)));

        Assert.All(VoiceGroups.All, slot => Assert.Equal(voice, VoiceGroups.ProviderFor(settings.Current.Speech, slot.Group)));
    }

    [Fact]
    public void OpenAiIsNotUsedWhereALanguageIsNeeded()
    {
        var (_, settings) = Surface(TtsProviderCatalog.OpenAiId);

        Assert.Empty(FirstRun.Apply(settings, Choices(TtsProviderCatalog.OpenAiId)));

        var speech = settings.Current.Speech;
        Assert.Equal(TtsProviderCatalog.OpenAiId, VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard));
        Assert.Equal(TtsProviderCatalog.OpenAiId, VoiceGroups.ProviderFor(speech, VoiceGroup.Carrier));
        Assert.Equal(TtsProviderCatalog.OpenAiId, VoiceGroups.ProviderFor(speech, VoiceGroup.Npcs));
        Assert.All(Languageless, group => Assert.Equal(TtsProviderCatalog.EdgeId, VoiceGroups.ProviderFor(speech, group)));
    }

    [Fact]
    public void ANullMapBecomesAnEmptyOneThatMigrationLeavesAlone()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Secrets.Set(TtsProviderCatalog.ElevenLabsKeySecretName, "test-key");
        Assert.Null(surface.Settings.Current.Speech.GroupProviders);

        Assert.Empty(FirstRun.Apply(surface.Settings, Choices(TtsProviderCatalog.ElevenLabsId)));

        var saved = surface.Settings.Current;
        Assert.NotNull(saved.Speech.GroupProviders);
        Assert.Same(saved, VoiceGroups.Migrated(saved));
    }

    [Fact]
    public void NoVoiceStillLeavesANonNullMap()
    {
        var surface = TestSurface.For(new MemoryInstall());

        Assert.Empty(FirstRun.Apply(surface.Settings, Choices(TtsProviderCatalog.NoneId)));

        Assert.NotNull(surface.Settings.Current.Speech.GroupProviders);
    }

    [Fact]
    public void TheDraftShowsTheSameSlotsAsTheSave()
    {
        var (_, settings) = Surface(TtsProviderCatalog.ElevenLabsId);

        var draft = FirstRun.Draft(settings, Choices(TtsProviderCatalog.ElevenLabsId));

        Assert.All(VoiceGroups.All, slot => Assert.Equal(TtsProviderCatalog.ElevenLabsId, VoiceGroups.ProviderFor(draft.Speech, slot.Group)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AnEmptyMapSurvivesASaveAndReload()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var settings = FirstRun.EverySlotFollowsTheShip(store.Load());

        store.Save(settings);

        var reloaded = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance).Load();
        Assert.NotNull(reloaded.Speech.GroupProviders);
        Assert.Empty(reloaded.Speech.GroupProviders);
    }
}
