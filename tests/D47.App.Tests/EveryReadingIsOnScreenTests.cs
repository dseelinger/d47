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
/// The Transcript's three readings are segments, every one of them drawn whole inside the page bar on
/// every reading (#274).
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

    private static Segment_ Readings(PanelView panel) => new(panel.GetControl<D47.App.Controls.Segment>("ModeSegments"));

    /// <summary>The segment control and the buttons it draws, one per reading.</summary>
    private readonly record struct Segment_(D47.App.Controls.Segment Control)
    {
        public IReadOnlyList<RadioButton> Buttons =>
            [.. Control.GetVisualDescendants().OfType<RadioButton>()];
    }

    /// <summary>Three readings sit in segments, not behind a stepper's arrows.</summary>
    [AvaloniaFact]
    public void ThreeReadingsAreSegmentsRatherThanAStepper()
    {
        var (panel, window) = Showing(PanelView.ConversationRoot);

        Assert.True(panel.GetControl<D47.App.Controls.Segment>("ModeSegments").IsVisible);
        Assert.False(panel.GetControl<D47.App.Controls.Stepper>("ModeBox").IsVisible);

        window.Close();
    }

    /// <summary>
    /// Every segment ends inside the page bar, at every width wide enough to hold them, and the one
    /// checked is the reading showing.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(PanelView.ConversationRoot, "In Ship")]
    [InlineData(PanelView.LogRoot, "Log File")]
    [InlineData(PanelView.JournalRoot, "Journal File")]
    public void EveryReadingIsDrawnInsideThePageBar(string root, string word)
    {
        foreach (var width in new double[] { 620, 1180 })
        {
            var (panel, window) = Showing(root, width);
            var bar = panel.GetControl<DockPanel>("PageBar");
            var buttons = Readings(panel).Buttons;

            Assert.Equal(["In Ship", "Log File", "Journal File"], buttons.Select(button => button.Content as string));
            Assert.Equal(word, buttons.Single(button => button.IsChecked == true).Content as string);

            foreach (var button in buttons)
            {
                var right = button.TranslatePoint(new Point(button.Bounds.Width, 0), bar);

                Assert.NotNull(right);
                Assert.True(
                    right.Value.X <= bar.Bounds.Width,
                    $"{root} at {width}: {button.Content} ends {right.Value.X - bar.Bounds.Width} pixels past the bar");
            }

            window.Close();
        }
    }

    /// <summary>
    /// On a pane too narrow for the three, the readings step instead, with both arrows inside the box
    /// rather than cut off at the bar's edge.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(PanelView.ConversationRoot)]
    [InlineData(PanelView.LogRoot)]
    [InlineData(PanelView.JournalRoot)]
    public void ANarrowPaneStepsInstead(string root)
    {
        var (panel, window) = Showing(root, 320);
        var box = panel.GetControl<D47.App.Controls.Stepper>("ModeBox");
        var bar = panel.GetControl<DockPanel>("PageBar");

        Assert.False(panel.GetControl<D47.App.Controls.Segment>("ModeSegments").IsVisible);
        Assert.True(box.IsVisible);

        var next = box.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Next");

        var right = next.TranslatePoint(new Point(next.Bounds.Width, 0), bar);

        Assert.NotNull(right);
        Assert.True(right.Value.X <= bar.Bounds.Width, $"the next arrow ends {right.Value.X - bar.Bounds.Width} pixels past the bar");

        window.Close();
    }

    /// <summary>
    /// And the row is the same width whichever reading is showing, so nothing moves under the pointer
    /// as the reading changes.
    /// </summary>
    [AvaloniaFact]
    public void TheRowIsTheSameWidthOnEveryReading()
    {
        var widths = new[] { PanelView.ConversationRoot, PanelView.LogRoot, PanelView.JournalRoot }
            .Select(root =>
            {
                var (panel, window) = Showing(root);
                var width = Readings(panel).Control.Bounds.Width;

                window.Close();

                return width;
            })
            .ToList();

        Assert.All(widths, width => Assert.Equal(widths[0], width, 1));
    }
}
