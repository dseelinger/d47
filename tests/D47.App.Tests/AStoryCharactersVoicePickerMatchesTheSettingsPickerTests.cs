using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests;

/// <summary>A story character's voice picker offers the gender filter, the free sample, the Settings wording and the key status (#1069).</summary>
[Trait("Category", "Integration")]
public sealed class AStoryCharactersVoicePickerMatchesTheSettingsPickerTests
{
    private static readonly VoiceCatalogue Listed = VoiceCatalogue.Of(
    [
        new VoiceInfo("a", "Ava", "en-US", "Female") { PreviewUrl = "https://example.test/a.mp3" },
        new VoiceInfo("b", "Ben", "en-US", "Male"),
    ]);

    private static StoryCastMember Member() => new(
        "story",
        "The Story",
        new StorySpeakerShown("juno", "Juno", TtsProviderCatalog.EdgeId, "x", "story.juno"),
        new PinnedVoice(TtsProviderCatalog.EdgeId, "x"),
        new PinnedVoice(TtsProviderCatalog.EdgeId, "x"));

    private static (PanelPrompts Prompts, PanelNavigator Nav, CastVoiceSurface Surface) Open(Func<string, bool> keyStored)
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Transcript, new NavCrumb("transcript", "Transcript"));
        var prompts = new PanelPrompts(nav, new Avalonia.Controls.Panel());
        var member = Member();

        var surface = new CastVoiceSurface(
            _ => member,
            (_, _, _, _) => { },
            (_, _) => Task.FromResult(Listed),
            (_, _) => { },
            _ => null,
            () => D47Settings.Defaults,
            keyStored,
            (_, _, _) => Task.CompletedTask);

        CastVoiceChooser.Open(prompts, surface, member.Key, () => { });

        return (prompts, nav, surface);
    }

    private static PickerPage Page(PanelPrompts prompts, PanelNavigator nav) =>
        (PickerPage)prompts.Build(nav.Trail[^1])!;

    private static void Choose(PickerPage page, string value)
    {
        var list = page.GetVisualDescendants().OfType<ListBox>().Single();
        var index = list.Items.Cast<PickerChoice>().ToList().FindIndex(choice => choice.Value == value);
        list.SelectedIndex = index;
        page.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "AcceptButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static async Task<PickerPage> NextPageAsync(PanelPrompts prompts, PanelNavigator nav, int depth)
    {
        for (var wait = 0; wait < 100 && nav.Trail.Count < depth; wait++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        return Page(prompts, nav);
    }

    [AvaloniaFact]
    public async Task ElevenLabsWithoutAKeyIsOfferedWithItsStatusAndRefusesToSpeak()
    {
        var (prompts, nav, _) = Open(_ => false);
        var depth = nav.Trail.Count;

        var providers = Page(prompts, nav);
        var window = new Window { Content = providers, Width = 640, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            providers.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "PAID · NEEDS KEY");

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("cast-voice-providers.png");
        }

        Choose(providers, TtsProviderCatalog.ElevenLabsId);
        var voices = await NextPageAsync(prompts, nav, depth + 1);
        window.Content = voices;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            voices.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "ElevenLabs needs an API key before it will speak.");

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("cast-voice-no-key.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task AVoiceListWithGendersCarriesTheGenderFilterAndTheFreeSample()
    {
        var (prompts, nav, _) = Open(_ => true);
        var depth = nav.Trail.Count;

        var providers = Page(prompts, nav);
        var window = new Window { Content = providers, Width = 640, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Choose(providers, TtsProviderCatalog.ElevenLabsId);
        var voices = await NextPageAsync(prompts, nav, depth + 1);
        window.Content = voices;
        Dispatcher.UIThread.RunJobs();

        Assert.True(voices.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "FacetPanel").IsVisible);

        var choices = voices.GetVisualDescendants().OfType<ListBox>().Single().Items.Cast<PickerChoice>().ToList();
        Assert.True(choices.Single(choice => choice.Value == "a").HasSample);
        Assert.False(choices.Single(choice => choice.Value == "b").HasSample);

        window.Close();
    }
}
