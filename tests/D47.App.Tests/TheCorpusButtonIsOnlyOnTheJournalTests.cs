using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The affordance that starts a whole-history donation, and the pages it belongs on
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>It is deliberately on fewer pages than <c>DonateButton</c> beside it.</b> An incident
/// excerpt has two halves — Elite's events and what d47 did with them — so it is offered from the
/// Log reading as well. A corpus is Elite's journals and nothing else, so offering it from the page
/// that shows d47's own log would name a source it does not read.
/// </para>
/// <para>
/// Drawn through the real panel rather than probed on the view model, for the reason the excerpt
/// button's own tests record: a null host delegate is exactly the kind of absence a probe of the
/// model cannot see.
/// </para>
/// </summary>
public class TheCorpusButtonIsOnlyOnTheJournalTests
{
    private static PanelView Furnished(bool corpus)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSearch();
        panel.EnableRawJournal();
        panel.EnableDonation(() => { });

        if (corpus)
        {
            panel.EnableCorpusDonation(() => { });
        }

        var window = new Window { Content = panel, Width = 1200, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static bool Shown(PanelView panel, TranscriptPage page)
    {
        panel.Page = page;
        Dispatcher.UIThread.RunJobs();

        return panel.GetControl<Button>("DonateCorpusButton").IsVisible;
    }

    /// <summary>The two readings of Elite's own journals, which is what a corpus is made of.</summary>
    [AvaloniaFact]
    public void ItIsOnTheJournalReadings()
    {
        var panel = Furnished(corpus: true);

        Assert.True(Shown(panel, TranscriptPage.Journal));
        Assert.True(Shown(panel, TranscriptPage.RawJournal));
    }

    /// <summary>
    /// <b>And not on the Log, where the excerpt button is.</b> d47's own log keeps a fortnight
    /// against the journals' thirteen months, and it is speech rather than a schema of game facts —
    /// so it has no field list, and its control is the show step, which is the one control a corpus
    /// cannot use.
    /// </summary>
    [AvaloniaFact]
    public void ItIsNotOnTheLogReadingEvenThoughTheExcerptButtonIs()
    {
        var panel = Furnished(corpus: true);

        Assert.False(Shown(panel, TranscriptPage.Log));
        Assert.True(panel.GetControl<Button>("DonateButton").IsVisible);
    }

    /// <summary>Nor on the pages there is nothing to cut from at all.</summary>
    [AvaloniaFact]
    public void ItIsNotOnThePagesThatAreNotAReading()
    {
        var panel = Furnished(corpus: true);

        Assert.False(Shown(panel, TranscriptPage.Conversation));
        Assert.False(Shown(panel, TranscriptPage.Technical));
    }

    /// <summary>
    /// Furnished rather than branched. A surface with no file picker cannot finish this act, and
    /// the headset is one — so it is simply absent there rather than present and broken.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceThatCannotFinishTheActDoesNotOfferIt()
    {
        var panel = Furnished(corpus: false);

        Assert.False(Shown(panel, TranscriptPage.Journal));
        Assert.False(Shown(panel, TranscriptPage.RawJournal));
    }
}
