using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>While a jump writes a chapter the Story bench page shows the bar, the stage and the hint under its notice.</summary>
[Trait("Category", "Integration")]
public sealed class TheBenchShowsHowFarAJumpedChapterHasGotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 20, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void WhileAJumpWritesAChapterThePageShowsTheBarTheStageAndTheHint()
    {
        using var run = Begin();

        run.Clock.Advance(TimeSpan.FromSeconds(12));
        Bar(run.Page).Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(StoryBenchPage.WritingNotice, Texts(run.Page));
        Assert.Contains("0:12", Texts(run.Page));
        Assert.Contains("WRITING YOUR OBJECTIVES…", Texts(run.Page));
        Assert.Contains(Texts(run.Page), text => text?.StartsWith("Usually ready in about", StringComparison.Ordinal) == true);

        Save(run.Window, "story-bench-chapter-writing.png");
        run.Release();
    }

    [AvaloniaFact]
    public void TheBarGoesWhenTheChapterIsWritten()
    {
        using var run = Begin();

        Assert.NotEmpty(Bars(run.Page));
        run.Release();
        Until(() => Bars(run.Page).Count == 0);
    }

    [AvaloniaFact]
    public void ALaterDraftDoesNotMoveTheBarBack()
    {
        using var run = Begin();
        run.Clock.Advance(TimeSpan.FromSeconds(20));
        Bar(run.Page).Refresh();
        Dispatcher.UIThread.RunJobs();
        var before = Fill(run.Page);

        run.Director.Progress("F1")!.NextDraft();
        run.Clock.Advance(TimeSpan.FromSeconds(1));
        Bar(run.Page).Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Fill(run.Page) >= before);
        run.Release();
    }

    private static Run Begin()
    {
        var paths = new AppPaths(TempFolders.Create("d47-bench-progress"));
        paths.EnsureCreated();

        var files = new DiskFileSystem();
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);
        var mine = StorySlotFiles.In(paths.Data);
        var stories = StoryStore.Open(mine.Stories, files, NullLogger<StoryStore>.Instance);
        var adventures = new AdventureStore(mine.Adventures, files, NullLogger<AdventureStore>.Instance);
        var archive = StoryChapterArchive.Open(files, mine.Chapters, NullLogger<StoryChapterArchive>.Instance);
        var book = new AdventureBook(adventures, NullLogger<AdventureBook>.Instance);
        adventures.Changed += book.Reconcile;

        var clock = new Clock();
        var gate = new TaskCompletionSource();
        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
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
            Time = clock,
        };

        stories.Save("F1", new Story
        {
            Id = StoryFixture.Story.Id,
            Title = StoryFixture.Story.Title,
            PublicLayer = "-",
            Length = StoryPacing.OneMonth.Key,
            PickedAt = Now,
            BeaconScanAt = Now,
            Chapters = ["one"],
        });

        var bench = new StoryBench(paths.Data, files, stories, adventures, archive, NullLogger<StoryBench>.Instance) { Director = director };
        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director, Bench: bench);

        var look = AppLook.Put();
        var page = new StoryBenchPage(surface, bench, director, new PanelPrompts(new PanelNavigator(), new Avalonia.Controls.Panel()));
        var window = new Window { Content = page, Width = 640, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var jump = page.GetVisualDescendants().OfType<Button>().First(button => button.Name == "Jump");
        jump.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(() => Bars(page).Count == 1);

        return new Run(window, page, director, clock, () => gate.TrySetResult(), look);
    }

    private static WriteProgressBar Bar(Control host) => host.GetVisualDescendants().OfType<WriteProgressBar>().Single();

    private static List<Grid> Bars(Control page) =>
        [.. page.GetVisualDescendants().OfType<Grid>().Where(grid => Avalonia.Automation.AutomationProperties.GetName(grid) == "Writing the chapter")];

    private static double Fill(Control page) => Bars(page).Single().ColumnDefinitions[0].Width.Value;

    private static List<string?> Texts(Control control) =>
        [.. control.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text)];

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

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

    private sealed class Run(Window window, StoryBenchPage page, StoryDirector director, Clock clock, Action release, IDisposable look) : IDisposable
    {
        public Window Window => window;

        public StoryBenchPage Page => page;

        public StoryDirector Director => director;

        public Clock Clock => clock;

        public void Release() => release();

        public void Dispose()
        {
            release();
            window.Close();
            look.Dispose();
        }
    }

    private sealed class Clock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }
}
