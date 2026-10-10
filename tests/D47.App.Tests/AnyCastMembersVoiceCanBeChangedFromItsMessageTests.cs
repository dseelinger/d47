using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The card's reading page lists the story's primary cast, and no one else, each with its voice, Play sample and Change
/// voice; a message from any cast member, primary or not, carries Change voice (#737).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AnyCastMembersVoiceCanBeChangedFromItsMessageTests
{
    private static readonly StoryCatalog Catalog = new(
        [StoryFixture.Story with
        {
            Blurb = "Juno and Harrow know a song they should not.",
            CastPictures = [$"{StoryFixture.Story.Id}.juno", $"{StoryFixture.Story.Id}.harrow"],
        }],
        () =>
        [
            StoryFixture.Secret with
            {
                Cast =
                [
                    new StorySpeaker { Id = "juno", Name = "Juno", Who = "A stowaway.", Provider = StorySpeaker.Kokoro, Voice = "af_river", Primary = true },
                    new StorySpeaker { Id = "harrow", Name = "Harrow", Who = "A broker.", Provider = StorySpeaker.Kokoro, Voice = "bm_george", Primary = true },
                    new StorySpeaker { Id = "teller", Name = "Quill", Who = "A bank teller.", Provider = StorySpeaker.Kokoro, Voice = "am_michael" },
                ],
            },
        ]);

    private static (AdventureSurface Surface, StoryDirector Director, Dictionary<string, StoryVoiceChoice> Choices) Open(AppPaths paths)
    {
        var surface = AdventureFixture.Surface(paths);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), new DiskFileSystem(), NullLogger<StoryStore>.Instance);
        var choices = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal);
        var director = new StoryDirector(
            stories, surface.Book, () => Catalog, surface.Generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Choices = () => choices,
        };

        var voices = new CastVoiceSurface(
            director.CastMember,
            (key, provider, voice, name) =>
            {
                if (provider is null || voice is null)
                {
                    choices.Remove(key);
                }
                else
                {
                    choices[key] = new StoryVoiceChoice(provider, voice) { VoiceName = name };
                }
            },
            (_, _) => Task.FromResult(VoiceCatalogue.Silent),
            (_, _) => { },
            _ => null);

        return (surface with { Stories = director, CastVoices = voices, Messages = null }, director, choices);
    }

    private static List<string> Drawn(Control control) =>
        [.. control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static List<string> Buttons(Control control) =>
        [.. control.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string ?? string.Empty)];

    private static Window Show(Control page, string capture)
    {
        var window = new Window { Content = page, Width = 640, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture(capture);
        }

        return window;
    }

    [AvaloniaFact]
    public void TheReadingPageListsTheTwoPrimaryMembersAndNotTheOther()
    {
        var paths = new AppPaths(TempFolders.Create("d47-cast-voices"));
        paths.EnsureCreated();
        var (surface, director, choices) = Open(paths);
        choices[$"{StoryFixture.Story.Id}.harrow"] = new StoryVoiceChoice(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb") { VoiceName = "George" };

        var view = new StoriesView(surface, director, new PanelNavigator(), new PanelPrompts(new PanelNavigator(), new Avalonia.Controls.Panel()));
        var page = view.Build(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Story.Id, StoryFixture.Story.Title))!;
        using var window = new DisposableWindow(Show(page, "story-cast-section.png"));

        var drawn = Drawn(page);

        Assert.Contains("Juno", drawn);
        Assert.Contains("Harrow", drawn);
        Assert.DoesNotContain(drawn, text => text.Contains("Quill", StringComparison.Ordinal));
        Assert.Contains("Kokoro · af_river (the story's voice)", drawn);
        Assert.Contains("ElevenLabs · George", drawn);
        Assert.DoesNotContain(drawn, text => text.Contains("JBFqnCBsd6RMkjVDRZzb", StringComparison.Ordinal));
        Assert.Equal(2, Buttons(page).Count(label => label == "Change voice"));
        Assert.Equal(2, Buttons(page).Count(label => label == "Play sample"));
    }

    [AvaloniaFact]
    public void AMessageFromAMemberThatIsNotPrimaryHasChangeVoice()
    {
        var paths = new AppPaths(TempFolders.Create("d47-cast-voices"));
        paths.EnsureCreated();
        var (surface, _, _) = Open(paths);

        var messages = new MessageStore(Path.Combine(paths.Data, "messages.json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
        var fromQuill = messages.Post("Quill", "The Test Story", "Your account is overdrawn.", DateTimeOffset.Now, cast: $"{StoryFixture.Story.Id}.teller");
        var fromShip = messages.Post("archivist", "The Test Story", "A song.", DateTimeOffset.Now);

        var view = new MessagesView(messages, new PanelNavigator(), surface, new PanelPrompts(new PanelNavigator(), new Avalonia.Controls.Panel()));

        var quill = view.Build(new NavCrumb(MessagesView.ReadPrefix + fromQuill.Key, "The Test Story"))!;
        using (new DisposableWindow(Show(quill, "message-change-voice.png")))
        {
            Assert.Contains("Change voice", Buttons(quill));
        }

        var ship = view.Build(new NavCrumb(MessagesView.ReadPrefix + fromShip.Key, "The Test Story"))!;
        Assert.DoesNotContain("Change voice", Buttons(ship));
    }

    [AvaloniaFact]
    public void ChangeVoiceStaysBesideThePictureButtonsWhenThePictureIsShownAgain()
    {
        var paths = new AppPaths(TempFolders.Create("d47-cast-voices"));
        paths.EnsureCreated();
        var changeVoice = new Button { Content = "Change voice" };

        var chooser = new PictureChooser(new SpeakerPictures(new DiskFileSystem(), paths), "the-test-story.juno", 240, null, changeVoice);
        var second = new PictureChooser(new SpeakerPictures(new DiskFileSystem(), paths), "the-test-story.juno", 240, null, changeVoice);

        Assert.Same(second.Children.OfType<StackPanel>().Single(), changeVoice.Parent);
        Assert.DoesNotContain(changeVoice, chooser.Children.OfType<StackPanel>().Single().Children);
    }

    private sealed class DisposableWindow(Window window) : IDisposable
    {
        public void Dispose() => window.Close();
    }
}
