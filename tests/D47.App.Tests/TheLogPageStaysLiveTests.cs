using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The log page keeps up with the file while it is open, reading it once a second.</summary>
[Trait("Category", "Integration")]
public class TheLogPageStaysLiveTests
{
    /// <summary>Longer than two of the page's one-second reads, so a follow that is running is seen to read.</summary>
    private const double Quiet = 2.5;

    private sealed class Log(string text)
    {
        private int _reads;

        public string Text { get; set; } = text;

        public int Reads => Volatile.Read(ref _reads);

        public string Read()
        {
            Interlocked.Increment(ref _reads);
            return Text;
        }

        /// <summary>Runs the UI thread until the file is read again, or <paramref name="seconds"/> pass; true when it was.</summary>
        public bool ReadAgainWithin(double seconds)
        {
            var start = Reads;

            return UiWait.Running(() => Reads > start, seconds);
        }
    }

    private static (Window Window, PanelView View, PanelViewModel Model) Open(Func<string> source)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel { LogSource = source };
        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 500 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view, model);
    }

    private static string Drawn(PanelView view)
    {
        var block = view.FindControl<SelectableTextBlock>("Transcript")!;
        return string.Concat((block.Inlines ?? []).OfType<Run>().Select(run => run.Text));
    }

    /// <summary>
    /// A line written while the page is open reaches it, without the Commander touching anything.
    /// </summary>
    [AvaloniaFact]
    public async Task ALineWrittenWhileThePageIsOpenArrivesOnIt()
    {
        var log = new Log("[12:00:00] the first line\n");
        var (window, view, _) = Open(log.Read);

        view.Page = TranscriptPage.Log;
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("the first line", Drawn(view), StringComparison.Ordinal);

        log.Text += "[12:00:01] THE LINE THAT ARRIVED LATER\n";

        Assert.True(UiWait.Running(() => Drawn(view).Contains("THE LINE THAT ARRIVED LATER", StringComparison.Ordinal), seconds: 5));

        window.Close();
    }

    /// <summary>
    /// And the file not having moved costs nothing: no redraw, because a redraw rebuilds every run and
    /// would fight a reader's selection once a second for no new text.
    /// </summary>
    [AvaloniaFact]
    public async Task AnUnchangedFileIsNotRedrawn()
    {
        var log = new Log("[12:00:00] steady\n");
        var (window, view, _) = Open(log.Read);

        view.Page = TranscriptPage.Log;
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        var block = view.FindControl<SelectableTextBlock>("Transcript")!;
        var before = block.Inlines!.Count;

        // Two reads, so the first has finished and anything it would have drawn is drawn.
        Assert.True(log.ReadAgainWithin(5), "the page did not read the file at all.");
        Assert.True(log.ReadAgainWithin(5), "the page read the file once and stopped.");

        Assert.Equal(before, block.Inlines!.Count);

        window.Close();
    }

    /// <summary>
    /// A page that is not the log does not read the file, which is the reasoning the original
    /// on-open-only read was built on and is kept.
    /// </summary>
    [AvaloniaFact]
    public async Task AnotherPageDoesNotReadTheLogAtAll()
    {
        var log = new Log("[12:00:00] steady\n");
        var (window, view, _) = Open(log.Read);

        view.Page = TranscriptPage.Log;
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        view.Page = TranscriptPage.Conversation;
        Dispatcher.UIThread.RunJobs();

        Assert.False(log.ReadAgainWithin(Quiet));

        window.Close();
    }

    /// <summary>And the wiring, not just the tick ( #294).</summary>
    [AvaloniaFact]
    public void ArrivingOnTheLogPageByAPageChangeFollowsIt()
    {
        var log = new Log("[12:00:00] steady\n");
        var (window, view, _) = Open(log.Read);

        Assert.False(log.ReadAgainWithin(Quiet), "it followed a page nobody is on.");

        view.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        Assert.True(log.ReadAgainWithin(5), "arriving by a page change did not start the follow.");
        Assert.True(log.ReadAgainWithin(5), "arriving by a page change read once and followed nothing.");

        view.Page = TranscriptPage.Conversation;
        Dispatcher.UIThread.RunJobs();

        Assert.False(log.ReadAgainWithin(Quiet), "leaving the page did not stop the follow.");

        window.Close();
    }

    /// <summary>The binding route: a surface put on the log page before it was given a model.</summary>
    [AvaloniaFact]
    public async Task ASurfaceBoundAfterItWasPutOnTheLogPageFollowsIt()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var log = new Log("[12:00:00] bound late\n");
        var view = new PanelView { Page = TranscriptPage.Log };
        var window = new Window { Content = view, Width = 900, Height = 500 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.DataContext = new PanelViewModel { LogSource = log.Read };
        Dispatcher.UIThread.RunJobs();
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("bound late", Drawn(view), StringComparison.Ordinal);
        Assert.True(log.ReadAgainWithin(5), "the surface bound late read once and followed nothing.");

        window.Close();
    }

    /// <summary>Switching tab off the transcript stops it: the branch that builds another tab's page returns before the transcript's reading is looked at.</summary>
    [AvaloniaFact]
    public void SwitchingToAnotherTabStopsTheFollowAndComingBackResumesIt()
    {
        var log = new Log("[12:00:00] steady\n");
        var (window, view, _) = Open(log.Read);

        view.Furnish(PanelTab.Commander, _ => new TextBlock(), new NavCrumb("checklist", "Checklist"));

        view.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        Assert.True(log.ReadAgainWithin(5));
        Assert.True(log.ReadAgainWithin(5), "the page read the file once and followed nothing.");

        view.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        Assert.False(log.ReadAgainWithin(Quiet), "another tab's page left the log being read behind it.");

        view.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.True(log.ReadAgainWithin(5), "coming back to the page did not read the file.");
        Assert.True(log.ReadAgainWithin(5), "coming back to the page did not resume the follow.");

        window.Close();
    }

    /// <summary>And a window nobody has open reads nothing: the surface leaving the screen stops it.</summary>
    [AvaloniaFact]
    public void ClosingTheWindowStopsTheFollow()
    {
        var log = new Log("[12:00:00] steady\n");
        var (window, view, _) = Open(log.Read);

        view.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        Assert.True(log.ReadAgainWithin(5));
        Assert.True(log.ReadAgainWithin(5), "the page read the file once and followed nothing.");

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(log.ReadAgainWithin(Quiet), "a closed window was still reading the log every second.");
    }
}
