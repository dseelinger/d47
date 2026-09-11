using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

// System.IO is implicitly imported, and System.IO.Path is not the one meant anywhere here.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Controls;

/// <summary>
/// The drawn marks d47 uses instead of words, where a word had a standard picture already (asked for
/// 2026-08-24: "use standard glyphs where possible, including expand and shrink, instead of the
/// words").
/// </summary>
public static class Glyphs
{
    /// <summary>
    /// Show more: four corner brackets opening outwards, which is the mark every video player and
    /// browser uses for full screen.
    /// </summary>
    public const string Expand =
        "M 4,10 L 4,4 L 10,4  M 14,4 L 20,4 L 20,10  M 20,14 L 20,20 L 14,20  M 10,20 L 4,20 L 4,14";

    /// <summary>
    /// Show less: the same four brackets pulled inwards, which is the same players' mark for leaving
    /// full screen.
    /// </summary>
    public const string Shrink =
        "M 4,10 L 10,10 L 10,4  M 20,10 L 14,10 L 14,4  M 20,14 L 14,14 L 14,20  M 4,14 L 10,14 L 10,20";

    /// <summary>
    /// A picture in half the width with the words beside it: a box on the right, short lines to its
    /// left and full-width lines under both (#289).
    /// </summary>
    public const string PictureBeside =
        "M 13,4 L 20,4 L 20,12 L 13,12 Z  M 4,6 L 10,6  M 4,10 L 10,10  M 4,16 L 20,16  M 4,20 L 20,20";

    /// <summary>
    /// A picture across the full width with the words under it: the same mark with the box widened.
    /// </summary>
    public const string PictureWide =
        "M 4,4 L 20,4 L 20,12 L 4,12 Z  M 4,16 L 20,16  M 4,20 L 20,20";

    /// <summary>Open every card: a plus (#223).</summary>
    public const string ExpandAll = "M 12,5 L 12,19  M 5,12 L 19,12";

    /// <summary>Shut every card: a minus, drawn as a filled bar rather than as a stroked line.</summary>
    public const string CollapseAll =
        "M 6,11 L 18,11 A 1,1 0 0 1 18,13 L 6,13 A 1,1 0 0 1 6,11 Z";

    /// <summary>
    /// What this row is: a lower-case <c>i</c> in a circle (asked for 2026-09-01 — "That is WAY too
    /// much text").
    /// </summary>
    public const string Info =
        "M 12,4 A 8,8 0 0 1 12,20 A 8,8 0 0 1 12,4  M 12,8 L 12,8  M 12,11.5 L 12,16";

    /// <summary>Two sheets, one behind the other.</summary>
    public const string Copy =
        "M 9,9 L 20,9 L 20,20 L 9,20 Z  M 15,9 L 15,4 L 4,4 L 4,15 L 9,15";

    /// <summary>A plus.</summary>
    public const string Add = "M 12,5 L 12,19  M 5,12 L 19,12";

    /// <summary>
    /// Go back to how it was: a circle open at the upper right, travelled anticlockwise from the top,
    /// with the arrowhead where it starts.
    /// </summary>
    public const string Reset =
        "M 5,9.2 A 8.5,8.5 0 1 1 4.5,13.5  M 9.6,8.9 L 4.8,9.1 L 5.0,4.3";

    /// <summary>What this has cost: a banknote, a rectangle with a circle in the middle of it (#210).</summary>
    public const string Spend =
        "M 3,6 L 21,6 L 21,18 L 3,18 Z  M 12,9 A 3,3 0 1 1 11.99,9";

    /// <summary>A prerequisite not met: an empty box (#126).</summary>
    public const string BoxEmpty = "M 5,5 L 19,5 L 19,19 L 5,19 Z";

    /// <summary>A prerequisite met: the same box with a check inside it (#126).</summary>
    public const string BoxChecked =
        "M 5,5 L 19,5 L 19,19 L 5,19 Z  M 8,12.5 L 11,15.5 L 16.5,8.5";

    /// <summary>
    /// A prerequisite nothing d47 reads can decide: the same box with a dash, the mark an indeterminate
    /// checkbox carries elsewhere (#126).
    /// </summary>
    public const string BoxUndecided = "M 5,5 L 19,5 L 19,19 L 5,19 Z  M 9,12 L 15,12";

    /// <summary>The eight tab marks (#234).</summary>
    public static class Tabs
    {
        /// <summary>A speech bubble.</summary>
        public const string Transcript = "M 4,5 L 20,5 L 20,15 L 11,15 L 7,19 L 7,15 L 4,15 Z";

        /// <summary>A signpost: a post with an arm pointing each way.</summary>
        public const string Routing = "M 12,3 L 12,21  M 12,6 L 20,6 L 18,9 L 12,9  M 12,12 L 4,12 L 6,15 L 12,15";

        /// <summary>Two ticks beside two lines: the list, and the fact that lines come off it.</summary>
        public const string Checklist = "M 3,7 L 5,9 L 9,5  M 3,15 L 5,17 L 9,13  M 12,7 L 21,7  M 12,15 L 21,15";

