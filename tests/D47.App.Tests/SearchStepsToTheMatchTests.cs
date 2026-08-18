using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Stepping through matches takes you to them (remediation.md 10, item 6).
/// <para>
/// Reported from a running build: the count moved — "11 of 16" — and the page did not, and the
/// occurrence being counted was not marked out from the others.
/// </para>
/// </summary>
public class SearchStepsToTheMatchTests
{
    private static (PanelView Panel, TextBox Box) Searching(string query, int lines = 200)
    {
        var model = new PanelViewModel();

        for (var line = 0; line < lines; line++)
        {
            model.Append($"Line {line}: the beacon at Shinrarta Dezhra is quiet.\n");
        }

        // Themed, because the highlight is a theme resource: unthemed, every brush lookup
        // resolves to nothing and this would be comparing two absences.
        new D47.App.Theming.ThemeManager(
                Application.Current!,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 900, Height = 400 };

        window.Show();
        panel.EnableSearch();
        Dispatcher.UIThread.RunJobs();

        var box = panel.GetControl<TextBox>("SearchInput");

        box.Text = query;
        Dispatcher.UIThread.RunJobs();

        return (panel, box);
    }

    private static void Step(PanelView panel) =>
        panel.GetControl<Button>("SearchNext").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>The whole complaint: the count moves and the page does not.</summary>
    [AvaloniaFact]
    public void SteppingScrollsTheMatchIntoView()
    {
        var (panel, _) = Searching("beacon");

        var scroller = panel.GetControl<ScrollViewer>("TranscriptScroller");
        var start = scroller.Offset.Y;

        for (var press = 0; press < 20; press++)
        {
            Step(panel);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(
            Math.Abs(scroller.Offset.Y - start) > 1,
            $"the page did not move: still at {scroller.Offset.Y}");
    }

    /// <summary>
    /// And the one being counted is drawn differently from the rest, or the count is describing
    /// something the Commander cannot see.
    /// </summary>
    [AvaloniaFact]
    public void TheCurrentMatchIsColouredApartFromTheOthers()
    {
        var (panel, _) = Searching("beacon");

        Step(panel);
        Dispatcher.UIThread.RunJobs();

        var backgrounds = panel.GetControl<SelectableTextBlock>("Transcript")
            .Inlines!
            .OfType<Run>()
            .Where(run => run.Background is not null)
            .Select(run => (run.Background as ISolidColorBrush)?.Color)
            .Distinct()
            .ToList();

        // Two colours among the marked runs: every hit, and the one you are on.
        Assert.True(
            backgrounds.Count >= 2,
            $"every match was drawn the same: {backgrounds.Count} colour(s)");
    }

    /// <summary>The count itself, which was the half that already worked.</summary>
    [AvaloniaFact]
    public void TheCountSaysWhereYouAre()
    {
        var (panel, _) = Searching("beacon");

        Step(panel);
        Dispatcher.UIThread.RunJobs();

        var count = panel.GetControl<TextBlock>("SearchCount");

        Assert.False(string.IsNullOrWhiteSpace(count.Text));
        Assert.Contains(" of ", count.Text, StringComparison.Ordinal);
    }
}
