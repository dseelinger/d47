using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Transcript's three readings are a plain text row, every one of them drawn whole in the page
/// bar on every reading, wrapping onto a second line rather than stepping or being squeezed (#356).
/// </summary>
public sealed class EveryReadingIsOnScreenTests
{
    private static (PanelView Panel, Window Window) Showing(string root, double width = 1180)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRawJournal();

        var window = new Window { Content = panel, Width = width, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        PanelModes.Choose(panel, root);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static IReadOnlyList<RadioButton> Readings(PanelView panel) =>
        [.. panel.GetControl<D47.App.Controls.TextChoice>("ModeReadings").GetVisualDescendants().OfType<RadioButton>()];

    /// <summary>All three readings are on screen at once — nothing steps and nothing is hidden.</summary>
    [AvaloniaFact]
    public void ThreeReadingsAreAllOnScreenAtOnce()
    {
        var (panel, window) = Showing(PanelView.ConversationRoot);

        var buttons = Readings(panel);

        Assert.Equal(["IN SHIP", "LOG FILE", "JOURNAL FILE"], buttons.Select(button => button.Content as string));
        Assert.All(buttons, button => Assert.True(button.IsVisible));

        window.Close();
    }

    /// <summary>
    /// Every reading ends inside the page bar, at a width wide enough to hold them on one line, and
    /// the one checked is the reading showing.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(PanelView.ConversationRoot, "IN SHIP")]
    [InlineData(PanelView.LogRoot, "LOG FILE")]
    [InlineData(PanelView.JournalRoot, "JOURNAL FILE")]
    public void EveryReadingIsDrawnInsideThePageBar(string root, string word)
    {
        var (panel, window) = Showing(root, 1180);
        var bar = panel.GetControl<DockPanel>("PageBar");
        var buttons = Readings(panel);

        Assert.Equal(["IN SHIP", "LOG FILE", "JOURNAL FILE"], buttons.Select(button => button.Content as string));
        Assert.Equal(word, buttons.Single(button => button.IsChecked == true).Content as string);

        // One line at this width — nothing has wrapped down.
        Assert.All(buttons, button => Assert.Equal(buttons[0].Bounds.Top, button.Bounds.Top));

        foreach (var button in buttons)
        {
            var right = button.TranslatePoint(new Point(button.Bounds.Width, 0), bar);

            Assert.NotNull(right);
            Assert.True(
                right.Value.X <= bar.Bounds.Width,
                $"{root} at 1180: {button.Content} ends {right.Value.X - bar.Bounds.Width} pixels past the bar");
        }

        window.Close();
    }

    /// <summary>
    /// On a pane too narrow for the three whole words, the row wraps onto a second line rather than
    /// squeezing them or replacing them with a stepper.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(PanelView.ConversationRoot)]
    [InlineData(PanelView.LogRoot)]
    [InlineData(PanelView.JournalRoot)]
    public void ANarrowPaneWrapsOntoASecondLine(string root)
    {
        var (panel, window) = Showing(root, 220);
        var buttons = Readings(panel);

        Assert.Equal(3, buttons.Count);
        Assert.All(buttons, button => Assert.True(button.IsVisible));
        Assert.True(
            buttons.Select(button => button.Bounds.Top).Distinct().Count() > 1,
            "the row stayed on one line instead of wrapping");

        window.Close();
    }

    /// <summary>
    /// And the row is the same width whichever reading is showing, so nothing moves under the pointer
    /// as the reading changes — every reading is always drawn, not only the one showing.
    /// </summary>
    [AvaloniaFact]
    public void TheRowIsTheSameWidthOnEveryReading()
    {
        var widths = new[] { PanelView.ConversationRoot, PanelView.LogRoot, PanelView.JournalRoot }
            .Select(root =>
            {
                var (panel, window) = Showing(root);
                var width = panel.GetControl<D47.App.Controls.TextChoice>("ModeReadings").Bounds.Width;

                window.Close();

                return width;
            })
            .ToList();

        Assert.All(widths, width => Assert.Equal(widths[0], width, 1));
    }
}
