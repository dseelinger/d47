using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A switch or a phrase can open Stories › Catalogue before the Stories tab was ever shown, and the page drawn
/// is the catalogue rather than a placeholder (#1149).
/// </summary>
[Trait("Category", "Integration")]
public class ASubPageOpensBeforeItsRootWasEverDrawnTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    private static (Window Window, PanelView Panel) Open()
    {
        var paths = new AppPaths(TempFolders.Create("d47-sub-page"));
        paths.EnsureCreated();

        var cards = new List<StoryCard> { StoryFixture.Story };
        var catalog = new StoryCatalog(cards, () => [StoryFixture.Secret]);
        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), new DiskFileSystem(), NullLogger<StoryStore>.Instance);
        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(
            stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);
        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    [AvaloniaFact]
    public void TheCatalogueIsDrawnWhenItIsTheFirstStoriesPageShown()
    {
        var (window, panel) = Open();

        Assert.Equal(PanelTab.Transcript, panel.Nav.Tab);
        Assert.Contains(panel.Nav.Destinations, page => page.Key == StoriesView.RootKey);

        Assert.True(panel.Nav.Show(StoriesView.RootKey));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([AdventuresPage.RootKey, StoriesView.RootKey], panel.Nav.Trail.Select(crumb => crumb.Key));
        Assert.NotEmpty(panel.GetVisualDescendants().OfType<StoriesView>());
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Nothing here.");

        window.Close();
    }

    [AvaloniaFact]
    public void TheCatalogueAnswersToTheTranscribersSpelling()
    {
        var (window, panel) = Open();

        Assert.Equal("Catalogue.", PanelPhrases.Apply("Open the catalog.", panel.Nav));
        Assert.Equal([AdventuresPage.RootKey, StoriesView.RootKey], panel.Nav.Trail.Select(crumb => crumb.Key));

        window.Close();
    }
}
