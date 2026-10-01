using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Stories page lists every card by title, genre and blurb, and the running story, a card reads in full with Pick or Switch,
/// and no hidden sentence is drawn anywhere. Captures are saved to <see cref="TestSurface.CaptureDirectory"/>.
/// </summary>
public class TheStoriesPageShowsOnlyThePublicLayerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);

    private sealed record Surface(Window Window, PanelView Panel);

    private static Surface Open(bool running, StoryCatalog? catalog = null)
    {
        var paths = new AppPaths(TempFolders.Create("d47-stories-capture"));
        paths.EnsureCreated();

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), NullLogger<StoryStore>.Instance);
        var card = StoryFixture.Story;

        if (running)
        {
            Assert.Null(book.Write("F1", new Adventure
            {
                Key = "the-first-light",
                Name = "The First Light",
                Source = AdventureSource.Generated,
                StoryId = card.Id,
                Spine = new AdventureSpine { Premise = "A stock voice asks for something." },
                Beats =
                [
                    new AdventureBeat
                    {
                        Title = "The Beacon",
                        Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 13872878396833, System = "IC 2391 Sector MX-T b3-6" },
                        Line = "Scan it.",
                    },
                ],
                AcceptedAt = Now.AddHours(-1),
            }));

            stories.Save("F1", new Story
            {
                Id = card.Id,
                Title = card.Title,
                PublicLayer = card.Describe(),
                Chapters = ["the-first-light"],
                PickedAt = Now.AddHours(-1),
            });
        }

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);

        var director = new StoryDirector(
            stories, book, catalog ?? StoryFixture.Catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Adventures;
        panel.Nav.GoTo(new NavCrumb(StoriesView.RootKey, "Stories"));
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel);
    }

    private static string Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        return path;
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>Whether a text block draws <paramref name="text"/>, in any case, since list rows are drawn in capitals.</summary>
    private static bool Shows(PanelView panel, string text) =>
        Drawn(panel).Any(drawn => string.Equals(drawn, text, StringComparison.OrdinalIgnoreCase));

    private static List<string> Buttons(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string ?? string.Empty)];

    private static void NoHiddenSentence(PanelView panel)
    {
        var drawn = string.Join("\n", Drawn(panel));

        foreach (var secret in StoryFixture.Catalog.Secrets)
        {
            foreach (var (_, field) in secret.Texts().Where(text => text.Text.Length > 0))
            {
                Assert.True(!drawn.Contains(field, StringComparison.OrdinalIgnoreCase), $"The page draws hidden text from {secret.Id}.");
            }
        }
    }

    [AvaloniaFact]
    public void WithNoStoryTheCardsAreListedAndACardOffersPick()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(running: false);
        var panel = surface.Panel;
        Assert.True(Shows(panel, "The Test Story"));
        Assert.True(Shows(panel, "The Other Story"));
        Assert.True(Shows(panel, "Whydunit · Quiet test · Archivist"));
        Assert.True(Shows(panel, StoryFixture.Story.Blurb));
        Assert.False(Shows(panel, StoryFixture.Story.InYourWords));
        NoHiddenSentence(panel);
        Save(surface.Window, "stories-root.png");

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Other.Id, StoryFixture.Other.Title));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Pick", Buttons(panel));
        Assert.True(Shows(panel, "Buddy Love"));
        Assert.True(Shows(panel, "Core: Archivist"));
        Assert.True(Shows(panel, StoryFixture.Other.Blurb));
        Assert.True(Shows(panel, "In your words"));
        Assert.True(Shows(panel, "The beacon"));
        NoHiddenSentence(panel);
        Save(surface.Window, "stories-card.png");

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithNoStoriesThePageSaysSo()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(running: false, StoryFixture.Empty);

        Assert.True(Shows(surface.Panel, "No stories yet."));
        Save(surface.Window, "stories-empty.png");

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithAStoryRunningItLeadsTheListAndAnotherCardOffersSwitch()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(running: true);
        var panel = surface.Panel;

        Assert.True(Shows(panel, "Your story — chapter 1 under way."));
        Assert.Contains("Abandon", Buttons(panel));
        Assert.Contains("Read the chapter", Buttons(panel));
        Save(surface.Window, "stories-running.png");

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Other.Id, StoryFixture.Other.Title));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Switch", Buttons(panel));
        Assert.DoesNotContain("Pick", Buttons(panel));

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Story.Id, StoryFixture.Story.Title));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(panel, "This is your story."));

        surface.Window.Close();
    }
}
