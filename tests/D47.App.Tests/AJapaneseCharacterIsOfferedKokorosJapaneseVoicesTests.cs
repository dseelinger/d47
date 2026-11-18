using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Speech;
using D47.Core.Stories;
using D47.Tts;
using Xunit;

namespace D47.App.Tests;

/// <summary>Change voice offers Kokoro's five Japanese voices to a Japanese-speaking character and its English voices to anyone else (#2534).</summary>
[Trait("Category", "Integration")]
public sealed class AJapaneseCharacterIsOfferedKokorosJapaneseVoicesTests
{
    private static StoryCastMember Member(string? language) => new(
        "story",
        "The Story",
        new StorySpeakerShown("ren", "Ren", TtsProviderCatalog.EdgeId, "ja-JP-NanamiNeural", "story.ren") { Pinned = true },
        new PinnedVoice(TtsProviderCatalog.EdgeId, "ja-JP-NanamiNeural"),
        new PinnedVoice(TtsProviderCatalog.EdgeId, "ja-JP-NanamiNeural"))
    {
        Language = language,
    };

    private static List<string> Choices(PickerPage page) =>
        [.. page.GetVisualDescendants().OfType<ListBox>().Single().Items.Cast<PickerChoice>().Select(choice => choice.Value!)];

    private static async Task<(List<string> Providers, List<string> Voices)> OfferedAsync(string? language)
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Transcript, new NavCrumb("transcript", "Transcript"));
        var prompts = new PanelPrompts(nav, new Avalonia.Controls.Panel());
        var member = Member(language);

        var surface = new CastVoiceSurface(
            _ => member,
            (_, _, _, _) => { },
            (provider, spoken, _) => Task.FromResult(provider == TtsProviderCatalog.KokoroId
                ? KokoroTtsProvider.Catalogue(installed: true, spoken)
                : VoiceCatalogue.Silent),
            (_, _) => { },
            _ => null);

        CastVoiceChooser.Open(prompts, surface, member.Key, () => { });
        var depth = nav.Trail.Count;

        var providerPage = (PickerPage)prompts.Build(nav.Trail[^1])!;
        var window = new Window { Content = providerPage, Width = 640, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var providers = Choices(providerPage);
        var list = providerPage.GetVisualDescendants().OfType<ListBox>().Single();
        list.SelectedIndex = providers.IndexOf(TtsProviderCatalog.KokoroId);
        providerPage.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "AcceptButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        for (var wait = 0; wait < 100 && nav.Trail.Count <= depth; wait++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        var voicePage = (PickerPage)prompts.Build(nav.Trail[^1])!;
        window.Content = voicePage;
        Dispatcher.UIThread.RunJobs();

        var voices = Choices(voicePage);
        window.Close();

        return (providers, voices);
    }

    [AvaloniaFact]
    public async Task AJapaneseSpeakerSeesKokoroAndItsFiveJapaneseVoicesOnly()
    {
        var (providers, voices) = await OfferedAsync(SpokenLanguage.Japanese);

        Assert.Contains(TtsProviderCatalog.KokoroId, providers);
        Assert.DoesNotContain(TtsProviderCatalog.ChatterboxId, providers);
        Assert.Equal(KokoroAssets.JapaneseVoiceIds.Order(StringComparer.Ordinal), voices.Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public async Task AnEnglishSpeakerSeesKokorosTwentyEightEnglishVoicesOnly()
    {
        var (_, voices) = await OfferedAsync(null);

        Assert.Equal(KokoroAssets.VoiceIds.Order(StringComparer.Ordinal), voices.Order(StringComparer.Ordinal));
    }
}
