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

    /// <summary>
    /// Opens help for wherever the Commander is standing, and says so. Null when there is nothing
    /// to open — no band for this level, or a chooser already holding the panel.
    /// </summary>
    public static bool Open(PanelNavigator nav) =>
        !nav.Modal
        && nav.Help is { Length: > 0 } capability
        && HelpLibrary.For(capability) is not null
        && nav.Take(For(capability));
}

/// <summary>
/// One page's short-form help — the ELI5 band from the top of its documentation page, as data
/// the panel can draw (list.md Phase 4, "GitHub Pages documentation"; the in-app half was asked
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

    /// <summary>The one line under the title. Always present; a band without one is malformed.</summary>
    public required string Lede { get; init; }

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
