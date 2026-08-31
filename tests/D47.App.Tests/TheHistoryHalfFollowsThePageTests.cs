using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// One button since <a href="https://github.com/dseelinger/d47/issues/238">#238</a>, and the
/// history half follows the page: the panel tells the host whether the page the button was
/// pressed on shows Elite's journals, because a journal history is the journals alone and the
/// Log page does not show them (#174's rule, carried by an argument instead of a second button).
/// <para>
/// Drawn through the real panel rather than probed on the view model, for the reason the
/// button's own tests record: a null host delegate is exactly the kind of absence a probe of the
/// model cannot see.
/// </para>
/// </summary>
public class TheHistoryHalfFollowsThePageTests
{
    private static (PanelView Panel, Func<bool?> LastOffer) Furnished()
    {
        bool? offered = null;

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSearch();
        panel.EnableRawJournal();
        panel.EnableDonation(history => offered = history);

        var window = new Window { Content = panel, Width = 1200, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        return (panel, () => offered);
    }

    private static void Press(PanelView panel, TranscriptPage page)
    {
        panel.Page = page;
        Dispatcher.UIThread.RunJobs();

        panel.GetControl<Button>("DonateButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The two readings of Elite's own journals, which is what a history is made of.</summary>
    [AvaloniaFact]
    public void TheJournalPagesOfferTheHistory()
    {
        var (panel, last) = Furnished();

        Press(panel, TranscriptPage.Journal);
        Assert.True(last());

        Press(panel, TranscriptPage.RawJournal);
        Assert.True(last());
    }

    /// <summary>
    /// <b>And the Log page does not, though the button is there.</b> d47's own log keeps a
    /// fortnight against the journals' thirteen months, and it is speech rather than a schema of
    /// game facts — the source a history does not read.
    /// </summary>
    [AvaloniaFact]
    public void TheLogPageOffersOnlyTheExcerpt()
    {
        var (panel, last) = Furnished();

        Press(panel, TranscriptPage.Log);

        Assert.True(panel.GetControl<Button>("DonateButton").IsVisible);
        Assert.False(last());
    }
}
