using Avalonia.Automation;
using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Tests.Stories;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Stories page draws each story's average stars, filters and sorts by them, and lets the Commander rate a picked story.</summary>
[Trait("Category", "Integration")]
public class TheStoriesPageShowsAndFiltersRatingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 20, 0, 0, TimeSpan.Zero);

    private const string Averages = """
        {"format":1,"stories":{
          "story-0":{"average":2.2,"count":3},
          "story-1":{"average":4.6,"count":12},
          "story-2":{"average":3.9,"count":8}}}
        """;

    private sealed record Page(Window Window, PanelView Panel, RatingsWorker Worker, StoryStore Stories);

    private static Page Open(bool ratingsOn = true)
    {
        var paths = new AppPaths(TempFolders.Create("d47-story-ratings"));
        paths.EnsureCreated();

        var cards = Enumerable.Range(0, 4)
            .Select(i => StoryFixture.Story with { Id = $"story-{i}", Number = i, Title = $"Story {i}" })
            .ToList();
        var catalog = new StoryCatalog(cards, () => [.. cards.Select(card => StoryFixture.Secret with { Id = card.Id })]);

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), NullLogger<StoryStore>.Instance);
        stories.Save("F1", new Story { Id = "story-1", Title = "Story 1", PublicLayer = "-", State = StoryState.Finished, Rating = 4 });

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(
            stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var worker = new RatingsWorker { List = Averages };
        var client = worker.Client(stories, ratingsOn);
        client.Open().GetAwaiter().GetResult();

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director, StoryFilters: new StoryFilterMemory(new ViewStateStore(paths, new DiskFileSystem(), NullLogger<ViewStateStore>.Instance)),
            Ratings: client);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(new NavCrumb(StoriesView.RootKey, "Stories"));
        Dispatcher.UIThread.RunJobs();

        return new Page(window, panel, worker, stories);
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    private static List<string> Titles(PanelView panel) =>
        [.. Drawn(panel).Where(text => text.StartsWith("Story ", StringComparison.OrdinalIgnoreCase) && text.Length < 10).Select(text => text.ToUpperInvariant())];

    private static T Named<T>(PanelView panel, string name)
        where T : Control =>
        panel.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetName(control) == name);

    private static void Read(Page page, string id)
    {
        page.Panel.Nav.Drill(new NavCrumb(StoriesView.ReadPrefix + id, id) { Level = StoriesView.ReadPrefix });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Light)]
    public void EachCardShowsItsStarsAndCountAndTheBarHasRatingAndSort(string theme)
    {
        using var look = AppLook.Put(theme, null);

        var page = Open();
        var drawn = Drawn(page.Panel);

        Assert.Equal(["STORY 0", "STORY 1", "STORY 2", "STORY 3"], Titles(page.Panel));
        Assert.Contains("(12)", drawn);
        Assert.Contains(StarRating.Unrated, drawn);
        Assert.True(Named<Stepper>(page.Panel, "Rating").IsEffectivelyVisible);
        Assert.True(Named<Segment>(page.Panel, "Sort").IsEffectivelyVisible);
        Assert.Equal(4.5, page.Panel.GetVisualDescendants().OfType<StarRating>().Select(stars => stars.Value).Max());
        Save(page.Window, $"stories-ratings-cards-{theme}.png");

        page.Window.Close();
    }

    [AvaloniaFact]
    public void FourStarsAndUpListsOnlyStoriesDrawnWithFourOrMoreAndCountsThem()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open();

        Named<Stepper>(page.Panel, "Rating").GetVisualDescendants().OfType<RepeatButton>().Last()
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["STORY 1", "STORY 2"], Titles(page.Panel));
        Assert.Contains("2 of 4 stories.", Drawn(page.Panel));

        page.Window.Close();
    }

    [AvaloniaFact]
    public void HighestRatedPutsTheBestFirstAndTheUnratedLast()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open();

        Named<Segment>(page.Panel, "Sort").GetVisualDescendants().OfType<RadioButton>().ElementAt(1).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["STORY 1", "STORY 2", "STORY 0", "STORY 3"], Titles(page.Panel));

        page.Window.Close();
    }

    [AvaloniaFact]
    public void WithRatingsOffNoStarOrRatingChoiceIsDrawn()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(ratingsOn: false);

        Assert.Empty(page.Panel.GetVisualDescendants().OfType<StarRating>());
        Assert.False(Named<Segment>(page.Panel, "Sort").IsEffectivelyVisible);
        Assert.DoesNotContain(StarRating.Unrated, Drawn(page.Panel));

        Read(page, "story-1");

        Assert.Empty(page.Panel.GetVisualDescendants().OfType<StarRating>());
        Assert.DoesNotContain("Your rating", Drawn(page.Panel));

        page.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Light)]
    public void APickedStorysPageShowsYourRating(string theme)
    {
        using var look = AppLook.Put(theme, null);

        var page = Open();

        Read(page, "story-1");

        var mine = page.Panel.GetVisualDescendants().OfType<StarRating>().Single(stars => stars.IsInteractive);
        Assert.Contains("Your rating", Drawn(page.Panel));
        Assert.Equal(4, mine.Value);
        Assert.Equal("Your rating, 4 of 5 stars", AutomationProperties.GetName(mine));
        Save(page.Window, $"stories-ratings-reading-{theme}.png");

        page.Window.Close();
    }

    [AvaloniaFact]
    public void AStoryNotPickedSaysToPickItFirst()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open();

        Read(page, "story-3");

        Assert.Contains("Pick this story to rate it.", Drawn(page.Panel));
        Assert.Contains(StarRating.Unrated, Drawn(page.Panel));
        Assert.DoesNotContain(page.Panel.GetVisualDescendants().OfType<StarRating>(), stars => stars.IsInteractive && stars.IsEffectivelyVisible);
        Save(page.Window, "stories-ratings-unrated-elite.png");

        page.Window.Close();
    }

    [AvaloniaFact]
    public void TheKeysChangeAndClearTheCommandersVote()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open();

        Read(page, "story-1");

        var mine = page.Panel.GetVisualDescendants().OfType<StarRating>().Single(stars => stars.IsInteractive);
        mine.Focus();
        page.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Settle(page, () => page.Stories.Find("F1", "story-1") is { Rating: 5, RatingPending: false });

        Assert.Equal("""{"stars":5}""", page.Worker.Requests[^1].Body);

        page.Window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Settle(page, () => page.Stories.Find("F1", "story-1") is { Rating: null, RatingPending: false });

        Assert.Equal("DELETE", page.Worker.Requests[^1].Method);
        Assert.Equal("Your rating, not rated", AutomationProperties.GetName(mine));

        page.Window.Close();
    }

    private static void Settle(Page page, Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }
}
