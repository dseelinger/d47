using Avalonia.Controls;
using D47.Core.Storage;
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

/// <summary>The Catalogue narrows its cards by the sentence of tokens, says how many it hides, and remembers the filter.</summary>
[Trait("Category", "Integration")]
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

    private static (AdventureSurface Surface, ViewStateStore Store) Build(AppPaths paths, IReadOnlyList<(string Level, string Length)>? shapes = null)
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
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), new DiskFileSystem(), NullLogger<StoryStore>.Instance);
        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(
            stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var store = new ViewStateStore(paths, new DiskFileSystem(), NullLogger<ViewStateStore>.Instance);
        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director, StoryFilters: new StoryFilterMemory(store));

        return (surface, store);
    }

    private static Page Open(AppPaths paths, IReadOnlyList<(string Level, string Length)>? shapes = null)
    {
        var (surface, store) = Build(paths, shapes);
        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Page(window, panel, store);
    }

    private static AppPaths NewPaths()
    {
        var paths = new AppPaths(TempFolders.Create("d47-story-filter"));
        paths.EnsureCreated();
        return paths;
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(CatalogueControls.TextOf)];

    private static int Rows(PanelView panel) =>
        Drawn(panel).Count(text => text.StartsWith("Story ", StringComparison.OrdinalIgnoreCase) && text.Length < 10);

    private static List<string> Sentence(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<WrapPanel>().First(wrap => wrap.GetVisualAncestors().OfType<StoriesView>().Any()).Children.Where(child => child.IsVisible).Select(child => child switch
        {
            TextBlock words => words.Text ?? string.Empty,
            _ => child.GetVisualDescendants().OfType<TextBlock>().First().Text ?? string.Empty,
        })];

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void ALevelNarrowsTheListAndTheCountSaysSo()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        Assert.Equal(11, Rows(page.Panel));
        Assert.Contains("11 of 11 stories", Drawn(page.Panel));
        Assert.DoesNotContain(page.Panel.GetVisualDescendants().OfType<Button>(), button => button.Content as string == "Clear filters" && button.IsEffectivelyVisible);

        CatalogueControls.Tick(page.Panel, "Endgame");

        Assert.Equal(3, Rows(page.Panel));
        Assert.Contains("3 of 11 stories", Drawn(page.Panel));
        Assert.Equal(["endgame"], page.Store.Load().StoryFilter?.Levels);
        Assert.Equal("ENDGAME", CatalogueControls.TokenText(page.Panel, "Level"));
        Assert.False(CatalogueControls.Token(page.Panel, "Story length").IsEffectivelyVisible);
        Save(page.Window, "stories-filter-level.png");

        page.Window.Close();
    }

    [AvaloniaFact]
    public void SeveralLevelsListTheCardsOfAnyOfThemAndTheTokenNamesThem()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        CatalogueControls.Tick(page.Panel, "New commander");
        CatalogueControls.Tick(page.Panel, "Endgame");

        Assert.Equal(7, Rows(page.Panel));
        Assert.Equal("NEW COMMANDER AND ENDGAME", CatalogueControls.TokenText(page.Panel, "Level"));
        Assert.Contains(CatalogueFilters.LevelHint, Drawn(page.Panel));

        CatalogueControls.Tick(page.Panel, "Mid-range");

        Assert.Equal("ALL", CatalogueControls.TokenText(page.Panel, "Level"));
        Assert.Equal(11, Rows(page.Panel));
        Assert.Null(page.Store.Load().StoryFilter);

        page.Window.Close();
    }

    [AvaloniaFact]
    public void ALengthComparisonShowsTheSevenLengthsAndNarrowsTheList()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        Assert.False(CatalogueControls.Token(page.Panel, "Story length").IsEffectivelyVisible);

        CatalogueControls.Choose(page.Panel, "Length", 1);

        Assert.True(CatalogueControls.Token(page.Panel, "Story length").IsEffectivelyVisible);
        Assert.Equal("1 MONTH", CatalogueControls.TokenText(page.Panel, "Story length"));
        Assert.Equal(6, Rows(page.Panel));
        Assert.Contains("6 of 11 stories", Drawn(page.Panel));

        CatalogueControls.PressToken(page.Panel, "Story length");

        var options = page.Panel.GetVisualDescendants().OfType<Segment>().Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Story length options");
        Assert.Equal(7, options.ItemsSource.Count);
        Assert.Equal("1 month", options.SelectedItem);

        CatalogueControls.Tick(page.Panel, "Endgame");

        Assert.Equal(2, Rows(page.Panel));
        Save(page.Window, "stories-filter-level-and-length.png");

        page.Window.Close();
    }

    [AvaloniaFact]
    public void OpeningASecondPanelClosesTheFirstAndPickingAnOptionClosesItsPanel()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        CatalogueControls.PressToken(page.Panel, "Level");
        Assert.Contains(page.Panel.GetVisualDescendants().OfType<CheckBox>(), box => box.IsEffectivelyVisible);

        CatalogueControls.PressToken(page.Panel, "Length");

        Assert.DoesNotContain(page.Panel.GetVisualDescendants().OfType<CheckBox>(), box => box.IsEffectivelyVisible);
        Assert.Contains(page.Panel.GetVisualDescendants().OfType<Segment>(), segment => segment.IsEffectivelyVisible);

        CatalogueControls.PressToken(page.Panel, "Length");

        Assert.DoesNotContain(page.Panel.GetVisualDescendants().OfType<Segment>(), segment => segment.IsEffectivelyVisible);

        CatalogueControls.Choose(page.Panel, "Length", 2);

        Assert.DoesNotContain(page.Panel.GetVisualDescendants().OfType<Segment>(), segment => segment.IsEffectivelyVisible);

        page.Window.Close();
    }

    [AvaloniaFact]
    public void EveryTokenAndOptionAndCheckboxIsAtLeastFortyFourTall()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        CatalogueControls.Choose(page.Panel, "Length", 1);

        foreach (var name in new[] { "Level", "Length", "Story length" })
        {
            Assert.True(CatalogueControls.Token(page.Panel, name).Bounds.Height >= 44, name);
        }

        CatalogueControls.PressToken(page.Panel, "Story length");
        Assert.All(page.Panel.GetVisualDescendants().OfType<Segment>().SelectMany(segment => segment.GetVisualDescendants().OfType<RadioButton>()).Where(option => option.IsEffectivelyVisible), option => Assert.True(option.Bounds.Height >= 44));

        CatalogueControls.PressToken(page.Panel, "Level");
        Assert.All(page.Panel.GetVisualDescendants().OfType<CheckBox>().Where(box => box.IsEffectivelyVisible), box => Assert.True(box.Bounds.Height >= 44));

        page.Window.Close();
    }

    [AvaloniaFact]
    public void AFilterThatHidesEveryStoryOffersToBeCleared()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths(), [("new", "1-year"), ("midrange", "1-year")]);

        CatalogueControls.Tick(page.Panel, "Endgame");

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

        CatalogueControls.Tick(first.Panel, "Mid-range");
        CatalogueControls.Tick(first.Panel, "Endgame");
        CatalogueControls.Choose(first.Panel, "Length", 3);
        first.Window.Close();

        var second = Open(paths);

        Assert.Equal(new StoryFilter(["midrange", "endgame"], StoryLengthCompare.AtMost, "1-month"), second.Store.Load().StoryFilter);
        Assert.Equal("MID-RANGE AND ENDGAME", CatalogueControls.TokenText(second.Panel, "Level"));
        Assert.Equal(4, Rows(second.Panel));
        Assert.Contains("4 of 11 stories", Drawn(second.Panel));

        second.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData("{\"storyFilter\":{\"level\":\"endgame\"}}")]
    [InlineData("{\"StoryFilter\":{\"Level\":\"endgame\"}}")]
    public void AFilterSavedWithASingleLevelLoadsWithThatLevelTicked(string saved)
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var paths = NewPaths();
        File.WriteAllText(paths.ViewStateFile, saved);

        var page = Open(paths);

        Assert.Equal("ENDGAME", CatalogueControls.TokenText(page.Panel, "Level"));
        Assert.Equal(3, Rows(page.Panel));

        CatalogueControls.Tick(page.Panel, "Endgame", on: false);

        Assert.Null(page.Store.Load().StoryFilter);

        page.Window.Close();
    }

    [AvaloniaFact]
    public void TheSentenceReadsTheFilterInWords()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var page = Open(NewPaths());

        Assert.Equal(["Show", "ALL", "stories of", "ANY LENGTH"], Sentence(page.Panel));
        Assert.Contains(CatalogueFilters.TokenHint, Drawn(page.Panel));

        CatalogueControls.Choose(page.Panel, "Length", 2);

        Assert.Equal(["Show", "ALL", "stories of", "EXACTLY", "1 MONTH"], Sentence(page.Panel));

        page.Window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelAtItsDefaultSizeShowsThreeCardsInItsFirstRow()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (surface, _) = Build(NewPaths());
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(D47.Core.Capabilities.Builtin.VrCapability.ModeKey, "full", SettingsCaller.Panel);

        using var vr = new D47.App.Headset.VrPanelSurface(new PanelViewModel(), settings, _ => null, gameState: () => null, adventures: surface);

        vr.Nav.Select(PanelTab.Stories);
        vr.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();

        var grid = vr.Board.View.GetVisualDescendants().OfType<CardGrid>().Single();
        var firstRow = grid.Children.Where(card => card.Bounds.Top == 0).ToList();

        Assert.Equal(3, firstRow.Count);
        Assert.True(grid.Bounds.Width <= 940 + 1);
        Assert.All(grid.Children, card => Assert.True(card.Bounds.Right <= grid.Bounds.Width + 0.5));

        vr.Board.Render().SaveCapture("stories-catalogue-vr-big-panel.png");
    }

    [AvaloniaFact]
    public void TheSevenLengthsAreOneRowOnTheBigPanel()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var paths = NewPaths();
        File.WriteAllText(paths.ViewStateFile, "{\"storyFilter\":{\"compare\":1,\"length\":\"1-month\"}}");

        var (surface, _) = Build(paths);
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(D47.Core.Capabilities.Builtin.VrCapability.ModeKey, "full", SettingsCaller.Panel);

        using var vr = new D47.App.Headset.VrPanelSurface(new PanelViewModel(), settings, _ => null, gameState: () => null, adventures: surface);

        vr.Nav.Select(PanelTab.Stories);
        vr.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();

        vr.Board.View.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(CatalogueFilters.TokenClass) && Avalonia.Automation.AutomationProperties.GetName(button) == "Story length")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();

        var options = vr.Board.View.GetVisualDescendants().OfType<Segment>()
            .Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Story length options")
            .GetVisualDescendants().OfType<RadioButton>().ToList();

        Assert.Equal(7, options.Count);
        Assert.Single(options.Select(option => option.Bounds.Top).Distinct());
        Assert.All(options, option => Assert.True(option.Bounds.Height >= 44));

        vr.Board.Render().SaveCapture("stories-catalogue-vr-lengths.png");
    }

    [AvaloniaFact]
    public void ThreeCardsFitAcrossTheBigPanelsDefaultPage()
    {
        Assert.Equal(3, CardGrid.ColumnsAt(940));
        Assert.Equal(1, CardGrid.ColumnsAt(300));
        Assert.Equal(2, CardGrid.ColumnsAt(600));
    }
}
