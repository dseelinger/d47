using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;

namespace D47.Core.Help;

/// <summary>
/// Reads the ELI5 band out of a shipped documentation page (asked for 2026-08-22).
/// <para>
/// The pages are embedded rather than fetched, so in-app help works with no network and inside a
/// headset, which is the whole reason it exists: the panel's help mark used to open a browser on
/// the desktop, and a Commander wearing a headset cannot see a desktop (change-requests.md 24).
/// </para>
/// <para>
/// <b>The vocabulary is closed, and an unknown word is an exception rather than a shrug.</b> Both
/// the element names and the colour tokens are checked as they are read. A browser resolves an
/// unknown CSS custom property to nothing and draws an invisible box without complaining, which
/// is a defect that ships; here the page fails to parse and a test says which word did it.
/// </para>
/// </summary>
public static class HelpLibrary
{
    /// <summary>Where the csproj puts the pages. One name, so a rename breaks in one place.</summary>
    private const string ResourcePrefix = "D47.Core.Help.";

    private const string BandOpen = "<div class=\"d47-eli5\">";

    private static readonly ConcurrentDictionary<string, HelpArticle?> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// The band for a capability, or null when that page has none yet. Parsed once and kept —
    /// the pages are static, and re-reading one every time the Commander opens help would be
    /// paying for the same answer on a surface that redraws at 4-10 Hz.
    /// </summary>
    public static HelpArticle? For(string capabilityId) =>
        Cache.GetOrAdd(capabilityId, static id => Parse(PageFor(id), id));

