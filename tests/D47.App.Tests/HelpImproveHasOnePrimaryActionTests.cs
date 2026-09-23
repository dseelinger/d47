using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Donation;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>The action bar carries one primary action and a clear rank below it (#338).</summary>
public sealed class HelpImproveHasOnePrimaryActionTests
{
    private static T Control<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    private static HelpImproveWindow.CorpusReading Reading(string report) =>
        new(new CorpusSurvey(null, null, 0, 0, new CorpusTally(0, 0, 0, 0, 0, 0), []), report);

    /// <summary>Both halves wired, which is the shape the running app always builds — the shape most
    /// likely to end up with two primaries if the rule is only "add the class once".</summary>
    private static HelpImproveWindow Full()
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero),
            TestSurface.Excerpt("an excerpt"),
            send: (_, _) => Task.FromResult(new DonationSent(DonationOutcome.Stored("k"), null)),
            destination: "https://donate.invalid/donate",
            read: (_, _, _) => Task.FromResult(Reading("a report")),
            write: (_, _, _) => Task.CompletedTask,
            sendCorpus: (_, _, _) => Task.FromResult(new DonationSent(DonationOutcome.Stored("k"), null)),
            forget: _ => Task.FromResult("Forgotten."));

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaFact]
    public void ExactlyOneButtonIsPrimaryOnTheExcerptPage()
    {
        var window = Full();

        Assert.Single(window.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("primary"));
        Assert.Contains("primary", Control<Button>(window, "SendExcerpt").Classes);
    }

    [AvaloniaFact]
    public void ExactlyOneButtonIsPrimaryOnTheHistoryPageToo()
    {
        var window = Full();

        Control<CheckBox>(window, "IncludeHistory").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(window.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("primary"));
        Assert.Contains("primary", Control<Button>(window, "SendCorpus").Classes);
    }

    [AvaloniaFact]
    public void SaveAndCopyAreQuietCancelIsNeitherAndForgetIsDestructive()
    {
        var window = Full();

        Assert.Contains("quiet", Control<Button>(window, "CopyExcerpt").Classes);
        Assert.Contains("quiet", Control<Button>(window, "SaveCorpus").Classes);

        var cancel = Control<Button>(window, "StopCorpus");
        Assert.DoesNotContain("quiet", cancel.Classes);
        Assert.DoesNotContain("primary", cancel.Classes);
        Assert.DoesNotContain("destructive", cancel.Classes);

        Assert.Contains("destructive", Control<Button>(window, "ForgetDonations").Classes);
    }

    /// <summary>The exact text starts folded away, and a press on the toggle brings it up.</summary>
    [AvaloniaFact]
    public void ThePayloadIsCollapsedUntilTheToggleIsPressed()
    {
        var window = Full();

        var pane = Control<Border>(window, "DisclosurePane");
        var toggle = Control<Button>(window, "DisclosureToggle");

        Assert.Equal(0d, pane.Height);

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(double.IsNaN(pane.Height));

        // And the exact text is there to show, whether or not the fold is open — GetVisualDescendants finds
        // the control by name either way.
        Assert.Equal("an excerpt", Control<SelectableTextBlock>(window, "Excerpt").Text);
    }

    /// <summary>The four figures answer from the tally the build reported, with no press.</summary>
    [AvaloniaFact]
    public void TheFourFiguresReflectTheTallyWithNoPress()
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero),
            _ => ("some text", new ExcerptTally(
                JournalEvents: 12,
                JournalWithheld: 0,
                NamesReplaced: 3,
                LogEntries: 7,
                MySpeechLines: 0,
                MySpeechIncluded: false,
                InGameMessages: 0,
                LinksDropped: 0)));

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains("12", texts);
        Assert.Contains("3", texts);
        Assert.Contains("7", texts);
        Assert.Contains("9", texts);
        Assert.Contains("LOG ENTRIES", texts);
        Assert.Contains("JOURNAL EVENTS", texts);
        Assert.Contains("NAMES REPLACED", texts);
        Assert.Contains("CHARACTERS", texts);
    }
}
