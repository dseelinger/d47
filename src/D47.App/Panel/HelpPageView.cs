﻿using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Help;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>One figure out of a help band, drawn.</summary>
public sealed class HelpFigureView : Control
{
    private readonly HelpFigure _figure;

    private readonly bool _onPlate;

    /// <param name="onPlate">Drawn on a slab plate, where the SurfaceAlt role resolves to the window ground.</param>
    public HelpFigureView(HelpFigure figure, bool onPlate = false)
    {
        _figure = figure;
        _onPlate = onPlate;
    }

    protected override Size MeasureOverride(Size available)
    {
        // Width-led: the height follows from the aspect the viewBox declares, so a figure never decides how
        // much room it takes down the page — the panel's width does.
        var width = double.IsInfinity(available.Width) ? _figure.Width : available.Width;

        return new Size(width, width * (_figure.Height / _figure.Width));
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || _figure.Width <= 0)
        {
            return;
        }

        var scale = Bounds.Width / _figure.Width;

        using var _ = context.PushTransform(Matrix.CreateScale(scale, scale));

        foreach (var shape in _figure.Shapes)
        {
            if (shape.Opacity >= 1)
            {
                Draw(context, shape);
                continue;
            }

            using var fade = context.PushOpacity(shape.Opacity);
            Draw(context, shape);
        }
    }

    private void Draw(DrawingContext context, HelpShape shape)
    {
        var fill = Brush(shape.Fill);
        var pen = Pen(shape);

        switch (shape)
        {
            case HelpRectangle rectangle:
                context.DrawRectangle(
                    fill,
                    pen,
                    new RoundedRect(
                        new Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height),
                        rectangle.Radius));
                break;

            case HelpEllipse ellipse:
                context.DrawEllipse(
                    fill,
                    pen,
                    new Point(ellipse.CentreX, ellipse.CentreY),
                    ellipse.RadiusX,
                    ellipse.RadiusY);
                break;

            // A line with no stroke draws nothing, rather than throwing.
            case HelpLine line when pen is not null:
                context.DrawLine(pen, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
                break;

            case HelpPolygon polygon:
                context.DrawGeometry(fill, pen, Outline(polygon));
                break;

            case HelpPath path:
                context.DrawGeometry(fill, pen, Geometry.Parse(path.Data));
                break;

            case HelpLabel label:
                DrawLabel(context, label);
                break;
        }
    }

    private void DrawLabel(DrawingContext context, HelpLabel label)
    {
        var text = new FormattedText(
            label.Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                TextElement.GetFontFamily(this),
                FontStyle.Normal,
                label.Bold ? FontWeight.Bold : FontWeight.Normal),
            label.Size,
            Brush(label.Fill) ?? Brushes.Transparent);

        var x = label.Anchor switch
        {
            HelpAnchor.Middle => label.X - (text.Width / 2),
            HelpAnchor.End => label.X - text.Width,
            _ => label.X,
        };

        // SVG's y is the baseline; DrawText takes the top-left.
        context.DrawText(text, new Point(x, label.Y - text.Baseline));
    }

    private static StreamGeometry Outline(HelpPolygon polygon)
    {
        var geometry = new StreamGeometry();

        using var figure = geometry.Open();

        if (polygon.Points.Count == 0)
        {
            return geometry;
        }

        figure.BeginFigure(new Point(polygon.Points[0].X, polygon.Points[0].Y), isFilled: true);

        foreach (var point in polygon.Points.Skip(1))
        {
            figure.LineTo(new Point(point.X, point.Y));
        }

        figure.EndFigure(isClosed: true);

        return geometry;
    }

    private Pen? Pen(HelpShape shape)
    {
        if (Brush(shape.Stroke) is not { } stroke || shape.StrokeWidth <= 0)
        {
            return null;
        }

        // Round throughout.
        return new Pen(
            stroke,
            shape.StrokeWidth,
            Dashes(shape),
            PenLineCap.Round,
            PenLineJoin.Round);
    }

    /// <summary>
    /// SVG measures a dash pattern in user units; Avalonia measures it in multiples of the stroke
    /// width.
    /// </summary>
    private static DashStyle? Dashes(HelpShape shape) =>
        shape is HelpLine { Dashes.Count: > 0 } line && shape.StrokeWidth > 0
            ? new DashStyle(line.Dashes.Select(dash => dash / shape.StrokeWidth), 0)
            : null;

    /// <summary>
    /// A role, resolved against the running theme — so a figure follows the Commander's palette,
    /// including the Elite HUD recolouring, without the markup knowing a theme exists.
    /// </summary>
    private IBrush? Brush(HelpColour? role)
    {
        if (role is not { } colour)
        {
            return null;
        }

        var key = colour switch
        {
            HelpColour.Background => ThemeManager.BgKey,
            HelpColour.Surface => ThemeManager.BarKey,
            HelpColour.SurfaceAlt => _onPlate ? ThemeManager.BgKey : ThemeManager.SlabKey,
            HelpColour.Border => ThemeManager.Line2Key,
            HelpColour.Text => ThemeManager.WhiteKey,
            HelpColour.TextMuted => ThemeManager.GreyKey,
            HelpColour.Accent => ThemeManager.AKey,
            HelpColour.AccentMuted => ThemeManager.LineKey,
            HelpColour.Danger => ThemeManager.RedKey,
            _ => ThemeManager.BlueKey,
        };

        return this.TryFindResource(key, out var found) && found is IBrush brush
            ? brush
            : new SolidColorBrush(ThemeManager.TokenColours(Palettes.Elite)[key]);
    }
}

