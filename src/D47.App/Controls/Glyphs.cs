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
/// than the word it replaced — so <em>Order</em> and <em>Import/Export</em> stay as words. That is
/// the whole of "where possible": it is a test the picture has to pass, not a target to convert
/// everything to.
/// </para>
/// <para>
/// <b>The tab names were on that list and are not any more</b>
/// (<a href="https://github.com/dseelinger/d47/issues/234">#234</a>), and the rule above is the
/// reason rather than something set aside. A tab shows its <em>word</em> whenever the strip has
/// room; the mark appears only when it does not, in place of the tab scrolling out of sight
/// altogether. So a Commander never has to learn a picture to find a page — they meet it, if at
/// all, beside the word it stands for, and the alternative to the mark is not the word but
/// nothing. The Commander chose all eight from drawn candidates on 2026-08-31.
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

    /// <summary>
    /// Open every card: a plus
    /// (<a href="https://github.com/dseelinger/d47/issues/223">#223</a>).
    /// <para>
    /// <b>It was two chevrons pointing apart, and the Commander asked for a plus and a minus
    /// instead</b> (2026-09-01). The chevron pair reads as <i>scroll</i> before it reads as
    /// <i>unfold</i>, and doubling it does not fix that — where plus and minus are what every
    /// tree control has meant by open and shut for thirty years, and they carry no second
    /// meaning at all.
    /// </para>
    /// <para>
    /// <b>Not <see cref="Expand"/>, which is a different verb.</b> Those four brackets are the
    /// full-screen mark — make this thing bigger — and this is about what is inside a card. The
    /// pair still only works as a pair.
    /// </para>
    /// </summary>
    public const string ExpandAll = "M 12,5 L 12,19  M 5,12 L 19,12";

    /// <summary>
    /// Shut every card: the same stroke without the upright.
    /// <para>
    /// <see cref="ExpandAll"/> and <see cref="Add"/> are the same path today and are deliberately
    /// two constants: one means <i>open what is inside this</i> and the other means <i>make
    /// another one</i>, and a change to either verb's mark must not move the other.
    /// </para>
    /// </summary>
    public const string CollapseAll = "M 5,12 L 19,12";

    /// <summary>
    /// What this row is: a lower-case <c>i</c> in a circle
    /// (asked for 2026-09-01 — <i>"That is WAY too much text"</i>).
    /// <para>
    /// <b>The help moved behind it because the rows were unreadable.</b> Push-to-talk's help runs
    /// to eleven lines, and eleven lines of grey prose under every row is a page nobody scans —
    /// the setting a Commander came for is buried in the explanation of the setting above it. The
    /// text is unchanged and one press away.
    /// </para>
    /// <para>
    /// <b>An <c>i</c> rather than a <c>?</c>.</b> The question mark is already spoken for on this
    /// surface: it is the card's way out to the documentation site, and two marks that both mean
    /// <i>help</i> and do different things is worse than either alone. This one says <i>about this
    /// row</i>; that one says <i>the long form, on the web</i>. The callout carries both, which is
    /// how they stay told apart.
    /// </para>
    /// <para>
    /// The dot is a zero-length segment: <see cref="Made"/> strokes with a round cap, so it draws
    /// as a dot without needing a second filled shape.
    /// </para>
    /// <para>
    /// <b>Two semicircles rather than one nearly-closed arc.</b> The other round mark here is
    /// written <c>A 9,9 0 1 1 11.99,3</c> — an arc that ends a hundredth of a unit from where it
    /// began — which leaves which circle is meant to be inferred from the flags. Two half turns
    /// say it outright, and the shape cannot come out subtly wrong.
    /// </para>
    /// <para>
    /// <b>It came out looking like a flat tyre, and the radius was not why</b> (reported
    /// 2026-09-01). <see cref="Made"/> stretches the <em>geometry</em> to the control and then
    /// strokes it two units wide, so <b>half the stroke always lands outside the box</b> — one
    /// unit, about 0.9 of a pixel at this size, measured. <b>Every mark in this file does it.</b>
    /// Only a closed curve makes it visible: a clipped arc reads as a flat edge where a clipped
    /// line end reads as nothing at all.
    /// </para>
    /// <para>
    /// So the fix is on the button, which had four pixels of horizontal padding and none
    /// vertical — which is exactly where it was cut. The smaller radius here is only that a
    /// slightly smaller circle sits better beside the pills.
    /// </para>
    /// </summary>
    public const string Info =
        "M 12,4 A 8,8 0 0 1 12,20 A 8,8 0 0 1 12,4  M 12,8 L 12,8  M 12,11.5 L 12,16";

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
    /// <para>
    /// <b>Redrawn 2026-09-01 — <i>"this glyph is dumb"</i>.</b> It was a three-quarter arc opening
    /// at the top with the arrowhead folded back over the gap, which at fourteen pixels read as a
    /// comma with a tick on it. This is a fuller turn with the head clear of the arc, chosen from
    /// seven drawings. The direction is unchanged and is still the point.
    /// </para>
    /// <para>
    /// <b>Inside its own bounds on purpose.</b> <see cref="Made"/> stretches the geometry to the
    /// control and then strokes it two units wide, so half that stroke lands outside — which is
    /// what made the info mark look like a flat tyre until the button was given vertical padding.
    /// The arrowhead here is the outermost thing on three sides, and it is a line end rather than
    /// a curve, so a clipped pixel of it reads as nothing.
    /// </para>
    /// </summary>
    public const string Reset =
        "M 5,9.2 A 8.5,8.5 0 1 1 4.5,13.5  M 9.6,8.9 L 4.8,9.1 L 5.0,4.3";

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
    /// The eight tab marks (#234). Each was chosen from four drawn candidates, and three of them
    /// were arrived at by rejecting something more obvious — those reasons are worth keeping.
    /// </summary>
    public static class Tabs
    {
        /// <summary>A speech bubble. The conversation, and the two file readings beside it.</summary>
        public const string Transcript = "M 4,5 L 20,5 L 20,15 L 11,15 L 7,19 L 7,15 L 4,15 Z";

        /// <summary>
        /// A signpost: a post with an arm pointing each way.
        /// <para>
        /// <b>Not a rising line with waypoints on it</b>, which was the obvious drawing and reads
        /// as a stock chart. Seen that way once it cannot be unseen, and nothing about it says
        /// <em>route</em>.
        /// </para>
        /// </summary>
        public const string Routing = "M 12,3 L 12,21  M 12,6 L 20,6 L 18,9 L 12,9  M 12,12 L 4,12 L 6,15 L 12,15";

        /// <summary>Two ticks beside two lines: the list, and the fact that lines come off it.</summary>
        public const string Checklist = "M 3,7 L 5,9 L 9,5  M 3,15 L 5,17 L 9,13  M 12,7 L 21,7  M 12,15 L 21,15";

        /// <summary>
        /// A flagship and two escorts — everything the Commander owns, rather than one hull.
        /// <para>
        /// A single ship was chosen first and then changed: the tab is named <em>Fleet</em>, and
        /// the root beneath it is meant to hold carriers one day
        /// (<a href="https://github.com/dseelinger/d47/issues/230">#230</a>). A mark saying "this
        /// ship" would have to be redrawn the moment it does.
        /// </para>
        /// </summary>
        public const string Fleet =
            "M 12,3 L 16.5,13 L 12,10.8 L 7.5,13 Z  M 5,13 L 7.8,19.5 L 5,18 L 2.2,19.5 Z"
            + "  M 19,13 L 21.8,19.5 L 19,18 L 16.2,19.5 Z";

        /// <summary>
        /// An anvil.
        /// <para>
        /// <b>Not a cog, and that is a decision about the set rather than about this mark.</b>
        /// Engineers, Utilities and Settings all pull toward a cog, a wrench or a row of sliders.
        /// Each reads fine alone; a strip where three of eight are variations on a gear is a strip
        /// nobody can scan. Settings has the most universal claim on the cog, so it took it.
        /// </para>
        /// </summary>
        public const string Engineers =
            "M 3,9 L 21,9 L 17,14 L 9,14 L 9,18 L 15,18 L 15,20 L 5,20 L 5,18 L 7,18 L 7,14 L 3,12 Z";

        /// <summary>A compass rose. Stories the Commander flies, rather than a list of jobs.</summary>
        public const string Adventures = "M 12,3 A 9,9 0 1 1 11.99,3  M 15.5,8.5 L 13,13 L 8.5,15.5 L 11,11 Z";

        /// <summary>A plug: the odds and ends that attach to d47 without being settings.</summary>
        public const string Utilities =
            "M 9,3 L 9,8  M 15,3 L 15,8  M 6,8 L 18,8 L 18,13 A 6,6 0 0 1 6,13 Z  M 12,19 L 12,21";

        /// <summary>
        /// A gear, and the one mark here that is <b>filled</b> rather than stroked.
        /// <para>
        /// <b>The second exception on record, and it is the first one's reason again.</b> The send
        /// arrow is filled because an outlined arrowhead at 17 pixels is a smudge; eight teeth in
        /// outline fail the same way at tab size, which is smaller still. The solid shape stays a
        /// gear all the way down.
        /// </para>
        /// <para>
        /// <c>F0</c> is the even-odd fill rule, which is what makes the hub a hole rather than a
        /// disc: the rim and the bore are two subpaths of one geometry, and under the default rule
        /// the second would fill in.
        /// </para>
        /// <para>
        /// The teeth are computed rather than drawn by eye, and the first attempt was not a gear at
        /// all — a circle with eight detached rays around it, which is a sun. A gear's teeth are
        /// cut into the rim.
        /// </para>
        /// </summary>
        public const string Settings =
            "F0 M 10.1,1.2 L 13.9,1.2 L 13.9,4.0 L 16.3,5.0 L 18.3,3.0 L 21.0,5.7 L 19.0,7.7"
            + " L 20.0,10.1 L 22.8,10.1 L 22.8,13.9 L 20.0,13.9 L 19.0,16.3 L 21.0,18.3"
            + " L 18.3,21.0 L 16.3,19.0 L 13.9,20.0 L 13.9,22.8 L 10.1,22.8 L 10.1,20.0"
            + " L 7.7,19.0 L 5.7,21.0 L 3.0,18.3 L 5.0,16.3 L 4.0,13.9 L 1.2,13.9 L 1.2,10.1"
            + " L 4.0,10.1 L 5.0,7.7 L 3.0,5.7 L 5.7,3.0 L 7.7,5.0 L 10.1,4.0 Z"
            + " M 12,6.8 A 5.2,5.2 0 1 1 11.99,6.8 Z";
    }

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
    public static Path Draw(string data, string brush, double size = 14, bool filled = false)
    {
        var glyph = Made(data, size);

        // Bound rather than assigned, so switching theme repaints it — colour by role, never by
        // literal, and never a colour read once at build time (Phase 4).
        //
        // A filled mark paints the same role through Fill and carries no stroke at all, rather
        // than doing both: a stroked outline around a solid shape thickens it by the stroke and
        // closes up the gaps a gear's teeth depend on.
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
    public static void Mark(
        Button button, string data, string brush, string says, double size = 14, bool filled = false)
    {
        button.Content = Draw(data, brush, size, filled);

        ToolTip.SetTip(button, says);
        Avalonia.Automation.AutomationProperties.SetName(button, says);
    }
}
