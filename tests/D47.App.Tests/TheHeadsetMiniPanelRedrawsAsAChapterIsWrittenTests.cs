using Avalonia.Controls;
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
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>The headset redraws from <see cref="PanelView.TickAdventures"/>, so a moving write bar has to be reported there.</summary>
[Trait("Category", "Integration")]
public sealed class TheHeadsetMiniPanelRedrawsAsAChapterIsWrittenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void TheTickReportsAWriteBarThatMovedAndNothingElse()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-headset-write-bar"));
        paths.EnsureCreated();
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), TestSurface.FilesFor(paths), NullLogger<StoryStore>.Instance);
        var clock = new Clock();
        var gate = new TaskCompletionSource();
        var baseSurface = AdventureFixture.Surface(paths);
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);

        var director = new StoryDirector(
            stories, baseSurface.Book, () => catalog,
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

        using var look = AppLook.Put();
        var task = director.PickAsync("F1", StoryFixture.Story.Id, Now, CancellationToken.None);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(baseSurface with { Stories = director });
        var window = new Window { Content = panel, Width = 640, Height = 700 };
        window.Show();
        panel.Tab = PanelTab.Stories;
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();
        Until(() => Bars(panel).Count == 1, 10, 20);

        var before = ClockText(panel);
        panel.TickAdventures();

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(panel.TickAdventures());
        Assert.NotEqual(before, ClockText(panel));
        Assert.False(panel.TickAdventures());

        clock.Advance(TimeSpan.FromMilliseconds(300));
        Assert.False(panel.TickAdventures());

        gate.SetResult();
        task.GetAwaiter().GetResult();
        window.Close();
    }

    [AvaloniaFact]
    public void ASecondTheBarsOwnTimerDrewIsStillReportedToTheTick()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-headset-write-bar-timer"));
        paths.EnsureCreated();
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), TestSurface.FilesFor(paths), NullLogger<StoryStore>.Instance);
        var clock = new Clock();
        var gate = new TaskCompletionSource();
        var baseSurface = AdventureFixture.Surface(paths);
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);

        var director = new StoryDirector(
            stories, baseSurface.Book, () => catalog,
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

        using var look = AppLook.Put();
        var task = director.PickAsync("F1", StoryFixture.Story.Id, Now, CancellationToken.None);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(baseSurface with { Stories = director });
        var window = new Window { Content = panel, Width = 640, Height = 700 };
        window.Show();
        panel.Tab = PanelTab.Stories;
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();
        Until(() => Bars(panel).Count == 1, 10, 20);

        panel.TickAdventures();

        clock.Advance(TimeSpan.FromSeconds(1));
        Bars(panel).Single().Refresh();

        Assert.True(panel.TickAdventures());
        Assert.False(panel.TickAdventures());

        gate.SetResult();
        task.GetAwaiter().GetResult();
        window.Close();
    }

    private static List<WriteProgressBar> Bars(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<WriteProgressBar>().Where(bar => bar.IsEffectivelyVisible)];

    private static string? ClockText(PanelView panel) =>
        Bars(panel).Single().GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).FirstOrDefault(text => text?.Contains(':') == true);

    private sealed class Clock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }
}