/// <summary>A help page in the panel: the how-to band's steps, then the How it works band's plates.</summary>
public static class HelpPageView
{
    /// <summary>How a help level is keyed.</summary>
    public const string CrumbPrefix = HelpLevel.Prefix;

    /// <summary>The crumb that takes the panel for one capability's help.</summary>
    public static NavCrumb Crumb(string capabilityId) => HelpLevel.For(capabilityId);

    /// <summary>Whether there is anything to show for this capability.</summary>
    public static bool Exists(string? capabilityId) => HelpLibrary.Has(capabilityId);

    /// <summary>Draws the page a help crumb names, or says plainly that there is not one yet.</summary>
    /// <param name="openUrl">How this surface reaches the web, or null where it cannot.</param>
    /// <param name="openSettings">
    /// How this surface reaches a settings section, or null where it has no Settings tab — which the
    /// headset does not, and which is why this is a handed-in action rather than a call.
    /// </param>
    public static Control Build(
        NavCrumb crumb, PanelNavigator nav, Action<string>? openUrl, Action<string>? openSettings = null)
    {
        var id = HelpLevel.CapabilityOf(crumb);

        var howTo = HelpLibrary.HowTo(id);
        var works = HelpLibrary.For(id);

        return howTo is null && works is null
            ? Missing(id)
            : Build(howTo, works, nav, openUrl, openSettings);
    }

    /// <summary>A page with a How it works band and no how-to band.</summary>
    public static Control Build(
        HelpArticle article, PanelNavigator nav, Action<string>? openUrl, Action<string>? openSettings = null) =>
        Build(null, article, nav, openUrl, openSettings);

