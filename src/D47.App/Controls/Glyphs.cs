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
/// than the word it replaced — so <em>Order</em>, <em>Import/Export</em> and the tab names stay as
/// words. That is the whole of "where possible": it is a test the picture has to pass, not a target
/// to convert everything to.
/// </para>
/// <para>
/// <b><em>Details</em> was on that list until 2026-08-30 and is not any more</b>
/// (<a href="https://github.com/dseelinger/d47/issues/210">#210</a>). The word was kept because no
/// picture says "the figures behind this"; what the Commander asked for, and chose from six
/// drawings, was a mark for the <em>subject</em> rather than for the act — and a banknote passes the
/// test the word failed. See <see cref="Spend"/>, including why it is neither a coin nor a currency
/// symbol.
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
    /// Go back to how it was: a circle open at the upper right, travelled anticlockwise from the
    /// top, with the arrowhead where it starts. The undo and refresh mark near enough everywhere
    /// (https://github.com/dseelinger/d47/issues/69).
    /// <para>
    /// <b>Anticlockwise, and that is the whole of why it reads as undo.</b> The same circle drawn
    /// the other way is refresh — do it again — which is a different promise to make about a button
    /// that throws away what the Commander typed.
    /// </para>
    /// <para>
    /// It replaces <c>↺</c> (U+21BA), which was a text character and therefore whatever the
    /// installed font happened to have: a different weight from the four marks beside it, hung off
    /// a baseline rather than centred in its box, and a hollow rectangle on a machine without it.
    /// </para>
    /// </summary>
    public const string Reset =
        "M 12,4 A 8,8 0 1 0 20,12  M 15,1 L 12,4 L 15,7";

    /// <summary>
    /// What this has cost: a banknote, a rectangle with a circle in the middle of it
    /// (<a href="https://github.com/dseelinger/d47/issues/210">#210</a>). The Commander chose it
    /// from six drawings on 2026-08-30.
    /// <para>
    /// <b>Not a coin, and not a currency symbol, and both are worth writing down because both look
    /// like the obvious choice.</b> The figures behind this button are <em>real money</em> — dollars
    /// on a provider account, not the Commander's in-game balance — and a coin-shaped mark in a
    /// cockpit overlay is exactly the thing that reads as credits. A symbol has the other problem:
    /// the figures are formatted <c>:C4</c>, which follows the machine's culture, so a <c>$</c>
    /// would be wrong for anybody not billed in dollars. On the one figure in the app that must
    /// never be misread, a note carries "money" without carrying either.
    /// </para>
    /// <para>
    /// <b>18 across by 12 down, and the proportion is the deliberate part.</b> The drawing that was
    /// chosen spanned 20 by 10, and <see cref="Draw"/> puts a glyph in a <em>square</em> box and
    /// stretches uniformly — so a 2:1 note would have filled the width, reached seven of fourteen
    /// units of height, and read as a short wide bar smaller than the marks beside it. The
    /// alternative was a non-square box, which is what <c>HelpGlyph</c> does and which would have
    /// taken this off the one path every other mark travels. Redrawing it as a normal note
    /// proportion keeps it on that path and still reads as a banknote.
    /// </para>
    /// </summary>
    public const string Spend =
        "M 3,6 L 21,6 L 21,18 L 3,18 Z  M 12,9 A 3,3 0 1 1 11.99,9";

    /// <summary>
    /// One mark, sized and coloured for the row it sits in.
    /// </summary>
    /// <param name="data">One of the constants above.</param>
    /// <param name="brush">
    /// The theme key to stroke it with, so switching theme repaints it without it knowing a theme
    /// exists — colour by role, never by literal (Phase 4).
    /// </param>
    /// <param name="size">
    /// Pixels across, square. 14 sits beside secondary text; the microphone uses 13 beside small
    /// text.
    /// </param>
    public static Path Draw(string data, string brush, double size = 14)
    {
        var glyph = Made(data, size);

        // Bound rather than assigned, so switching theme repaints it — colour by role, never by
        // literal, and never a colour read once at build time (Phase 4).
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
