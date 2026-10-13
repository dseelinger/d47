using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Story so far is on the current stock story's page and on an active short story's page, and pressing it asks for the
/// recap the phrase does: the stock story's, or that short story's by its key.
/// </summary>
public sealed class StorySoFarIsOnBothStoryPagesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private const string Label = "Story so far";

    private static (PanelView Panel, Window Window, List<string?> Asked) Open(params Adventure[] adventures)
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-story-so-far"));
        var surface = AdventureFixture.Surface(paths);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), TestSurface.FilesFor(paths), NullLogger<StoryStore>.Instance);
        var asked = new List<string?>();

        foreach (var adventure in adventures)
        {
            Assert.Null(surface.Book.Write("F1", adventure));
        }

        if (adventures.FirstOrDefault(adventure => adventure.StoryId is not null) is { } chapter)
        {
            stories.Save("F1", new Story
            {
                Id = StoryFixture.Story.Id,
                Title = StoryFixture.Story.Title,
                PublicLayer = StoryFixture.Story.Describe(),
                Chapters = [chapter.Key],
                PickedAt = Now.AddHours(-1),
            });
        }

        var director = new StoryDirector(
            stories, surface.Book, () => StoryFixture.Catalog, surface.Generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface with
        {
            Stories = director,
            Recap = key =>
            {
                asked.Add(key);
                return null;
            },
        });

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        return (panel, window, asked);
    }

    private static List<Button> Buttons(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Where(button => button.Content as string == Label)];

    private static void Go(PanelView panel, NavCrumb crumb)
    {
        panel.Nav.GoTo(crumb);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AShortStoryUnderWayRecapsItselfByItsKey()
    {
        var (panel, window, asked) = Open(AdventuresTabTests.Story(Now.AddHours(-1), AdventureSource.Generated));

        Go(panel, new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Assert.Single(Buttons(panel)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["the-lantern-route"], asked);
        window.Close();
    }

    [AvaloniaFact]
    public void ADraftShortStoryHasNoRecap()
    {
        var (panel, window, _) = Open(AdventuresTabTests.Story(null, AdventureSource.Generated));

        Go(panel, new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Assert.Empty(Buttons(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void TheStockStorysOwnPageRecapsTheStoryUnderWay()
    {
        var chapter = AdventuresTabTests.Story(Now.AddHours(-1), AdventureSource.Generated) with { StoryId = StoryFixture.Story.Id };
        var (panel, window, asked) = Open(chapter);

        Go(panel, new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Story.Id, StoryFixture.Story.Title));
        Assert.Single(Buttons(panel)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([null], asked);

        Go(panel, new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Other.Id, StoryFixture.Other.Title));
        Assert.Empty(Buttons(panel));

        Go(panel, new NavCrumb(AdventuresPage.ReadPrefix + chapter.Key, chapter.Name));
        Assert.Empty(Buttons(panel));

        window.Close();
    }
}
