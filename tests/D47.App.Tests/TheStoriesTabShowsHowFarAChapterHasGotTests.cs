using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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
/// While a story's chapter is written the Stories tab shows the bar, what the write is doing and the hint under the
/// standing line, and drops them when the write ends.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheStoriesTabShowsHowFarAChapterHasGotTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void WhileAChapterIsWrittenTheTabShowsTheBarTheStageAndTheHint()
    {
        using var run = Begin();

        run.Clock.Advance(TimeSpan.FromSeconds(12));
        run.View.RefreshProgress();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("0:12", Texts(run.View));
        Assert.Contains("YOUR STORY — WRITING CHAPTER 1…", Texts(run.View));
        Assert.Contains("WRITING YOUR OBJECTIVES…", Texts(run.View));
        Assert.Contains(Texts(run.View), text => text?.StartsWith("Usually ready in about", StringComparison.Ordinal) == true);
        Assert.InRange(Fill(run.View), 0.3, 0.95);

        Save(run.Window, "stories-tab-chapter-writing.png");
        run.Release();
    }

    [AvaloniaFact]
    public void TheBarGoesWhenTheChapterIsWritten()
    {
        using var run = Begin();

        Assert.NotEmpty(Bars(run.View));
        run.Release();
        Until(() => Bars(run.View).Count == 0);
    }

    [AvaloniaFact]
    public void ALaterDraftDoesNotMoveTheBarBack()
    {
        using var run = Begin();
        run.Clock.Advance(TimeSpan.FromSeconds(20));
        run.View.RefreshProgress();
        Dispatcher.UIThread.RunJobs();
        var before = Fill(run.View);

        run.Director.Progress("F1")!.NextDraft();
        run.Clock.Advance(TimeSpan.FromSeconds(1));
        run.View.RefreshProgress();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Fill(run.View) >= before);
        run.Release();
    }

    private static Run Begin()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-chapter-progress"));
        paths.EnsureCreated();
        var surface = AdventureFixture.Surface(paths);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), TestSurface.FilesFor(paths), NullLogger<StoryStore>.Instance);
        var clock = new Clock();
        var gate = new TaskCompletionSource();
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);

        var director = new StoryDirector(
            stories, surface.Book, () => catalog,
            async (ask, _, _) =>
            {
                ask.Progress!.Enter(WriteStage.Objectives, StoryPace.For("claude-sonnet-5-5"));
                await gate.Task.ConfigureAwait(false);
                return new AdventureOutcome(null, null, "Not written.", []);
            },
            () => null, _ => { }, NullLogger.Instance)
        {
            Time = clock,
        };

        var look = AppLook.Put();

        var task = director.PickAsync("F1", StoryFixture.Story.Id, Now, CancellationToken.None);
        var view = new StoriesView(surface, director, new PanelNavigator(), new PanelPrompts(new PanelNavigator(), new Avalonia.Controls.Panel()));
        var window = new Window { Content = view, Width = 640, Height = 700 };
        window.Show();
        Until(() => Bars(view).Count == 1);

        return new Run(window, view, director, clock, () => { gate.SetResult(); task.GetAwaiter().GetResult(); }, look);
    }

    private static List<Grid> Bars(Control view) =>
        [.. view.GetVisualDescendants().OfType<Grid>().Where(grid => Avalonia.Automation.AutomationProperties.GetName(grid) == "Writing the chapter")];

    private static double Fill(Control view) => Bars(view).Single().ColumnDefinitions[0].Width.Value;

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

    private sealed class Run(Window window, StoriesView view, StoryDirector director, Clock clock, Action release, IDisposable look) : IDisposable
    {
        public Window Window => window;

        public StoriesView View => view;

        public StoryDirector Director => director;

        public Clock Clock => clock;

        public void Release() => release();

        public void Dispose()
        {
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
