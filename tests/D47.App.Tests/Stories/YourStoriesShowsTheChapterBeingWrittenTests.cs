using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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

/// <summary>While a stock story's next chapter is written, or its write failed, Your stories draws a card for the story.</summary>
[Trait("Category", "Integration")]
public sealed class YourStoriesShowsTheChapterBeingWrittenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 20, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void WhileTheNextChapterIsWrittenTheRootShowsTheStoryWithItsBar()
    {
        using var run = Begin();
        var write = run.Director.WriteNextAsync("F1", Now, CancellationToken.None);
        Until(() => run.Director.IsWriting("F1") && Shows(run.Page, "Your story — writing chapter 1…"));

        Assert.True(Shows(run.Page, StoryFixture.Story.Title));
        Assert.Single(Bars(run.Page));
        Assert.DoesNotContain(Texts(run.Page), text => text?.StartsWith("No stories yet", StringComparison.OrdinalIgnoreCase) == true);

        run.Release();
        write.GetAwaiter().GetResult();
    }

    [AvaloniaFact]
    public void AFailedWriteShowsTheStoryWithWriteItAgain()
    {
        using var run = Begin();
        var write = run.Director.WriteNextAsync("F1", Now, CancellationToken.None);
        Until(() => run.Director.IsWriting("F1"));
        run.Release();
        write.GetAwaiter().GetResult();
        Until(() => Shows(run.Page, "Your story — chapter 1 could not be written."));

        Assert.Empty(Bars(run.Page));
        Assert.Contains(run.Page.GetVisualDescendants().OfType<Button>(), button => button.Content as string == "Write it again");
    }

    [AvaloniaFact]
    public void WithNothingBeingWrittenTheRootHasNoStoryCard()
    {
        using var run = Begin();

        Assert.False(Shows(run.Page, StoryFixture.Story.Title));
    }

    private static Run Begin()
    {
        var paths = new AppPaths(TempFolders.Create("d47-your-stories-writing"));
        paths.EnsureCreated();

        var files = new DiskFileSystem();
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);
        var mine = StorySlotFiles.In(paths.Data);
        var stories = StoryStore.Open(mine.Stories, files, NullLogger<StoryStore>.Instance);
        var adventures = new AdventureStore(mine.Adventures, files, NullLogger<AdventureStore>.Instance);
        var archive = StoryChapterArchive.Open(files, mine.Chapters, NullLogger<StoryChapterArchive>.Instance);
        var book = new AdventureBook(adventures, NullLogger<AdventureBook>.Instance);
        adventures.Changed += book.Reconcile;

        var gate = new TaskCompletionSource();
        var generator = AdventureFixture.Generator();
        var director = new StoryDirector(
            stories, book, () => catalog,
            async (ask, _, _) =>
            {
                ask.Progress!.Enter(WriteStage.Objectives, StoryPace.For("claude-sonnet-5-5"));
                await gate.Task.ConfigureAwait(false);
                return new AdventureOutcome(null, null, "Not written.", []);
            },
            () => null, _ => { }, NullLogger.Instance)
        {
            Archive = archive,
        };

        stories.Save("F1", new Story
        {
            Id = StoryFixture.Story.Id,
            Title = StoryFixture.Story.Title,
            PublicLayer = "-",
            Length = StoryPacing.OneMonth.Key,
            PickedAt = Now,
            BeaconScanAt = Now,
        });

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => null, () => { },
            Stories: director);

        var look = AppLook.Put();
        var page = new AdventuresPage(surface, new PanelNavigator(), new PanelPrompts(new PanelNavigator(), new Avalonia.Controls.Panel()));
        var window = new Window { Content = page, Width = 640, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Run(window, page, director, () => gate.TrySetResult(), look);
    }

    private static List<Grid> Bars(Control page) =>
        [.. page.GetVisualDescendants().OfType<Grid>().Where(grid => Avalonia.Automation.AutomationProperties.GetName(grid) == "Writing the chapter")];

    private static List<string?> Texts(Control control) =>
        [.. control.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text)];

    private static bool Shows(Control page, string text) =>
        Texts(page).Any(shown => string.Equals(shown, text, StringComparison.OrdinalIgnoreCase));

    private static void Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(condition());
    }

    private sealed class Run(Window window, AdventuresPage page, StoryDirector director, Action release, IDisposable look) : IDisposable
    {
        public AdventuresPage Page => page;

        public StoryDirector Director => director;

        public void Release() => release();

        public void Dispose()
        {
            release();
            window.Close();
            look.Dispose();
        }
    }
}