    /// <summary>Every capability whose page carries a band, for the tests that sweep them.</summary>
    public static IReadOnlyList<string> Pages =>
        typeof(HelpLibrary).Assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..])
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    /// <summary>The raw markdown of one page, or null when nothing is embedded under that id.</summary>
    public static string? PageFor(string capabilityId)
    {
        using var stream = typeof(HelpLibrary).Assembly
            .GetManifestResourceStream(ResourcePrefix + capabilityId);

        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// The band out of one page's markdown, or null when it has none — which most pages still do,
    /// and which is a page waiting to be written rather than a fault.
    /// </summary>
    public static HelpArticle? Parse(string? markdown, string capabilityId)
    {
        if (markdown is null || Band(markdown) is not { } band)
        {
            return null;
        }

        var root = XElement.Parse(band);

        // By name, not by position. The frame used to be "the first child div", which was true
        // and fragile: it is a styling wrapper, and the day it stopped being needed the first
        // child div became the cards block at the foot — leaving every band with no sections at
        // all in the panel while still looking correct on the web.
        var frame = root.Elements("div")
            .FirstOrDefault(child => (string?)child.Attribute("class") == "d47-frame")
            ?? root;

        var lede = frame.Elements("p").FirstOrDefault(p => (string?)p.Attribute("class") == "lede");

        return new HelpArticle
        {
            CapabilityId = capabilityId,
            Title = FrontMatter(markdown, "title") ?? capabilityId,
            Group = FrontMatter(markdown, "group") ?? string.Empty,
            NavOrder = int.TryParse(FrontMatter(markdown, "nav_order"), out var order) ? order : 0,
            Lede = lede?.Value.Trim() ?? string.Empty,
            Sections = frame.Elements("section").Select(Section).ToArray(),

            // From the band when they are still in it, and from the foot of the page when they
            // are not (#229). The cards moved out of the band so they stay visible while it is
            // collapsed, and they took a styling shell of their own — a second d47-eli5 block at
            // the foot. Reading both is what keeps the panel's "where to go next" working
            // through that move; without it every page would have quietly lost its feet, which
            // is the failure shape the comment on Links already warns about one size larger.
            Links = Links(frame, capabilityId) is { Count: > 0 } inBand
                ? inBand
                : Feet(markdown, capabilityId),
        };
    }

    /// <summary>
    /// The cards from the foot of a page, when they are not inside the band. Null-safe and quiet:
    /// a page with none is the ordinary case for anything that never had feet.
    /// </summary>
    private static IReadOnlyList<HelpLink> Feet(string markdown, string owner)
    {
        var at = markdown.LastIndexOf(BandOpen, StringComparison.Ordinal);

        if (at < 0 || at == markdown.IndexOf(BandOpen, StringComparison.Ordinal))
        {
            return [];
        }

        return Band(markdown[at..]) is { } foot ? Links(XElement.Parse(foot), owner) : [];
    }

    /// <summary>
    /// The "where to go next" cards at the foot of a band, if it has any.
    /// <para>
    /// A sibling capability page — <c>ships.html</c> — is another band this machine already
    /// carries, so it is kept as an id. Everything else keeps its address: a path up out of the
    /// folder reaches a page with no band, and an absolute URL reaches somewhere no panel can go.
    /// </para>
    /// <para>
    /// <b>The class is a list, and it is read as one.</b> This used to compare the whole attribute
    /// against <c>card</c>, which was true right up until a card needed a second word — and the
    /// failure it produced is the worst shape available: the card is dropped, the band draws
    /// without it, and nothing anywhere is wrong enough to say so. A foot that is one card short
    /// reads as a foot that was written that way.
    /// </para>
    /// </summary>
    private static IReadOnlyList<HelpLink> Links(XElement frame, string owner) =>
        frame.Descendants("a")
            .Select(anchor => (Anchor: anchor, Classes: Classes(anchor)))
            .Where(card => card.Classes.Contains("card"))
            .Select(card =>
            {
                var href = ((string?)card.Anchor.Attribute("href") ?? string.Empty).Trim();
                var sibling = Sibling(href, owner);

                return new HelpLink
                {
                    // The arrow is a web affordance. A button in the panel is already a button.
                    Title = Span(card.Anchor, "ct").TrimEnd(' ', '→').Trim(),
                    Blurb = Span(card.Anchor, "cd") is { Length: > 0 } blurb ? blurb : null,
                    Article = sibling,
                    Href = sibling is null ? href : null,

                    // A settings card names its section with the same id as the page it points at,
                    // which is what lets one href serve the browser and the panel both. Only a
                    // sibling can carry it: a section is a capability, and an address that is not
                    // one of this machine's pages cannot be naming a capability either.
                    Settings = card.Classes.Contains("settings") ? sibling : null,
                };
            })
            .ToArray();

    /// <summary>The words in an element's <c>class</c>, whitespace-separated as HTML writes them.</summary>
    private static IReadOnlyList<string> Classes(XElement element) =>
        ((string?)element.Attribute("class") ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>How the three general help pages are keyed, a folder up from the capabilities.</summary>
    public const string GeneralPrefix = "general-";

    /// <summary>
    /// The page id in <c>ships.html</c> or <c>../conversation.html</c>, or null for anything else.
    /// <para>
    /// Two shapes, because the site has two kinds of page. A bare name beside this one is a
    /// capability; one climb out of the folder reaches Overview, Installing or Talking to
    /// Directive 47, which are embedded under a prefix so <c>conversation</c> can mean two
    /// different pages without either quietly winning.
    /// </para>
    /// <para>
    /// <b>"Beside this one" depends on which folder this one is in, which is why the owner is
    /// passed.</b> The general pages live a folder above the capabilities, so a bare
    /// <c>conversation.html</c> written on one of <em>them</em> means <em>Talking to Directive
    /// 47</em> and not the Language model page. Read without the owner it meant the second one:
    /// the Overview band's own card said "Talking to Directive 47" and opened a page about
    /// providers and billing — the same complaint that started this work, arriving one page over.
    /// </para>
    /// <para>
    /// Deliberately strict beyond those shapes: a scheme, a fragment, or any other second slash is
    /// somewhere this build is not carrying, and is left as an address.
    /// </para>
    /// </summary>
    private static string? Sibling(string href, string owner)
    {
        const string CapabilityFolder = "capabilities/";

        var fromGeneralPage = owner.StartsWith(GeneralPrefix, StringComparison.Ordinal);
        var rest = href;
        bool general;

        if (href.StartsWith("../", StringComparison.Ordinal))
        {
            // Climbing out of the capability folder reaches the general pages. Written on a page
            // that is already up there it climbs out of the site, which is not a page at all.
            if (fromGeneralPage)
            {
                return null;
            }

            rest = href[3..];
            general = true;
        }
        else if (href.StartsWith(CapabilityFolder, StringComparison.Ordinal))
        {
            // The only way down, and only from up there — a capability page naming
            // capabilities/x.html would be pointing into a folder it is already in.
            if (!fromGeneralPage)
            {
                return null;
            }

            rest = href[CapabilityFolder.Length..];
            general = false;
        }
        else
        {
            // Beside this page, whichever folder that is.
            general = fromGeneralPage;
        }

        if (!rest.EndsWith(".html", StringComparison.Ordinal))
        {
            return null;
        }

        var id = rest[..^".html".Length];

        if (id.Length == 0 || !id.All(letter => char.IsAsciiLetterLower(letter) || letter is '-'))
        {
            return null;
        }

        return general ? GeneralPrefix + id : id;
    }

    private static string Span(XElement anchor, string kind) =>
        anchor.Elements("span")
            .FirstOrDefault(span => (string?)span.Attribute("class") == kind)
            ?.Value.Trim()
        ?? string.Empty;

    /// <summary>
    /// The band's span, by counting <c>div</c>s rather than by matching a closing tag. The band
    /// nests one, and the cards block at the foot of a page opens another later in the file — so
    /// the first <c>&lt;/div&gt;</c> after the opener is the wrong end and the last one in the
    /// file is a different block entirely.
    /// </summary>
    private static string? Band(string markdown)
    {
        var start = markdown.IndexOf(BandOpen, StringComparison.Ordinal);

        if (start < 0)
        {
            return null;
        }

        var depth = 0;

        for (var at = start; at < markdown.Length; at++)
        {
            if (markdown[at] != '<')
            {
                continue;
            }

            if (Opens(markdown, at))
            {
                depth++;
            }
            else if (Closes(markdown, at))
            {
                depth--;

                if (depth == 0)
                {
                    var end = markdown.IndexOf('>', at);
                    return end < 0 ? null : markdown[start..(end + 1)];
                }
            }
        }

        return null;
    }

    // A tag rather than a prefix: "<divider" is not a div, and neither is text that happens to
    // contain the letters.
    private static bool Opens(string text, int at) =>
        text.AsSpan(at).StartsWith("<div") && !char.IsLetterOrDigit(At(text, at + 4));

    private static bool Closes(string text, int at) =>
        text.AsSpan(at).StartsWith("</div") && !char.IsLetterOrDigit(At(text, at + 5));

    private static char At(string text, int index) => index < text.Length ? text[index] : ' ';

    /// <summary>One field of the page's front matter, or null when it does not carry it.</summary>
    private static string? FrontMatter(string markdown, string field)
    {
        var wanted = field + ":";

        foreach (var line in markdown.Split('\n').Take(20))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith(wanted, StringComparison.Ordinal))
            {
                return trimmed[wanted.Length..].Trim();
            }
        }

        return null;
    }

    private static HelpSection Section(XElement section)
    {
        var heading = section.Element("h2");
        var number = heading?.Element("span");
        var figure = section.Element("svg");
        var body = section.Elements("p").FirstOrDefault(p => (string?)p.Attribute("class") == "body");

        return new HelpSection
        {
            Number = number?.Value.Trim() ?? string.Empty,

            // The heading is the h2 minus its numbered span, which is drawn separately.
            Heading = string.Concat(heading?.Nodes().OfType<XText>().Select(t => t.Value) ?? []).Trim(),
            Figure = figure is null ? null : Figure(figure),
            Body = body?.Value.Trim(),
        };
    }

    private static HelpFigure Figure(XElement svg)
    {
        var box = ((string?)svg.Attribute("viewBox") ?? "0 0 100 100")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Number)
            .ToArray();

        var shapes = new List<HelpShape>();
        Collect(svg, shapes);

        return new HelpFigure
        {
            Width = box.Length == 4 ? box[2] : 100,
            Height = box.Length == 4 ? box[3] : 100,
            Shapes = shapes,
        };
    }

    /// <summary>
    /// Walks the drawing in document order, which is paint order — a later shape covers an
    /// earlier one, the same as in the browser. Groups are transparent: they carry no transform
    /// in any band, so recursing keeps that true rather than pretending they do not exist.
    /// </summary>
    private static void Collect(XElement parent, List<HelpShape> into)
    {
        foreach (var element in parent.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "g":
                    Collect(element, into);
                    break;

                case "title":
                case "desc":
                    break;

                default:
                    into.Add(Shape(element));
                    break;
            }
        }
    }

    private static HelpShape Shape(XElement element)
    {
        var name = element.Name.LocalName;

        HelpShape shape = name switch
        {
            "rect" => new HelpRectangle
            {
                X = Attribute(element, "x"),
                Y = Attribute(element, "y"),
                Width = Attribute(element, "width"),
                Height = Attribute(element, "height"),
                Radius = Attribute(element, "rx"),
            },

            "circle" => new HelpEllipse
            {
                CentreX = Attribute(element, "cx"),
                CentreY = Attribute(element, "cy"),
                RadiusX = Attribute(element, "r"),
                RadiusY = Attribute(element, "r"),
            },

            "ellipse" => new HelpEllipse
            {
                CentreX = Attribute(element, "cx"),
                CentreY = Attribute(element, "cy"),
                RadiusX = Attribute(element, "rx"),
                RadiusY = Attribute(element, "ry"),
            },

            "line" => new HelpLine
            {
                X1 = Attribute(element, "x1"),
                Y1 = Attribute(element, "y1"),
                X2 = Attribute(element, "x2"),
                Y2 = Attribute(element, "y2"),
                Dashes = Dashes(element),
            },

            "polygon" => new HelpPolygon { Points = Points((string?)element.Attribute("points")) },

            "path" => new HelpPath { Data = (string?)element.Attribute("d") ?? string.Empty },

            "text" => new HelpLabel
            {
                X = Attribute(element, "x"),
                Y = Attribute(element, "y"),
                Text = element.Value.Trim(),
                Size = Attribute(element, "font-size", 16),
                Bold = Attribute(element, "font-weight", 400) >= 600,
                Anchor = (string?)element.Attribute("text-anchor") switch
                {
                    "middle" => HelpAnchor.Middle,
                    "end" => HelpAnchor.End,
                    _ => HelpAnchor.Start,
                },
            },

            _ => throw new FormatException(
                $"Help bands may not use <{name}>. The drawable set is rect, circle, ellipse, "
                + "line, polygon, path, text and g."),
        };

        return shape with
        {
            Fill = Colour(element, "fill"),
            Stroke = Colour(element, "stroke"),
            StrokeWidth = Attribute(element, "stroke-width"),
            Opacity = Attribute(element, "opacity", 1),
        };
    }

    private static IReadOnlyList<double> Dashes(XElement element) =>
        (string?)element.Attribute("stroke-dasharray") is { Length: > 0 } pattern
            ? pattern.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(Number).ToArray()
            : [];

    private static IReadOnlyList<HelpPoint> Points(string? points)
    {
        if (points is null)
        {
            return [];
        }

        return points
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(','))
            .Where(pair => pair.Length == 2)
            .Select(pair => new HelpPoint(Number(pair[0]), Number(pair[1])))
            .ToArray();
    }

    /// <summary>
    /// A colour role, from a <c>var(--name)</c> token. Anything else throws: the point of the
    /// token is that the value lives in one place, so a literal here would be a diagram that
    /// ignores the Commander's theme and looks correct to whoever wrote it.
    /// </summary>
    private static HelpColour? Colour(XElement element, string attribute)
    {
        var value = ((string?)element.Attribute(attribute))?.Trim();

        if (value is null or "" or "none")
        {
            return null;
        }

        if (!value.StartsWith("var(--", StringComparison.Ordinal) || !value.EndsWith(')'))
        {
            throw new FormatException(
                $"Help bands may only colour by role: {attribute}=\"{value}\" is a literal. "
                + "Use var(--accent) and the rest of the nine Palette roles.");
        }

        var token = value["var(--".Length..^1];

        return token switch
        {
            "background" => HelpColour.Background,
            "surface" => HelpColour.Surface,
            "surface-alt" => HelpColour.SurfaceAlt,
            "border" => HelpColour.Border,
            "text" => HelpColour.Text,
            "text-muted" => HelpColour.TextMuted,
            "accent" => HelpColour.Accent,
            "accent-muted" => HelpColour.AccentMuted,
            "danger" => HelpColour.Danger,
            "info" => HelpColour.Info,
            _ => throw new FormatException(
                $"--{token} is not one of the nine Palette roles. A browser would draw that as "
                + "nothing at all and say so to nobody."),
        };
    }

    private static double Attribute(XElement element, string name, double fallback = 0) =>
        (string?)element.Attribute(name) is { Length: > 0 } value ? Number(value) : fallback;

    private static double Number(string value) =>
        double.Parse(value, CultureInfo.InvariantCulture);
}
