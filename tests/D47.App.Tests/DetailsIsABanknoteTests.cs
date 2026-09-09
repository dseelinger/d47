using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using Xunit;

// System.IO is implicitly imported and System.IO.Path is not the one meant here — the same aliasing Glyphs.cs
// carries, for the same reason.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

/// <summary>
/// The Details button is a banknote, chosen by the Commander from six drawings on 2026-08-30.
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

    /// <summary>The sentence is on both of the places it has to be.</summary>
    [AvaloniaFact]
    public void TheWordSurvivesOnTheTooltipAndTheAccessibleName()
    {
        var button = Details(Shown());
        const string Says = "Tokens, cost, and what this has come to over time";

        Assert.Equal(Says, ToolTip.GetTip(button));
        Assert.Equal(Says, AutomationProperties.GetName(button));
    }

    /// <summary>
    /// It is the shared constant rather than a path written out beside it, so the mark cannot drift
    /// from the family it belongs to.
    /// </summary>
    [AvaloniaFact]
    public void ItDrawsTheSharedMark()
    {
        var drawn = Assert.IsType<Path>(Details(Shown()).Content);

        Assert.Equal(
            Geometry.Parse(Glyphs.Spend).Bounds,
            drawn.Data!.Bounds);
    }

    /// <summary>The aspect, which is the thing the issue said not to discover at render time.</summary>
    [AvaloniaFact]
    public void TheInkIsANoteProportionRatherThanAWideBar()
    {
        var ink = Geometry.Parse(Glyphs.Spend).Bounds;

        Assert.Equal(3, ink.X, 2);
        Assert.Equal(6, ink.Y, 2);
        Assert.Equal(18, ink.Width, 2);
        Assert.Equal(12, ink.Height, 2);

        // Comfortably nearer square than the 2:1 the chosen drawing had, which is the whole of why it was
        // redrawn rather than given a box of its own.
        Assert.True(
            ink.Width / ink.Height < 1.75,
            $"the note is {ink.Width}x{ink.Height}, which stretches to a bar in a square box");
    }

    /// <summary>
    /// And the circle is inside the note rather than hanging off it, which is what makes it read as a
    /// banknote at fourteen pixels rather than as a rectangle with a bubble.
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

            // Inside the 24-unit space the family shares, so one row of marks reads as one family rather than
            // as pictures that happen to be nearby.
            Assert.InRange(bounds.Left, 0, 24);
            Assert.InRange(bounds.Right, 0, 24);
            Assert.InRange(bounds.Top, 0, 24);
            Assert.InRange(bounds.Bottom, 0, 24);
        }
    }
}
