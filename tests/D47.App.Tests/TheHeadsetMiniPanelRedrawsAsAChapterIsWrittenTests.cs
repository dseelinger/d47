using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
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
                ask.Progress!.Enter(WriteStage.Objectives, StoryPace.For("claude-sonnet-5-5", chapter: true));
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
                ask.Progress!.Enter(WriteStage.Objectives, StoryPace.For("claude-sonnet-5-5", chapter: true));
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

    [AvaloniaFact]
    public void TheHeadsetMiniSurfaceIsDirtiedAndDrawsANewClockEverySecond()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-headset-write-bar-surface"));
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
                ask.Progress!.Enter(WriteStage.Objectives, StoryPace.For("claude-sonnet-5-5", chapter: true));
                await gate.Task.ConfigureAwait(false);
                return new AdventureOutcome(null, null, "Not written.", []);
            },
            () => null, _ => { }, NullLogger.Instance)
        {
            Time = clock,
        };

        using var look = AppLook.Put();
        var task = director.PickAsync("F1", StoryFixture.Story.Id, Now, CancellationToken.None);

        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "mini", SettingsCaller.Panel);

        using (var vr = new VrPanelSurface(new PanelViewModel(), settings, _ => null, adventures: baseSurface with { Stories = director }))
        {
            vr.ApplyMode();
            vr.Nav.Select(PanelTab.Stories);
            Dispatcher.UIThread.RunJobs();
            Serve(vr);
            Until(() => HeadsetBars(vr).Count == 1, 10, 20);
            Serve(vr);

            vr.TickAdventures();
            Serve(vr);

            for (var second = 1; second <= 5; second++)
            {
                var before = HeadsetClockText(vr);
                clock.Advance(TimeSpan.FromSeconds(1));
                HeadsetBars(vr).Single().Refresh();

                vr.TickAdventures();
                Assert.True(vr.IsDirty, $"second {second}: the tick did not mark the frame dirty");

                Serve(vr);
                Assert.NotEqual(before, HeadsetClockText(vr));
            }
        }

        gate.SetResult();
        task.GetAwaiter().GetResult();
    }

    private static void Serve(VrPanelSurface panel)
    {
        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }
    }

    private static List<WriteProgressBar> HeadsetBars(VrPanelSurface panel) =>
        [.. panel.Board.View.GetVisualDescendants().OfType<WriteProgressBar>().Where(bar => bar.FindAncestorOfType<AdventureMini>() is not null)];

    private static string? HeadsetClockText(VrPanelSurface panel) =>
        HeadsetBars(panel).Single().GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).FirstOrDefault(text => text?.Contains(':') == true);

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
