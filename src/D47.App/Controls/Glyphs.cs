using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

// System.IO is implicitly imported, and System.IO.Path is not the one meant anywhere here.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Controls;

/// <summary>
/// The drawn marks d47 uses instead of words, where a word had a standard picture already
/// (asked for 2026-08-24: <em>"use standard glyphs where possible, including expand and shrink,
/// instead of the words"</em>).
/// <para>
/// <b>Drawn rather than typed</b>, which is the rule the microphone indicator and the help mark
/// already follow: a glyph taken from a font is whatever weight that font happens to have, it
/// hangs off a baseline instead of sitting in the middle of its box, and it is missing outright on
/// a machine whose font does not carry it. A 24-unit coordinate space scaled into a fixed box lines
/// up with the text beside it by construction.
/// </para>
/// <para>
/// <b>Stroked in a 24-unit box, every one of them</b>, so a row of glyphs reads as one family
/// rather than as four pictures that happen to be nearby. The one exception already in the panel
/// is the send arrow, which is filled because an outlined arrowhead at 17 pixels is a smudge.
/// </para>
/// <para>
/// <b>Only where the picture is genuinely standard.</b> A glyph a Commander has to learn is worse
/// than the word it replaced — so <em>Order</em>, <em>Import/Export</em>, <em>Details</em> and the
/// tab names stay as words. That is the whole of "where possible": it is a test the picture has to
/// pass, not a target to convert everything to.
/// </para>
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
    /// Show less: the same four brackets pulled inwards, which is the same players' mark for
    /// leaving full screen. The pair only works because they are a pair.
    /// </summary>
    public const string Shrink =
        "M 4,10 L 10,10 L 10,4  M 20,10 L 14,10 L 14,4  M 20,14 L 14,14 L 14,20  M 4,14 L 10,14 L 10,20";

    /// <summary>Two sheets, one behind the other. The clipboard mark, near enough universally.</summary>
    public const string Copy =
        "M 9,9 L 20,9 L 20,20 L 9,20 Z  M 15,9 L 15,4 L 4,4 L 4,15 L 9,15";

    /// <summary>A plus. There is no ambiguity about this one anywhere.</summary>
    public const string Add = "M 12,5 L 12,19  M 5,12 L 19,12";

    /// <summary>
    /// One mark, sized and coloured for the row it sits in.
    /// </summary>
    /// <param name="data">One of the constants above.</param>
    /// <param name="brush">
    /// The theme key to stroke it with, so switching theme repaints it without it knowing a theme
    /// exists — colour by role, never by literal (list.md Phase 4).
    /// </param>
    /// <param name="size">
    /// Pixels across, square. 14 sits beside secondary text; the microphone uses 13 beside small
    /// text.
    /// </param>
    public static Path Draw(string data, string brush, double size = 14)
    {
        var glyph = Made(data, size);

        // Bound rather than assigned, so switching theme repaints it — colour by role, never by
        // literal, and never a colour read once at build time (list.md Phase 4).
        glyph.Bind(Shape.StrokeProperty, glyph.GetResourceObservable(brush));

        return glyph;
    }

    private static Path Made(string data, double size) => new()
    {
        Width = size,
        Height = size,
        Stretch = Stretch.Uniform,
        StrokeThickness = 2,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Data = Geometry.Parse(data),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
    };

    /// <summary>
    /// Puts a mark on a button and keeps the word where a word still belongs: on the tooltip, and
    /// on the name a screen reader says.
    /// <para>
    /// <b>Neither is optional.</b> A glyph-only control with no accessible name is a control that
    /// does not exist for anybody not looking at it, and one with no tooltip is a picture the
    /// Commander has to guess at the first time. Replacing a word with a picture is only an
    /// improvement if the word is still reachable.
    /// </para>
    /// </summary>
    public static void Mark(Button button, string data, string brush, string says, double size = 14)
    {
        button.Content = Draw(data, brush, size);

        ToolTip.SetTip(button, says);
        Avalonia.Automation.AutomationProperties.SetName(button, says);
    }
}
