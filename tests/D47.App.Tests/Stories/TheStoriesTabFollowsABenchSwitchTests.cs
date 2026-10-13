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

namespace D47.App.Tests.Stories;

/// <summary>A bench switch redraws the Stories page and the Short Stories without a restart.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoriesTabFollowsABenchSwitchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 20, 0, 0, TimeSpan.Zero);

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Inlines is { Count: > 0 } inlines ? inlines.Text ?? string.Empty : block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheIncomingSlotsStoryAndShortStoryAreDrawn()
    {
        var paths = new AppPaths(TempFolders.Create("d47-story-bench"));
        paths.EnsureCreated();

        var files = new DiskFileSystem();
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);

        var bench = StorySlotFiles.In(Path.Combine(paths.Data, StoryBench.Folder, "test"));
        files.CreateFolder(Path.Combine(paths.Data, StoryBench.Folder, "test"));
        StoryStore.Open(bench.Stories, files, NullLogger<StoryStore>.Instance)
            .Save("F1", new Story { Id = StoryFixture.Story.Id, Title = StoryFixture.Story.Title, PublicLayer = "-" });
        Assert.Null(new AdventureStore(bench.Adventures, files, NullLogger<AdventureStore>.Instance)
            .Save("F1", AdventuresTabTests.Story(Now) with { Key = "the-bench-route", Name = "The Bench Route" }));

        var mine = StorySlotFiles.In(paths.Data);
        var stories = StoryStore.Open(mine.Stories, files, NullLogger<StoryStore>.Instance);
        var adventures = new AdventureStore(mine.Adventures, files, NullLogger<AdventureStore>.Instance);
        var archive = StoryChapterArchive.Open(files, mine.Chapters, NullLogger<StoryChapterArchive>.Instance);
        var book = new AdventureBook(adventures, NullLogger<AdventureBook>.Instance);
        adventures.Changed += book.Reconcile;

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Archive = archive,
        };
        var storyBench = new StoryBench(paths.Data, files, stories, adventures, archive, NullLogger<StoryBench>.Instance) { Director = director };
        Assert.Null(storyBench.Create("test"));

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director);
        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);
        new Window { Content = panel, Width = 1280, Height = 860 }.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(StoriesView.Crumb);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(Drawn(panel), text => text.StartsWith("Your story", StringComparison.OrdinalIgnoreCase));

        Assert.Null(storyBench.SwitchTo("test", "F1", Now));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Drawn(panel), text => text.StartsWith("Your story", StringComparison.OrdinalIgnoreCase));

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.RootKey, "Stories"));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Drawn(panel), text => text.Contains("THE BENCH ROUTE", StringComparison.Ordinal));

        Assert.Null(storyBench.SwitchTo(null, "F1", Now.AddHours(1)));
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(Drawn(panel), text => text.Contains("THE BENCH ROUTE", StringComparison.Ordinal));
    }
}