    /// <summary>The title, the how-to band, the How it works band, then where to go next; a missing band is left out.</summary>
    public static Control Build(
        HelpArticle? howTo,
        HelpArticle? works,
        PanelNavigator nav,
        Action<string>? openUrl,
        Action<string>? openSettings = null)
    {
        var lead = howTo ?? works ?? throw new ArgumentException("A help page needs at least one band.");

        var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 24) };

        var (title, _) = RoutingKit.Title(lead.Title);
        title.Margin = new Thickness(0, 0, 0, 24);
        stack.Children.Add(title);

        if (Drawn(howTo) is { } steps)
        {
            stack.Children.Add(StarSystemPage.GroupHead(
                "How to use it", "What to do, in order, and what happens when you do."));
            stack.Children.Add(HowToBand(steps));
        }

        if (Drawn(works) is { } plates)
        {
            stack.Children.Add(StarSystemPage.GroupHead(
                "How it works", "Why it behaves the way it does. Nothing here to do."));
            stack.Children.Add(HowItWorksBand(plates));
        }

        if (Everything(lead, nav) is { } index)
        {
            stack.Children.Add(index);
        }

        var links = works is { Links.Count: > 0 } ? works.Links : howTo?.Links ?? [];
        stack.Children.Add(Next(lead.CapabilityId, links, nav, openUrl, openSettings));

        return new ScrollViewer
        {
            Name = "HelpScroller",
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = stack,
        };
    }

    /// <summary>The band, when it has anything to draw.</summary>
    private static HelpArticle? Drawn(HelpArticle? band) =>
        band is { } article && (article.Intro.Length > 0 || article.Sections.Count > 0) ? article : null;

    private static StackPanel HowToBand(HelpArticle band)
    {
        var block = new StackPanel { Spacing = 40, Margin = new Thickness(0, 22, 0, 64) };

        if (band.Intro.Length > 0)
        {
            block.Children.Add(Typeset.Prose(band.Intro, TypeScale.Figure, ThemeManager.WhiteKey, FontWeight.Bold, wrap: true));
        }

        foreach (var section in band.Sections)
        {
            block.Children.Add(Step(section));
        }

        return block;
    }

    private static StackPanel HowItWorksBand(HelpArticle band)
    {
        var block = new StackPanel { Spacing = 2, Margin = new Thickness(0, 22, 0, 64) };

        if (band.Intro.Length > 0)
        {
            block.Children.Add(Typeset.Prose(
                band.Intro, TypeScale.Figure, ThemeManager.GreyKey, wrap: true, margin: new Thickness(0, 0, 0, 20)));
        }

        foreach (var section in band.Sections)
        {
            block.Children.Add(Plate(section));
        }

        return block;
    }

    /// <summary>A how-to step on the window ground: badge and heading, the YOU and THEN rows, the figure.</summary>
    private static StackPanel Step(HelpSection section)
    {
        var step = new StackPanel();

        step.Children.Add(Heading(section));

        if (section.Say is { Length: > 0 } || section.Expect is { Length: > 0 })
        {
            var lines = new StackPanel { Spacing = 2, Margin = new Thickness(0, 12, 0, 0) };

            if (section.Say is { Length: > 0 } say)
            {
                lines.Children.Add(Line("You", say, ThemeManager.CyanKey, ThemeManager.CyanGroundKey, ThemeManager.CyanKey));
            }

            if (section.Expect is { Length: > 0 } expect)
            {
                lines.Children.Add(Line("Then", expect, ThemeManager.GreyKey, ThemeManager.SlabKey, rule: null));
            }

            step.Children.Add(lines);
        }

        if (section.Figure is { } figure)
        {
            step.Children.Add(new HelpFigureView(figure) { Margin = new Thickness(0, 12, 0, 0) });
        }

        if (section.Body is { Length: > 0 } body)
        {
            step.Children.Add(Body(body, new Thickness(0, 12, 0, 0)));
        }

        return step;
    }

    /// <summary>One YOU or THEN row: a 64 px label column and the line, behind a 3 px left rule.</summary>
    private static Border Line(string label, string text, string labelKey, string groundKey, string? rule)
    {
        const double LabelSize = 12;

        var tag = Typeset.Chrome(
            label.ToUpperInvariant(), LabelSize, labelKey, FontWeight.SemiBold, tracking: LabelSize * 0.08,
            vertical: VerticalAlignment.Top, margin: new Thickness(0, 5, 0, 0));

        var words = Typeset.Prose(text, TypeScale.Body, ThemeManager.WhiteKey, wrap: true);
        words.LineHeight = TypeScale.Body * 1.5;
        Grid.SetColumn(words, 2);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("64,12,*") };
        grid.Children.Add(tag);
        grid.Children.Add(words);

        var row = new Border
        {
            Padding = new Thickness(13, 11, 16, 11),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = Brushes.Transparent,
            Child = grid,
        };

        ThemeInk.Bind(row, Border.BackgroundProperty, groundKey);

        if (rule is not null)
        {
            ThemeInk.Bind(row, Border.BorderBrushProperty, rule);
        }

        return row;
    }

    /// <summary>A How it works plate: a slab panel holding a two-digit number, the heading, the figure and the body.</summary>
    private static Border Plate(HelpSection section)
    {
        var plate = new StackPanel { Spacing = 14 };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };

        if (section.Number.Length > 0)
        {
            head.Children.Add(Typeset.Mono(
                int.TryParse(section.Number, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                    ? number.ToString("00", CultureInfo.InvariantCulture)
                    : section.Number,
                TypeScale.Translation,
                ThemeManager.GreyKey,
                vertical: VerticalAlignment.Top,
                margin: new Thickness(0, 2, 14, 0)));
        }

        var heading = Typeset.Prose(section.Heading, TypeScale.Figure, ThemeManager.WhiteKey, FontWeight.Bold, wrap: true);
        Grid.SetColumn(heading, 1);
        head.Children.Add(heading);
        plate.Children.Add(head);

        if (section.Figure is { } figure)
        {
            plate.Children.Add(new HelpFigureView(figure, onPlate: true));
        }

        if (section.Body is { Length: > 0 } body)
        {
            plate.Children.Add(Body(body, default));
        }

        var panel = new Border { Padding = new Thickness(24, 20, 24, 24), Child = plate };
        ThemeInk.Bind(panel, Border.BackgroundProperty, ThemeManager.SlabKey);

        return panel;
    }

    private static TextBlock Body(string text, Thickness margin)
    {
        var prose = Typeset.Prose(text, TypeScale.Body, ThemeManager.GreyKey, wrap: true, margin: margin);
        prose.LineHeight = TypeScale.Body * 1.5;

        return prose;
    }

    /// <summary>The step's badge and heading; a step numbered <c>!</c> takes the warning badge.</summary>
    private static Grid Heading(HelpSection section)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };

        if (section.Number.Length > 0)
        {
            var number = new TextBlock
            {
                Text = section.Number,
                FontSize = TypeScale.Subheading,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            ThemeInk.Bind(number, TextBlock.ForegroundProperty, ThemeManager.KnockKey);

            // 32 px, which is over the 30 px touch floor the checklist settled on — it is not pressable, but
            // a badge smaller than the things beside it reads as an afterthought on a quad a metre away.
            var badge = new Border
            {
                Width = 32,
                Height = 32,
                Child = number,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 12, 0),
            };

            ThemeInk.Bind(badge, Border.BackgroundProperty, section.Number == "!" ? ThemeManager.WarnKey : ThemeManager.AKey);
            row.Children.Add(badge);
        }

        var heading = Typeset.Prose(
            section.Heading, TypeScale.Figure + 1, ThemeManager.WhiteKey, FontWeight.Bold, wrap: true,
            vertical: VerticalAlignment.Center);
        Grid.SetColumn(heading, 1);
        row.Children.Add(heading);

        return row;
    }

    /// <summary>Everything this build can draw, on the one page that is about help itself.</summary>
    private static Control? Everything(HelpArticle article, PanelNavigator nav)
    {
        if (!string.Equals(article.CapabilityId, HelpLevel.Index, StringComparison.Ordinal))
        {
            return null;
        }

        var pages = HelpLibrary.Pages
            .Where(id => !string.Equals(id, HelpLevel.Index, StringComparison.Ordinal))
            .Select(HelpLibrary.Lead)
            .OfType<HelpArticle>()
            .OrderBy(page => page.NavOrder)
            .ToList();

        if (pages.Count == 0)
        {
            return null;
        }

        var block = new StackPanel { Spacing = 2, Margin = new Thickness(0, 24, 0, 0) };

        block.Children.Add(LoadoutPages.Section("Everything drawn in here"));

        foreach (var group in pages.GroupBy(page => page.Group, StringComparer.Ordinal))
        {
            var heading = TitleText.Build(
                group.Key.Length > 0 ? group.Key : "Elsewhere", TypeScale.Meta, TitleRank.Group);

            ThemeInk.Bind(heading, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            heading.Margin = new Thickness(0, 10, 0, 4);
            block.Children.Add(heading);

            foreach (var page in group)
            {
                var id = page.CapabilityId;
                block.Children.Add(Pressable(page.Title, HelpLibrary.IndexLine(id), () => nav.Take(Crumb(id))));
            }
        }

        return block;
    }

    /// <summary>Where to go next: what the bands name, and then the page they are the short form of.</summary>
    private static StackPanel Next(
        string capabilityId,
        IReadOnlyList<HelpLink> links,
        PanelNavigator nav,
        Action<string>? openUrl,
        Action<string>? openSettings)
    {
        var block = new StackPanel { Spacing = 2 };

        var head = StarSystemPage.GroupHead("Where to go next", "Pages that pick up where this one stops.");
        head.Margin = new Thickness(0, 0, 0, 12);
        block.Children.Add(head);

        foreach (var link in links)
        {
            block.Children.Add(Card(link, nav, openUrl, openSettings));
        }

        block.Children.Add(Card(
            new HelpLink
            {
                // Named for where it goes rather than for what it holds (asked for 2026-08-23). "Read the
                // full page" did not say that pressing it leaves the app — which is the one thing about this
                // card a Commander needs before they press it, and the whole story in a headset, where it
                // cannot be pressed and is drawn as a bare address.
                Title = "More details online",
                Blurb = "Everything this leaves out: the detail, the tables and the working.",
                Href = DocsSite.Page(capabilityId),
            },
            nav,
            openUrl,
            openSettings));

        return block;
    }

    /// <summary>One entry, drawn as whatever this surface can actually do with it.</summary>
    private static Control Card(
        HelpLink link, PanelNavigator nav, Action<string>? openUrl, Action<string>? openSettings)
    {
        if (openSettings is not null && link.Settings is { Length: > 0 } section)
        {
            return Pressable(link.Title, link.Blurb, () => openSettings(section));
        }

        if (link.Article is { } id && Exists(id))
        {
            return Pressable(link.Title, link.Blurb, () => nav.Take(Crumb(id)));
        }

        var address = Address(link);

        return openUrl is null
            ? Written(link.Title, link.Blurb, address)
            : Pressable(link.Title, link.Blurb, () => openUrl(address));
    }

    /// <summary>Where a link points, as something a browser can open.</summary>
    private static string Address(HelpLink link)
    {
        if (link.Article is { } id)
        {
            // The general pages live a folder up from the capabilities, which is where their ids came from
            // and where their addresses have to point back to.
            return DocsSite.Page(id);
        }

        var href = link.Href ?? string.Empty;

        if (href.StartsWith("http", StringComparison.Ordinal))
        {
            return href;
        }

        // A path up out of the capability folder reaches the general help pages.
        return href.StartsWith("../", StringComparison.Ordinal)
            ? DocsSite.Root + href[3..]
            : DocsSite.Root + "capabilities/" + href;
    }

    private static Control Pressable(string title, string? blurb, Action pressed)
    {
        var button = ListRow.Dress(new Button
        {
            Content = Stacked(title, blurb),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });

        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>A link on a surface that cannot follow it.</summary>
    private static Control Written(string title, string? blurb, string address)
    {
        var stack = (StackPanel)Stacked(title, blurb);

        var written = new TextBlock
        {
            Text = address,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        ThemeInk.Bind(written, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        stack.Children.Add(written);

        return ListRow.Dress(new Border { Child = stack });
    }

    private static Control Stacked(string title, string? blurb)
    {
        var stack = new StackPanel { Spacing = 3 };

        var heading = ListRow.Name(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });

        stack.Children.Add(heading);

        if (blurb is { Length: > 0 })
        {
            var line = ListRow.SecondaryInk(new TextBlock
            {
                Text = blurb,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            });

            stack.Children.Add(line);
        }

        return stack;
    }

    /// <summary>A capability whose page has neither band yet.</summary>
    private static Control Missing(string id)
    {
        var text = new TextBlock
        {
            Text = $"There is no short-form help for {id} yet. The full page is on the documentation site.",
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };

        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return text;
    }
}
