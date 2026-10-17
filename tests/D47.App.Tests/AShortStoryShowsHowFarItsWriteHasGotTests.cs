using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// While a short story is written the Short Story page shows a bar, what the write is doing and how long it usually takes.
/// The write is held on the surface, so a page opened during it, on either panel, shows it. A finished story is stored and
/// its reply said at once, page open or not, and the page opens its draft the next time it is shown.
/// </summary>
public class AShortStoryShowsHowFarItsWriteHasGotTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void WhileTheObjectivesAreWrittenThePageShowsTheBarTheStageAndTheHint()
    {
        var (panel, _, _, _, model, clock, _) = Open();

        Press(panel, "Go");
        Until(() => model.Asked == 2);
        clock.Advance(TimeSpan.FromSeconds(27));
        Reopen(panel);

        Assert.True(Progress(panel).IsVisible);
        Assert.Contains("WRITING YOUR OBJECTIVES…", Texts(Progress(panel)));
        Assert.Contains("0:27", Texts(Progress(panel)));
        Assert.Contains("Usually ready in about 40 seconds.", Texts(Progress(panel)));
        Assert.InRange(Fill(panel), 0.45, 0.90);
        Assert.False(Button(panel, "Go").IsEnabled);
        Assert.True(Button(panel, "Stop").IsVisible);

        Save(panel, "short-story-writing.png");
        model.Release();
    }

    [AvaloniaFact]
    public void LeavingThePageAndComingBackShowsTheBarNoLessFull()
    {
        var (panel, _, _, _, model, clock, _) = Open();

        Press(panel, "Go");
        Until(() => model.Asked == 2);
        clock.Advance(TimeSpan.FromSeconds(10));
        Reopen(panel);
        var before = Fill(panel);

        panel.Nav.SelectRoot(AdventuresPage.RootKey);
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromSeconds(5));
        panel.Nav.SelectRoot(AdventuresPage.AskKey);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Progress(panel).IsVisible);
        Assert.True(Fill(panel) >= before, $"{Fill(panel)} fell below {before}");
        model.Release();
    }

    [AvaloniaFact]
    public void TheOtherPanelShowsTheWriteBegunOnThisOne()
    {
        var (desktop, _, surface, _, model, _, _) = Open();
        var headset = Panel(surface);

        Press(desktop, "Go");
        Until(() => model.Asked == 2);
        headset.Nav.SelectRoot(AdventuresPage.AskKey);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Progress(headset).IsVisible);
        Assert.False(Button(headset, "Go").IsEnabled);
        model.Release();
    }

    [AvaloniaFact]
    public void AStoryFinishedWhileThePageWasClosedIsSavedAndSaidAtOnceAndOpenedWhenThePageOpens()
    {
        var (panel, book, surface, said, model, _, _) = Open();

        Press(panel, "Go");
        Until(() => model.Asked == 2);
        panel.Nav.SelectRoot(AdventuresPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        model.Release();
        Until(() => book.Store.Find("F1", "the-unrecoverable-column") is not null);

        Assert.True(book.Store.Find("F1", "the-unrecoverable-column")?.IsDraft);
        Assert.Equal(["Here it is."], said);
        Assert.Equal(AdventuresPage.RootKey, panel.Nav.Trail[^1].Key);

        panel.Nav.SelectRoot(AdventuresPage.AskKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(AdventuresPage.ReadPrefix + "the-unrecoverable-column", panel.Nav.Trail[^1].Key);
        Assert.Equal(["Here it is."], said);
        Assert.Null(surface.Writing.Current);
    }

    [AvaloniaFact]
    public void StopEndsTheWriteSayingNothingAndGoIsBack()
    {
        var (panel, book, surface, said, model, _, logged) = Open();

        Press(panel, "Go");
        Until(() => model.Asked == 2);
        Press(panel, "Stop");

        Assert.False(Progress(panel).IsVisible);
        Assert.False(Button(panel, "Stop").IsVisible);
        Assert.True(Button(panel, "Go").IsEnabled);
        Assert.Null(Status(panel).Text);
        Assert.Null(surface.Writing.Current);

        Until(() => model.Cancelled);
        Thread.Sleep(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(said);
        Assert.Null(Status(panel).Text);
        Assert.Empty(book.Store.For("F1"));
        Assert.DoesNotContain(logged.Lines, line => line.Contains("was not written", StringComparison.Ordinal));
    }

    private static (PanelView Panel, AdventureBook Book, AdventureSurface Surface, List<string> Said, HoldingModel Model, Clock Clock, RecordingLogger Logged) Open()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-short-story-progress"));
        var store = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), TestSurface.FilesFor(paths), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        var state = AdventuresTabTests.State();
        IGalaxyService galaxy = new AdventuresTabTests.Galaxy();
        var said = new List<string>();
        var model = new HoldingModel(AdventuresTabTests.Spine, AdventuresTabTests.Beats);
        var clock = new Clock();
        var logged = new RecordingLogger();

        var generator = new AdventureGenerator(
            () => model, () => null, () => null, () => null, () => null, () => state,
            () => galaxy, () => null, null, null, logged);

        var surface = new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, said.Add, () => true, () => true, () => null, () => { })
        {
            Writing = new ShortStoryWriting(clock),
        };

        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        return (Panel(surface), book, surface, said, model, clock, logged);
    }

    private static PanelView Panel(AdventureSurface surface)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(surface);

        new Window { Content = panel, Width = 900, Height = 700 }.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(AdventuresPage.AskKey);
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    /// <summary>Leaves the Short Story page and opens it again, which reads the write afresh.</summary>
    private static void Reopen(PanelView panel)
    {
        panel.Nav.SelectRoot(AdventuresPage.RootKey);
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(AdventuresPage.AskKey);
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Button(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label));

    private static StatusLine Status(PanelView panel) => panel.GetVisualDescendants().OfType<StatusLine>().Single();

    private static Grid Track(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Grid>().Single(grid => AutomationProperties.GetName(grid) == "Writing the story");

    private static StackPanel Progress(PanelView panel) => (StackPanel)Track(panel).Parent!;

    private static double Fill(PanelView panel) => Track(panel).ColumnDefinitions[0].Width.Value;

    private static List<string?> Texts(Control control) =>
        [.. control.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text)];

    private static void Press(PanelView panel, string label)
    {
        Button(panel, label).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(PanelView panel, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = TopLevel.GetTopLevel(panel)!.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    /// <summary>Pumps the UI thread while generation runs on the pool, until the condition holds.</summary>
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

    /// <summary>A clock that moves only when told.</summary>
    private sealed class Clock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_lines)
            {
                _lines.Add(formatter(state, exception));
            }
        }
    }

    /// <summary>Answers each round in turn; the second round waits for <see cref="Release"/> or the token.</summary>
    private sealed class HoldingModel(params string[] replies) : ILlmProvider
    {
        private readonly TaskCompletionSource _hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _asked;
        private volatile bool _cancelled;

        public int Asked => Volatile.Read(ref _asked);

        public bool Cancelled => _cancelled;

        public string Id => "anthropic";

        public string DisplayName => "Holding";

        public string DefaultModel => "claude-opus-5";

        public void Release() => _hold.TrySetResult();

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = true,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 512,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var round = Interlocked.Increment(ref _asked) - 1;

            if (round == 1)
            {
                try
                {
                    await _hold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _cancelled = true;
                    throw;
                }
            }

            if (round >= replies.Length)
            {
                throw new InvalidOperationException($"Round {round + 1} was asked for and {replies.Length} were scripted.");
            }

            yield return new LlmStreamEvent.TextDelta(replies[round]);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        }
    }
}
