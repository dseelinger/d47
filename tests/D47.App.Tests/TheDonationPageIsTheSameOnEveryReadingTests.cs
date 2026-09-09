using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Diagnostics.Donation;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>One button since #238, and one page behind it.</summary>
public class TheDonationPageIsTheSameOnEveryReadingTests
{
    private static (PanelView Panel, Func<int> Opened) Furnished()
    {
        var opened = 0;

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSearch();
        panel.EnableRawJournal();
        panel.EnableDonation(() => opened++);

        var window = new Window { Content = panel, Width = 1200, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        return (panel, () => opened);
    }

    private static void Press(PanelView panel, TranscriptPage page)
    {
        panel.Page = page;
        Dispatcher.UIThread.RunJobs();

        panel.GetControl<Button>("DonateButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static T Control<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    /// <summary>Every reading the button is on opens the one page.</summary>
    [AvaloniaFact]
    public void EveryReadingOpensIt()
    {
        var (panel, opened) = Furnished();

        Press(panel, TranscriptPage.Log);
        Press(panel, TranscriptPage.Journal);
        Press(panel, TranscriptPage.RawJournal);

        Assert.Equal(3, opened());
    }

    /// <summary>And the window it opens carries the toggle, off.</summary>
    [AvaloniaFact]
    public void ItOpensOnTheExcerptWithTheHistoryOnePressAway()
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 9, 8, 14, 0, 0, TimeSpan.Zero),
            _ => "an excerpt",
            read: (_, _, _) => Task.FromResult(
                new HelpImproveWindow.CorpusReading(
                    new CorpusSurvey(null, null, 0, 0, new CorpusTally(0, 0, 0, 0, 0, 0), []),
                    "a report")),
            write: (_, _, _) => Task.CompletedTask);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var toggle = Control<CheckBox>(window, "IncludeHistory");

        Assert.True(toggle.IsVisible);
        Assert.False(toggle.IsChecked);

        Assert.True(Control<TextBlock>(window, "Excerpt").IsVisible);
        Assert.False(Control<TextBlock>(window, "CorpusReport").IsVisible);
    }
}
