using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The affordance that starts a donation, and the two pages it belongs on
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// Drawn through the real panel rather than probed on the view model, because "the button is
/// there" is a claim about a page and a null host delegate is exactly the kind of absence a probe
/// of the model cannot see.
/// </para>
/// </summary>
public class TheDonateButtonIsOnlyWhereItCanWorkTests
{
    private static PanelView Furnished(bool donation)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSearch();
        panel.EnableRawJournal();

        if (donation)
        {
            panel.EnableDonation(_ => { });
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

        return panel.GetControl<Button>("DonateButton").IsVisible;
    }

    /// <summary>
    /// The two diagnostic readings are the two halves of an incident — Elite's events, and what
    /// d47 did with them.
    /// </summary>
    [AvaloniaFact]
    public void ItIsOnTheReadingsAnExcerptIsCutFrom()
    {
        var panel = Furnished(donation: true);

        Assert.True(Shown(panel, TranscriptPage.Log));
        Assert.True(Shown(panel, TranscriptPage.Journal));
        Assert.True(Shown(panel, TranscriptPage.RawJournal));
    }

    /// <summary>
    /// And not on In Ship, which is the conversation — the log already holds a more exact copy of
    /// it, so there is nothing here the other readings would not cut better.
    /// </summary>
    [AvaloniaFact]
    public void ItIsNotOnThePageItWouldCutNothingFrom()
    {
        var panel = Furnished(donation: true);

        Assert.False(Shown(panel, TranscriptPage.Conversation));
    }

    /// <summary>
    /// Furnished rather than branched, and here that is a safety property rather than a
    /// convention: the review step is the whole of the consent, and the headset has neither a
    /// clipboard to put the result on nor a file picker to write it with.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceThatCannotFinishTheActDoesNotOfferIt()
    {
        var panel = Furnished(donation: false);

        Assert.False(Shown(panel, TranscriptPage.Log));
        Assert.False(Shown(panel, TranscriptPage.Journal));
    }
}
