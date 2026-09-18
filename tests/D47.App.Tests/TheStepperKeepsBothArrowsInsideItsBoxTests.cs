using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Transcript's mode stepper is drawn whole — value and both arrows — on every reading (#274).</summary>
public sealed class TheStepperKeepsBothArrowsInsideItsBoxTests
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

    /// <summary>Where an arrow's far edge falls inside the box that draws it.</summary>
    private static double ArrowRightIn(D47.App.Controls.Stepper box, string arrowName)
    {
        var arrow = box.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => AutomationProperties.GetName(control) == arrowName);

        var at = arrow.TranslatePoint(new Point(arrow.Bounds.Width, 0), box);

        Assert.NotNull(at);

        return at.Value.X;
    }

    /// <summary>
    /// Every reading draws both arrows inside the box, at every width the panel can be dragged to.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(PanelView.ConversationRoot)]
    [InlineData(PanelView.LogRoot)]
    [InlineData(PanelView.JournalRoot)]
    public void EveryReadingDrawsBothArrowsInsideTheBox(string root)
    {
        foreach (var width in new double[] { 320, 620, 1180 })
        {
            var (panel, window) = Showing(root, width);
            var box = panel.GetControl<D47.App.Controls.Stepper>("ModeBox");

            Assert.True(
                ArrowRightIn(box, "Next") <= box.Bounds.Width,
                $"{root} at {width}: the next arrow ends {ArrowRightIn(box, "Next") - box.Bounds.Width} "
                + $"pixels past the box's own right edge");

            window.Close();
        }
    }

    /// <summary>
    /// And the box is sized by the longest reading rather than by the one showing, so it neither
    /// resizes under the pointer as the reading changes nor leaves the arrows whatever the shortest
    /// word happens to spare. "Journal File" is the longest of the three.
    /// </summary>
    [AvaloniaFact]
    public void TheBoxIsSizedByItsLongestReadingRatherThanTheOneShowing()
    {
        var widths = new[] { PanelView.ConversationRoot, PanelView.LogRoot, PanelView.JournalRoot }
            .Select(root =>
            {
                var (panel, window) = Showing(root);
                var width = panel.GetControl<D47.App.Controls.Stepper>("ModeBox").Bounds.Width;

                window.Close();

                return width;
            })
            .ToList();

        Assert.All(widths, width => Assert.Equal(widths[0], width, 1));
    }
}
