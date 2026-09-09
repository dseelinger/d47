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

/// <summary>The log page keeps up with the file while it is open.</summary>
public class TheLogPageStaysLiveTests
{
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
        var log = "[12:00:00] the first line\n";
        var (window, view, model) = Open(() => log);

        view.Page = TranscriptPage.Log;
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("the first line", Drawn(view), StringComparison.Ordinal);

        log += "[12:00:01] THE LINE THAT ARRIVED LATER\n";

        // The page's own refresh, driven directly rather than by waiting a real second: a test that sleeps
        // for a timer is a test that is slow and flaky about the same thing.
        await view.RefreshLogNow();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("THE LINE THAT ARRIVED LATER", Drawn(view), StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// And the file not having moved costs nothing: no redraw, because a redraw rebuilds every run and
    /// would fight a reader's selection once a second for no new text.
    /// </summary>
    [AvaloniaFact]
    public async Task AnUnchangedFileIsNotRedrawn()
    {
        var reads = 0;
        var (window, view, model) = Open(() => { reads++; return "[12:00:00] steady\n"; });

        view.Page = TranscriptPage.Log;
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        var block = view.FindControl<SelectableTextBlock>("Transcript")!;
        var before = block.Inlines!.Count;
        var readsAfterOpen = reads;

        await view.RefreshLogNow();
        Dispatcher.UIThread.RunJobs();

        // It read the file — that is the point of a tick — and drew nothing new from it.
        Assert.True(reads > readsAfterOpen, "the tick did not read the file at all.");
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
        var reads = 0;
        var (window, view, _) = Open(() => { reads++; return "[12:00:00] steady\n"; });

        view.Page = TranscriptPage.Log;
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        view.Page = TranscriptPage.Conversation;
        Dispatcher.UIThread.RunJobs();

        var settled = reads;

        await view.RefreshLogNow();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(settled, reads);

        window.Close();
    }

    /// <summary>And the wiring, not just the tick ( #294).</summary>
    [AvaloniaFact]
    public void ArrivingOnTheLogPageByAPageChangeFollowsIt()
    {
        var (window, view, _) = Open(() => "[12:00:00] steady\n");

        Assert.False(view.FollowingLog, "it followed a page nobody is on.");

        view.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.FollowingLog, "arriving by a page change did not start the follow.");

        view.Page = TranscriptPage.Conversation;
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.FollowingLog, "leaving the page did not stop the follow.");

        window.Close();
    }

    /// <summary>The binding route: a surface put on the log page before it was given a model.</summary>
    [AvaloniaFact]
    public async Task ASurfaceBoundAfterItWasPutOnTheLogPageFollowsIt()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var view = new PanelView { Page = TranscriptPage.Log };
        var window = new Window { Content = view, Width = 900, Height = 500 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.DataContext = new PanelViewModel { LogSource = () => "[12:00:00] bound late\n" };
        Dispatcher.UIThread.RunJobs();
        await view.Reading;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("bound late", Drawn(view), StringComparison.Ordinal);
        Assert.True(view.FollowingLog, "the surface bound late read once and followed nothing.");

        window.Close();
    }

    /// <summary>Switching tab off the transcript stops it: the branch that builds another tab's page returns before the transcript's reading is looked at.</summary>
    [AvaloniaFact]
    public void SwitchingToAnotherTabStopsTheFollowAndComingBackResumesIt()
    {
        var (window, view, _) = Open(() => "[12:00:00] steady\n");

        view.Furnish(PanelTab.Checklist, _ => new TextBlock(), new NavCrumb("checklist", "Checklist"));

        view.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.FollowingLog);

        view.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.FollowingLog, "another tab's page left the log being read behind it.");

        view.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.FollowingLog, "coming back to the page did not resume the follow.");

        window.Close();
    }

    /// <summary>And a window nobody has open reads nothing: the surface leaving the screen stops it.</summary>
    [AvaloniaFact]
    public void ClosingTheWindowStopsTheFollow()
    {
        var (window, view, _) = Open(() => "[12:00:00] steady\n");

        view.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.FollowingLog);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.FollowingLog, "a closed window was still reading the log every second.");
    }
}
