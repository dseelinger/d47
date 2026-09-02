using D47.Core.Interface;

namespace D47.Core.Help;

/// <summary>
/// The level help takes the panel with (asked for 2026-08-22).
/// <para>
/// Here rather than beside the drawing, because both routes to help have to build the same
/// crumb: the mark in the corner, which the view owns, and the spoken phrase, which
/// <see cref="PanelPhrases"/> owns and which cannot see the view at all. Two spellings of one
/// key is how the drawn route and the spoken one drift apart.
/// </para>
/// </summary>
public static class HelpLevel
{
    /// <summary>How a help level is keyed, so the page rebuilds from the trail alone.</summary>
    public const string Prefix = "help:";

    /// <summary>The crumb that takes the panel for one capability's help.</summary>
    public static NavCrumb For(string capabilityId) => new(Prefix + capabilityId, "Help");

    /// <summary>The capability a help crumb is about, or the key itself if it is not one.</summary>
    public static string CapabilityOf(NavCrumb crumb) =>
        crumb.Key.StartsWith(Prefix, StringComparison.Ordinal)
            ? crumb.Key[Prefix.Length..]
            : crumb.Key;

    /// <summary>The page help falls back to: the one that is about help itself.</summary>
    public const string Index = "help";

    /// <summary>
    /// Opens help for wherever the Commander is standing — or the index, when this level has no
    /// page of its own. False only when a chooser already holds the panel, or when even the index
    /// has not been written.
    /// <para>
    /// The fallback is what makes the mark worth having everywhere. Before it, a tab whose page
    /// had no band offered nothing at all on a surface with no browser, so the Commander learnt
    /// that the mark sometimes does nothing — which is worse than a mark that always opens
    /// something, even when what it opens is one level broader than they asked for.
    /// </para>
    /// </summary>
    /// <param name="preferred">
    /// A page to open instead of the level's own, for a mark that is about something narrower than
    /// the tab it sits on — a settings card's, which is about that capability rather than about
    /// Settings (asked for 2026-08-23). It takes the same ladder down rather than a rule of its
    /// own, so a card whose page nobody has illustrated still opens something.
    /// </param>
    public static bool Open(PanelNavigator nav, string? preferred = null)
    {
        // Already showing. Pressing the mark again is not a request for help about help, and
        // stacking a second copy would need pressing Back twice to leave one page.
        //
        // <b>A chooser is no longer a refusal.</b> It used to be — "a chooser holds the panel" —
        // and the cost was that the mark did nothing at all on the pages most in need of it: the
        // module picker is a hundred rows of gear with grades, damage types and a Powerplay
        // badge, and its question mark was inert. Reported 2026-08-23 as "there's no help for
        // this page". Help is itself a level and Back dismisses it, so the chooser is still
        // underneath and still the thing returned to.
        if (Showing(nav))
        {
            return false;
        }

        var here = Reachable(preferred)
            ?? Reachable(nav.Help)
            ?? Reachable(Index);

        return here is not null && nav.Take(For(here));
    }

    /// <summary>That page, if this build carries a band for it. One rung of the ladder above.</summary>
    private static string? Reachable(string? capabilityId) =>
        capabilityId is { Length: > 0 } id && HelpLibrary.For(id) is not null ? id : null;

    /// <summary>Whether help itself is what has the panel — as opposed to a chooser, which may.</summary>
    public static bool Showing(PanelNavigator nav) =>
        nav.Trail.Count > 0 && nav.Trail[^1].Key.StartsWith(Prefix, StringComparison.Ordinal);
}

/// <summary>
/// One page's short-form help — the ELI5 band from the top of its documentation page, as data
/// the panel can draw (Phase 4, "GitHub Pages documentation"; the in-app half was asked
/// for on 2026-08-22).
/// <para>
/// <b>One source, two surfaces.</b> The band is authored once, in the markdown, and published to
/// the web by Jekyll. This is the same bytes read a second time so the panel can draw them — not
/// a copy, not a second wording to keep in step. A page that says something different in the app
/// from what it says on the site is the failure this shape exists to make impossible.
/// </para>
/// <para>
/// Nothing here knows what a colour is. A shape carries a <see cref="HelpColour"/> role, and the
/// surface drawing it resolves that against the Commander's live palette — which is what lets one
/// diagram come out amber on the desktop, teal under the Guardian theme, and recoloured again by
/// an Elite HUD matrix, without the drawing knowing any of that happened.
/// </para>
/// </summary>
public sealed record HelpArticle
{
    /// <summary>The capability this explains, matching its registry id.</summary>
    public required string CapabilityId { get; init; }

    /// <summary>The page's front-matter title, which is also what the crumb says.</summary>
    public required string Title { get; init; }

    /// <summary>The section it is filed under, as the site's nav groups it.</summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// Where it sits in the nav. Carried so the in-app index can read in the order a reader would
    /// see on the site rather than alphabetically, which is an order nobody has a use for.
    /// </summary>
    public int NavOrder { get; init; }

    /// <summary>The one line under the title. Always present; a band without one is malformed.</summary>
    public required string Intro { get; init; }

    public required IReadOnlyList<HelpSection> Sections { get; init; }

    /// <summary>
    /// Where to go next, as the band's foot declares it. Empty for a band that names nowhere.
    /// </summary>
    public IReadOnlyList<HelpLink> Links { get; init; } = [];
}

