using D47.Core.Interface;

namespace D47.Core.Help;

/// <summary>The level help takes the panel with (asked for 2026-08-22).</summary>
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
    /// Opens help for wherever the Commander is standing — or the index, when this level has no page of
    /// its own.
    /// </summary>
    /// <param name="preferred">
    /// A page to open instead of the level's own, for a mark that is about something narrower than the
    /// tab it sits on — a settings card's, which is about that capability rather than about Settings
    /// (asked for 2026-08-23).
    /// </param>
    public static bool Open(PanelNavigator nav, string? preferred = null)
    {
        // Already showing.
        if (Showing(nav))
        {
            return false;
        }

        var here = Reachable(preferred)
            ?? Reachable(nav.Help)
            ?? Reachable(Index);

        return here is not null && nav.Take(For(here));
    }

    /// <summary>That page, if this build carries a band for it.</summary>
    private static string? Reachable(string? capabilityId) =>
        capabilityId is { Length: > 0 } id && HelpLibrary.For(id) is not null ? id : null;

    /// <summary>Whether help itself is what has the panel — as opposed to a chooser, which may.</summary>
    public static bool Showing(PanelNavigator nav) =>
        nav.Trail.Count > 0 && nav.Trail[^1].Key.StartsWith(Prefix, StringComparison.Ordinal);
}

/// <summary>
/// One page's short-form help — the ELI5 band from the top of its documentation page, as data the panel
/// can draw (Phase 4, "GitHub Pages documentation"; the in-app half was asked for on 2026-08-22).
/// </summary>
public sealed record HelpArticle
{
    /// <summary>The capability this explains, matching its registry id.</summary>
    public required string CapabilityId { get; init; }

    /// <summary>The page's front-matter title, which is also what the crumb says.</summary>
    public required string Title { get; init; }

    /// <summary>The section it is filed under, as the site's nav groups it.</summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>Where it sits in the nav.</summary>
    public int NavOrder { get; init; }

    /// <summary>The one line under the title.</summary>
    public required string Intro { get; init; }

    public required IReadOnlyList<HelpSection> Sections { get; init; }

    /// <summary>Where to go next, as the band's foot declares it.</summary>
    public IReadOnlyList<HelpLink> Links { get; init; } = [];
}

/// <summary>One "where to go next" entry (asked for 2026-08-22).</summary>
public sealed record HelpLink
{
    public required string Title { get; init; }

    /// <summary>The line under the title, or null.</summary>
    public string? Blurb { get; init; }

    /// <summary>A sibling capability page, by id, or null when this points off the site.</summary>
    public string? Article { get; init; }

    /// <summary>
    /// The address as written, for everything that is not a sibling page — an absolute URL, or a path
    /// up out of the capability folder.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// The settings section this card would rather take the Commander to, by capability id, or null for
    /// an ordinary card (asked for 2026-08-23).
    /// </summary>
    public string? Settings { get; init; }
}

/// <summary>One numbered step of a band: a heading, usually a picture, sometimes a paragraph.</summary>
public sealed record HelpSection
{
    /// <summary>The step number, as written.</summary>
    public required string Number { get; init; }

    public required string Heading { get; init; }

    /// <summary>The picture, or null for a step that is words alone.</summary>
    public HelpFigure? Figure { get; init; }

    /// <summary>The paragraph under the picture, or null.</summary>
    public string? Body { get; init; }
}

/// <summary>A picture, in its own coordinate space.</summary>
public sealed record HelpFigure
{
    public required double Width { get; init; }

    public required double Height { get; init; }

    public required IReadOnlyList<HelpShape> Shapes { get; init; }
}

/// <summary>The nine roles in the app's palette, and nothing else.</summary>
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

/// <summary>Which end of a label sits on its x.</summary>
public enum HelpAnchor
{
    Start,
    Middle,
    End,
}

/// <summary>One drawn thing.</summary>
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

    /// <summary>Corner radius.</summary>
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

/// <summary>A path, carrying its <c>d</c> verbatim.</summary>
public sealed record HelpPath : HelpShape
{
    public required string Data { get; init; }
}

/// <summary>Text.</summary>
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
