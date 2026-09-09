using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>The consent step for a corpus, driven through the drawn window.</summary>
public class ACorpusReportDescribesWhatWouldLeaveTests
{
    private static HelpImproveWindow Shown(
        Func<CorpusScope, IProgress<int>, CancellationToken, Task<HelpImproveWindow.CorpusReading>> read,
        Func<Stream, IProgress<int>, CancellationToken, Task>? write = null)
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 8, 31, 14, 0, 0, TimeSpan.Zero),
            _ => string.Empty,
            read: read,
            write: write ?? ((_, _, _) => Task.CompletedTask));

        window.Show();
        Dispatcher.UIThread.RunJobs();

 // The history half of the merged window, which is one press in: one donation page for every
        // reading opens on the excerpt, and the toggle is what asks for the other half.
        Control<CheckBox>(window, "IncludeHistory").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>By name down the visual tree — a window built in code has no name scope.</summary>
    private static T Control<T>(HelpImproveWindow window, string name)
        where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    private static HelpImproveWindow.CorpusReading Reading(string report) =>
        new(new CorpusSurvey(null, null, 0, 0, new CorpusTally(0, 0, 0, 0, 0, 0), []), report);

    private static async Task PressAsync(HelpImproveWindow window, string button)
    {
        Control<Button>(window, button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // The read runs on a worker and posts back.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ThereIsNothingToSaveBeforeAnythingHasBeenRead()
    {
        var window = Shown((_, _, _) => Task.FromResult(Reading("unused")));

        Assert.False(Control<Button>(window, "SaveCorpus").IsEnabled);
        Assert.Contains("Nothing has been read yet", Control<SelectableTextBlock>(window, "CorpusReport").Text);
    }

    /// <summary>The pane holds the report, which is the document the yes is given to.</summary>
    [AvaloniaFact]
    public async Task ReadingPutsTheReportOnScreenAndArmsTheSave()
    {
        var window = Shown((_, _, _) => Task.FromResult(Reading("the whole report")));

        await PressAsync(window, "ReadJournals");

        Assert.Equal("the whole report", Control<SelectableTextBlock>(window, "CorpusReport").Text);
        Assert.True(Control<Button>(window, "SaveCorpus").IsEnabled);
    }

    /// <summary>The one that matters.</summary>
    [AvaloniaFact]
    public async Task ChangingTheScopeThrowsTheReportAway()
    {
        var window = Shown((_, _, _) => Task.FromResult(Reading("the whole report")));

        await PressAsync(window, "ReadJournals");
        Assert.True(Control<Button>(window, "SaveCorpus").IsEnabled);

        Control<ComboBox>(window, "Scope").SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Control<Button>(window, "SaveCorpus").IsEnabled);
        Assert.DoesNotContain("the whole report", Control<SelectableTextBlock>(window, "CorpusReport").Text);
    }

    /// <summary>The scope the Commander chose is the scope that gets read.</summary>
    [AvaloniaFact]
    public async Task TheChosenScopeIsTheOneRead()
    {
        var asked = new List<CorpusScope>();

        var window = Shown((scope, _, _) =>
        {
            asked.Add(scope);
            return Task.FromResult(Reading("read"));
        });

        Control<ComboBox>(window, "Scope").SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();

        await PressAsync(window, "ReadJournals");

        Assert.Equal(CorpusScope.All[2], Assert.Single(asked));
    }

    /// <summary>Nothing is written until the Commander picks a file.</summary>
    [AvaloniaFact]
    public async Task NothingIsWrittenWithoutSomewhereToWriteIt()
    {
        var wrote = false;

        var window = Shown(
            (_, _, _) => Task.FromResult(Reading("read")),
            (_, _, _) =>
            {
                wrote = true;
                return Task.CompletedTask;
            });

        await PressAsync(window, "ReadJournals");
        await PressAsync(window, "SaveCorpus");

        Assert.False(wrote);
    }
}
