using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The log page keeps up with the file while it is open (reported 2026-08-28).
/// <para>
/// <b>It was a snapshot of the moment it was opened.</b> The read ran on navigation and nothing
/// ever read again, so a Commander watching the page during the failure it was opened to watch saw
/// nothing arrive — and the only way to see the next line was to leave the page and come back.
/// </para>
/// <para>
/// The original reasoning is kept and is why this is a ticker rather than a subscription: a log
/// nobody has open is not worth a file read per tick. What changed is that a page somebody is
/// looking at now counts as somebody looking.
/// </para>
/// </summary>
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

        // The page's own refresh, driven directly rather than by waiting a real second: a test
        // that sleeps for a timer is a test that is slow and flaky about the same thing.
        await view.RefreshLogNow();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("THE LINE THAT ARRIVED LATER", Drawn(view), StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// And the file not having moved costs nothing: no redraw, because a redraw rebuilds every run
    /// and would fight a reader's selection once a second for no new text.
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
}