/// <summary>
/// One "where to go next" entry (asked for 2026-08-22).
/// <para>
/// Two kinds, and the difference is whether the destination is on this machine. A sibling
/// capability page is an <see cref="Article"/> and becomes another level of help drawn in the
/// panel; anything else keeps its <see cref="Href"/> and is a place only a browser can reach.
/// </para>
/// <para>
/// The distinction is drawn here rather than at the moment of pressing, because the two need
/// different affordances on a surface with no browser — and a control that does nothing is worse
/// than an absent one.
/// </para>
/// </summary>
public sealed record HelpLink
{
    public required string Title { get; init; }

    /// <summary>The line under the title, or null.</summary>
    public string? Blurb { get; init; }

    /// <summary>A sibling capability page, by id, or null when this points off the site.</summary>
    public string? Article { get; init; }

    /// <summary>
    /// The address as written, for everything that is not a sibling page — an absolute URL, or a
    /// path up out of the capability folder. Resolved by whoever can open one.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// The settings section this card would rather take the Commander to, by capability id, or
    /// null for an ordinary card (asked for 2026-08-23).
    /// <para>
    /// <b>A third destination, not a third kind of link.</b> The card still carries a real
    /// <see cref="Article"/> — the page about the same subject — because the band is one source
    /// for two surfaces and the href has to remain something a browser can follow. This says only
    /// that a surface which can reach Settings should offer the setting rather than the reading:
    /// a Commander sent to help from the page that mentions Whisper wants the Whisper rows, and
    /// arriving at a second explanation instead is the long way round.
    /// </para>
    /// <para>
    /// Which is why it degrades rather than branching. The headset has no Settings tab at all, so
    /// there the card falls back to <see cref="Article"/> and behaves exactly as every other card
    /// does, with nothing anywhere testing which surface it is on.
    /// </para>
    /// </summary>
    public string? Settings { get; init; }
}

/// <summary>One numbered step of a band: a heading, usually a picture, sometimes a paragraph.</summary>
public sealed record HelpSection
{
    /// <summary>The step number, as written. A string because it is drawn, never counted with.</summary>
    public required string Number { get; init; }

    public required string Heading { get; init; }

    /// <summary>The picture, or null for a step that is words alone.</summary>
    public HelpFigure? Figure { get; init; }

    /// <summary>The paragraph under the picture, or null. The band's only long-form prose.</summary>
    public string? Body { get; init; }
}

/// <summary>
/// A picture, in its own coordinate space. <see cref="Width"/> and <see cref="Height"/> are the
/// viewBox, so a surface scales the whole thing to whatever room it has rather than the drawing
/// needing to know how wide the panel is.
/// </summary>
public sealed record HelpFigure
{
    public required double Width { get; init; }

    public required double Height { get; init; }

    public required IReadOnlyList<HelpShape> Shapes { get; init; }
}

/// <summary>
/// The nine roles in the app's palette, and nothing else.
/// <para>
/// A closed set on purpose. The web resolves these as CSS custom properties and would happily
/// render an unknown one as nothing at all — a diagram with an invisible box, published, with no
/// test able to see it. Parsing into an enum makes an unrecognised token a failure at the moment
/// the page is read instead.
/// </para>
/// </summary>
public enum HelpColour
{
    Background,
    Surface,
    SurfaceAlt,
    Border,
    Text,
    TextMuted,
    Accent,
    AccentMuted,
    Danger,
    Info,
}

/// <summary>Which end of a label sits on its x. SVG's <c>text-anchor</c>.</summary>
public enum HelpAnchor
{
    Start,
    Middle,
    End,
}

/// <summary>One drawn thing. The subset of SVG the bands are allowed to use.</summary>
public abstract record HelpShape
{
    /// <summary>The fill role, or null for <c>none</c>.</summary>
    public HelpColour? Fill { get; init; }

    /// <summary>The stroke role, or null for no stroke.</summary>
    public HelpColour? Stroke { get; init; }

    public double StrokeWidth { get; init; }

    public double Opacity { get; init; } = 1;
}

public sealed record HelpRectangle : HelpShape
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    /// <summary>Corner radius. Zero for a square corner.</summary>
    public double Radius { get; init; }
}

public sealed record HelpEllipse : HelpShape
{
    public required double CentreX { get; init; }

    public required double CentreY { get; init; }

    public required double RadiusX { get; init; }

    public required double RadiusY { get; init; }
}

public sealed record HelpLine : HelpShape
{
    public required double X1 { get; init; }

    public required double Y1 { get; init; }

    public required double X2 { get; init; }

    public required double Y2 { get; init; }

    /// <summary>The dash pattern, in the figure's own units, or empty for a solid line.</summary>
    public IReadOnlyList<double> Dashes { get; init; } = [];
}

public sealed record HelpPolygon : HelpShape
{
    public required IReadOnlyList<HelpPoint> Points { get; init; }
}

/// <summary>
/// A path, carrying its <c>d</c> verbatim. The one shape whose contents are not parsed here:
/// the bands use only moves, lines and quadratic curves, and every surface that can draw at all
/// can already read that mini-language.
/// </summary>
public sealed record HelpPath : HelpShape
{
    public required string Data { get; init; }
}

/// <summary>
/// Text. <see cref="Y"/> is the baseline, as it is in SVG, rather than the top of the line — so
/// a surface has to shift it by the font's ascent rather than drawing it a line too low.
/// </summary>
public sealed record HelpLabel : HelpShape
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required string Text { get; init; }

    public required double Size { get; init; }

    public bool Bold { get; init; }

    public HelpAnchor Anchor { get; init; } = HelpAnchor.Start;
}

public readonly record struct HelpPoint(double X, double Y);
