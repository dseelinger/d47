using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Stories page narrows its list by level and length, says how many it hides, and remembers the filter.</summary>
public class TheStoriesPageFiltersItsListTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 20, 0, 0, TimeSpan.Zero);

    private static readonly (string Level, string Length)[] Shapes =
    [
        ("new", "3-days"), ("new", "1-week"), ("new", "1-month"), ("new", "1-year"),
        ("midrange", "1-week"), ("midrange", "2-weeks"), ("midrange", "3-months"), ("midrange", "1-year"),
        ("endgame", "3-days"), ("endgame", "1-month"), ("endgame", "6-months"),
    ];

    private sealed record Page(Window Window, PanelView Panel, ViewStateStore Store);

    private static Page Open(AppPaths paths, IReadOnlyList<(string Level, string Length)>? shapes = null)
    {
        var cards = (shapes ?? Shapes)
            .Select((shape, i) => StoryFixture.Story with
            {
                Id = $"story-{i}",
                Number = i,
                Title = $"Story {i}",
                Level = shape.Level,
                Length = shape.Length,
            })
            .ToList();

        var catalog = new StoryCatalog(cards, () => [.. cards.Select(card => StoryFixture.Secret with { Id = card.Id })]);

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), NullLogger<StoryStore>.Instance);
        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(
            stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var store = new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance);
        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director, StoryFilters: new StoryFilterMemory(store));

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(new NavCrumb(StoriesView.RootKey, "Stories"));
        Dispatcher.UIThread.RunJobs();

        return new Page(window, panel, store);
    }

    private static AppPaths NewPaths()
    {
        var paths = new AppPaths(TempFolders.Create("d47-story-filter"));
        paths.EnsureCreated();
        return paths;
    }

    private static void Press(PanelView panel, int segment, int option)
    {
        panel.GetVisualDescendants().OfType<Segment>().ElementAt(segment).GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    private static int Rows(PanelView panel) =>
        Drawn(panel).Count(text => text.StartsWith("Story ", StringComparison.OrdinalIgnoreCase) && text.Length < 10);

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, name), new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void ALevelNarrowsTheListAndTheCountSaysSo()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        Assert.Equal(11, Rows(page.Panel));
        Assert.DoesNotContain(Drawn(page.Panel), text => text.EndsWith(" stories.", StringComparison.Ordinal));

        Press(page.Panel, 0, 3);

        Assert.Equal(3, Rows(page.Panel));
        Assert.Contains("3 of 11 stories.", Drawn(page.Panel));
        Assert.Equal("endgame", page.Store.Load().StoryFilter?.Level);
        Assert.DoesNotContain(page.Panel.GetVisualDescendants().OfType<Stepper>(), stepper => stepper.IsEffectivelyVisible);
        Save(page.Window, "stories-filter-level.png");

        page.Window.Close();
    }

    [AvaloniaFact]
    public void ALengthComparisonShowsTheSevenLengthsAndNarrowsTheList()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        Press(page.Panel, 1, 1);

        var stepper = page.Panel.GetVisualDescendants().OfType<Stepper>().Single();
        Assert.True(stepper.IsEffectivelyVisible);
        Assert.Equal(7, stepper.ItemsSource.Count);
        Assert.Equal("1 month", stepper.SelectedItem);
        Assert.Equal(6, Rows(page.Panel));
        Assert.Contains("6 of 11 stories.", Drawn(page.Panel));

        Press(page.Panel, 0, 3);

        Assert.Equal(2, Rows(page.Panel));
        Save(page.Window, "stories-filter-level-and-length.png");

        page.Window.Close();
    }

    [AvaloniaFact]
    public void AFilterThatHidesEveryStoryOffersToBeCleared()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths(), [("new", "1-year"), ("midrange", "1-year")]);

        Press(page.Panel, 0, 3);

        Assert.Equal(0, Rows(page.Panel));
        Assert.Contains("No stories match these filters.", Drawn(page.Panel));
        Save(page.Window, "stories-filter-none.png");

        page.Panel.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Clear filters")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, Rows(page.Panel));
        Assert.DoesNotContain("No stories match these filters.", Drawn(page.Panel));
        Assert.Null(page.Store.Load().StoryFilter);

        page.Window.Close();
    }

    [AvaloniaFact]
    public void TheFilterIsStillSetWhenTheAppOpensAgain()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var paths = NewPaths();
        var first = Open(paths);

        Press(first.Panel, 0, 3);
        Press(first.Panel, 1, 3);
        first.Window.Close();

        var second = Open(paths);

        Assert.Equal(new StoryFilter("endgame", StoryLengthCompare.AtMost, "1-month"), second.Store.Load().StoryFilter);
        Assert.Equal(2, Rows(second.Panel));
        Assert.Contains("2 of 11 stories.", Drawn(second.Panel));

        second.Window.Close();
    }
}