        /// <summary>A flagship and two escorts — everything the Commander owns, rather than one hull.</summary>
        public const string Fleet =
            "M 12,3 L 16.5,13 L 12,10.8 L 7.5,13 Z  M 5,13 L 7.8,19.5 L 5,18 L 2.2,19.5 Z"
            + "  M 19,13 L 21.8,19.5 L 19,18 L 16.2,19.5 Z";

        /// <summary>An anvil.</summary>
        public const string Engineers =
            "M 12,2.5 L 20.2,7.3 L 20.2,16.7 L 12,21.5 L 3.8,16.7 L 3.8,7.3 Z"
            + "  M 12,8.8 A 3.2,3.2 0 1 1 11.99,8.8";

        /// <summary>A compass rose.</summary>
        public const string Adventures = "M 12,3 A 9,9 0 1 1 11.99,3  M 15.5,8.5 L 13,13 L 8.5,15.5 L 11,11 Z";

        /// <summary>A plug: the odds and ends that attach to d47 without being settings.</summary>
        public const string Utilities =
            "M 9,3 L 9,8  M 15,3 L 15,8  M 6,8 L 18,8 L 18,13 A 6,6 0 0 1 6,13 Z  M 12,19 L 12,21";

        /// <summary>A gear, and the one mark here that is filled rather than stroked.</summary>
        public const string Settings =
            "F0 M 10.1,1.2 L 13.9,1.2 L 13.9,4.0 L 16.3,5.0 L 18.3,3.0 L 21.0,5.7 L 19.0,7.7"
            + " L 20.0,10.1 L 22.8,10.1 L 22.8,13.9 L 20.0,13.9 L 19.0,16.3 L 21.0,18.3"
            + " L 18.3,21.0 L 16.3,19.0 L 13.9,20.0 L 13.9,22.8 L 10.1,22.8 L 10.1,20.0"
            + " L 7.7,19.0 L 5.7,21.0 L 3.0,18.3 L 5.0,16.3 L 4.0,13.9 L 1.2,13.9 L 1.2,10.1"
            + " L 4.0,10.1 L 5.0,7.7 L 3.0,5.7 L 5.7,3.0 L 7.7,5.0 L 10.1,4.0 Z"
            + " M 12,6.8 A 5.2,5.2 0 1 1 11.99,6.8 Z";
    }

    /// <summary>One mark, sized and coloured for the row it sits in.</summary>
    /// <param name="data">One of the constants above.</param>
    /// <param name="brush">
    /// The theme key to stroke it with, so switching theme repaints it without it knowing a theme
    /// exists — colour by role, never by literal (Phase 4).
    /// </param>
    /// <param name="size">
    /// Pixels across, square. 14 sits beside secondary text; the microphone uses 13 beside small text.
    /// </param>
    public static Path Draw(string data, string brush, double size = 14, bool filled = false)
    {
        var glyph = Made(data, size);

        // Bound rather than assigned, so switching theme repaints it — colour by role, never by literal, and
        // never a colour read once at build time (Phase 4).
        if (filled)
        {
            glyph.StrokeThickness = 0;
            glyph.Bind(Shape.FillProperty, glyph.GetResourceObservable(brush));
        }
        else
        {
            glyph.Bind(Shape.StrokeProperty, glyph.GetResourceObservable(brush));
        }

        return glyph;
    }

    /// <summary>
    /// The box is the mark's own shape, not always a square (reported 2026-09-01 — the minus "is at the
    /// top of the square block").
    /// </summary>
    private static Path Made(string data, double size)
    {
        var geometry = Geometry.Parse(data);
        var bounds = geometry.Bounds;

        // Both sides, for a geometry with no area at all.
        var longest = Math.Max(Math.Max(bounds.Width, bounds.Height), 0.001);

        return new()
        {
            Width = size * bounds.Width / longest,
            Height = size * bounds.Height / longest,
            Stretch = Stretch.Uniform,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Data = geometry,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };
    }

    /// <summary>
    /// Puts a mark on a button and keeps the word where a word still belongs: on the tooltip, and on
    /// the name a screen reader says.
    /// </summary>
    public static void Mark(
        Button button, string data, string brush, string says, double size = 14, bool filled = false)
    {
        button.Content = Draw(data, brush, size, filled);

        ToolTip.SetTip(button, says);
        Avalonia.Automation.AutomationProperties.SetName(button, says);
    }

    /// <summary>Whether a path is a filled shape rather than a stroked outline.</summary>
    public static bool IsFilled(string data) =>
        data.StartsWith("F0", StringComparison.Ordinal)
        || data.StartsWith("F1", StringComparison.Ordinal);

    /// <summary>Puts a mark and its word on a button, the mark on the left (#266).</summary>
    public static void MarkAndWord(
        Button button, string data, string brush, string says, double size = 14)
    {
        var glyph = Draw(data, brush, size, IsFilled(data));

        Avalonia.Automation.AutomationProperties.SetAccessibilityView(
            glyph, Avalonia.Automation.AccessibilityView.Raw);

        var word = new TextBlock
        {
            Text = says,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        button.Content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { glyph, word },
        };

        ToolTip.SetTip(button, null);
        Avalonia.Automation.AutomationProperties.SetName(button, says);
    }
}
