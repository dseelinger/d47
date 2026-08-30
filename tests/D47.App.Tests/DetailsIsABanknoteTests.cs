using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using Xunit;

// System.IO is implicitly imported and System.IO.Path is not the one meant here — the same
// aliasing Glyphs.cs carries, for the same reason.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

/// <summary>
/// The Details button is a banknote (<a href="https://github.com/dseelinger/d47/issues/210">#210</a>),
/// chosen by the Commander from six drawings on 2026-08-30.
/// <para>
/// <b>The word has to survive the picture, and that is the assertion that matters here.</b>
/// <c>Glyphs.Mark</c> puts the sentence on the tooltip and on the accessible name, and a later
/// refactor that quietly dropped the second would leave a control that does not exist for anybody
/// not looking at it. Replacing a word with a picture is only an improvement while the word is
/// still reachable.
/// </para>
/// </summary>
public class DetailsIsABanknoteTests
{
    private static Button Details(PanelView view) =>
        view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "TurnDetails");

    private static PanelView Shown()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return view;
    }

    [AvaloniaFact]
    public void TheWordIsGoneAndAMarkIsInItsPlace()
    {
        var button = Details(Shown());

        Assert.IsType<Path>(button.Content);
        Assert.Null(button.Content as string);
    }

    /// <summary>
    /// <b>The sentence the word used to carry is still on both of the places it has to be.</b>
    /// This is the guard the issue asked for by name: the tooltip is what a Commander who has not
    /// met the picture reads, and the automation name is the only thing a screen reader has.
    /// </summary>
    [AvaloniaFact]
    public void TheWordSurvivesOnTheTooltipAndTheAccessibleName()
    {
        var button = Details(Shown());
        const string Says = "Tokens, cost, and what this has come to over time";

        Assert.Equal(Says, ToolTip.GetTip(button));
        Assert.Equal(Says, AutomationProperties.GetName(button));
    }

    /// <summary>
    /// It is the shared constant rather than a path written out beside it, so the mark cannot
    /// drift from the family it belongs to.
    /// </summary>
    [AvaloniaFact]
    public void ItDrawsTheSharedMark()
    {
        var drawn = Assert.IsType<Path>(Details(Shown()).Content);

        Assert.Equal(
            Geometry.Parse(Glyphs.Spend).Bounds,
            drawn.Data!.Bounds);
    }

    /// <summary>
    /// <b>The aspect, which is the thing the issue said not to discover at render time.</b>
    /// <c>Glyphs.Draw</c> puts a mark in a <em>square</em> box and stretches uniformly, so a note
    /// drawn 2:1 would fill the width, reach half the height and read as a short wide bar smaller
    /// than the marks beside it. Eighteen by twelve is a normal note proportion and fills the box.
    /// <para>
    /// It also pins where the arc resolved. The circle is written as one near-closed arc, and the
    /// two centres that arc could take are three units above and three below its start — so the
    /// wrong one puts the ink at 3..18 down instead of 6..18 and silently changes the aspect this
    /// exists to settle.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheInkIsANoteProportionRatherThanAWideBar()
    {
        var ink = Geometry.Parse(Glyphs.Spend).Bounds;

        Assert.Equal(3, ink.X, 2);
        Assert.Equal(6, ink.Y, 2);
        Assert.Equal(18, ink.Width, 2);
        Assert.Equal(12, ink.Height, 2);

        // Comfortably nearer square than the 2:1 the chosen drawing had, which is the whole of why
        // it was redrawn rather than given a box of its own.
        Assert.True(
            ink.Width / ink.Height < 1.75,
            $"the note is {ink.Width}x{ink.Height}, which stretches to a bar in a square box");
    }

    /// <summary>
    /// And the circle is inside the note rather than hanging off it, which is what makes it read
    /// as a banknote at fourteen pixels rather than as a rectangle with a bubble.
    /// </summary>
    [AvaloniaFact]
    public void TheFaceSitsInsideTheNote()
    {
        // The family separates its figures with a double space, so the last one is the circle.
        var figures = Glyphs.Spend.Split("  ", StringSplitOptions.RemoveEmptyEntries);

        var whole = Geometry.Parse(Glyphs.Spend).Bounds;
        var face = Geometry.Parse(figures[^1]).Bounds;

        Assert.True(whole.Contains(face), $"the face at {face} is not inside the note at {whole}");

        // Centred in it, to within a fraction of a unit on both axes.
        Assert.Equal(whole.Center.X, face.Center.X, 1);
        Assert.Equal(whole.Center.Y, face.Center.Y, 1);
    }

    /// <summary>
    /// Every mark in the family still parses. A path that does not is an empty box at render time
    /// and nothing at all in a log, which is the failure mode a drawn glyph exists to avoid.
    /// </summary>
    [AvaloniaFact]
    public void EveryMarkInTheFamilyStillDraws()
    {
        foreach (var data in new[]
                 {
                     Glyphs.Expand, Glyphs.Shrink, Glyphs.Copy,
                     Glyphs.Add, Glyphs.Reset, Glyphs.Spend,
                 })
        {
            var bounds = Geometry.Parse(data).Bounds;

            Assert.True(bounds.Width > 0 && bounds.Height > 0, $"{data} draws nothing");

            // Inside the 24-unit space the family shares, so one row of marks reads as one family
            // rather than as pictures that happen to be nearby.
            Assert.InRange(bounds.Left, 0, 24);
            Assert.InRange(bounds.Right, 0, 24);
            Assert.InRange(bounds.Top, 0, 24);
            Assert.InRange(bounds.Bottom, 0, 24);
        }
    }
}
