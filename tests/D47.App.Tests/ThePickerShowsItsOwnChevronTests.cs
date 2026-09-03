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
/// The Transcript's file picker is drawn whole — word and chevron — on every reading
/// (<a href="https://github.com/dseelinger/d47/issues/273">#273</a>).
/// <para>
/// Reported from a screenshot as a box sliced at its right edge. What the drawn page shows is
/// the mechanism behind it: a <c>ComboBox</c> measures itself against its selected item and
/// nothing else, so the box was 86 pixels wide showing "In Ship" and 115 showing "Journal File".
/// It changed size under the pointer as the reading changed, and the room its chevron had was
/// whatever the current word left over.
/// </para>
/// <para>
/// Asserted on where the chevron lands rather than on the width, because a width is only wrong
/// relative to what has to fit inside it, and the chevron is the part that was reported missing.
/// </para>
/// </summary>
public sealed class ThePickerShowsItsOwnChevronTests
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

    /// <summary>Where the chevron's right-hand edge falls inside the box that draws it.</summary>
    private static double ChevronRightIn(ComboBox box)
    {
        var glyph = box.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "DropDownGlyph");

        var at = glyph.TranslatePoint(new Point(glyph.Bounds.Width, 0), box);

        Assert.NotNull(at);

        return at.Value.X;
    }

    /// <summary>
    /// Every reading draws its chevron inside the box, at every width the panel can be dragged
    /// to. The narrow end is where the row is most crowded: the Raw switch is shown on the two
    /// file readings, so those rows are the widest the tab has (#231).
    /// </summary>
    [AvaloniaTheory]
    [InlineData(PanelView.ConversationRoot)]
    [InlineData(PanelView.LogRoot)]
    [InlineData(PanelView.JournalRoot)]
    public void EveryReadingDrawsItsChevronInsideTheBox(string root)
    {
        foreach (var width in new double[] { 320, 620, 1180 })
        {
            var (panel, window) = Showing(root, width);
            var box = panel.GetControl<ComboBox>("ModeBox");

            Assert.True(
                ChevronRightIn(box) <= box.Bounds.Width,
                $"{root} at {width}: the chevron ends {ChevronRightIn(box) - box.Bounds.Width} "
                + $"pixels past the box's own right edge");

            window.Close();
        }
    }

    /// <summary>
    /// And the box is sized by the longest reading rather than by the one showing, so it neither
    /// resizes under the pointer as the reading changes nor leaves the chevron whatever the
    /// shortest word happens to spare. "Journal File" is the longest of the three.
    /// </summary>
    [AvaloniaFact]
    public void TheBoxIsSizedByItsLongestReadingRatherThanTheOneShowing()
    {
        var widths = new[] { PanelView.ConversationRoot, PanelView.LogRoot, PanelView.JournalRoot }
            .Select(root =>
            {
                var (panel, window) = Showing(root);
                var width = panel.GetControl<ComboBox>("ModeBox").Bounds.Width;

                window.Close();

                return width;
            })
            .ToList();

        Assert.All(widths, width => Assert.Equal(widths[0], width, 1));
    }
}
