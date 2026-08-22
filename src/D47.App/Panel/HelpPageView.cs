using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Help;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// One figure out of a help band, drawn.
/// <para>
/// A drawn control rather than a tree of shapes. Text is the reason: SVG places a label by its
/// baseline and can anchor it by its middle or its end, and both of those need the text measured
/// before it is positioned. A <see cref="FormattedText"/> answers that exactly, where a
/// <c>TextBlock</c> on a <c>Canvas</c> would have to be laid out first and nudged afterwards.
/// </para>
/// <para>
/// It scales to whatever width it is given, because the figure carries a viewBox rather than
/// pixels. That is what lets one drawing serve a desktop window, a 1024-pixel quad a metre away,
/// and a phone reading the same markup on the published site.
/// </para>
/// </summary>
public sealed class HelpFigureView : Control
{
    private readonly HelpFigure _figure;

    public HelpFigureView(HelpFigure figure) => _figure = figure;

    protected override Size MeasureOverride(Size available)
    {
        // Width-led: the height follows from the aspect the viewBox declares, so a figure never
        // decides how much room it takes down the page — the panel's width does.
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

            // A line with no stroke draws nothing, rather than throwing. It is malformed markup,
            // but a blank space in one diagram is not worth taking the panel down for.
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

        // SVG's y is the baseline; DrawText takes the top-left. Without this every label in
        // every band sits about one line too low, which reads as bad spacing rather than as a
        // bug and would have been "corrected" by moving the numbers in the markup.
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

        // Round throughout. Every band draws with round caps and joins, so carrying the two
        // attributes through the model would be two more words in the vocabulary and no choice
        // anybody has yet wanted to make.
        return new Pen(
            stroke,
            shape.StrokeWidth,
            Dashes(shape),
            PenLineCap.Round,
            PenLineJoin.Round);
    }

    /// <summary>
    /// SVG measures a dash pattern in user units; Avalonia measures it in multiples of the stroke
    /// width. Dividing is the whole difference, and getting it wrong makes a 2.5-wide dashed
    /// border look solid.
    /// </summary>
    private static DashStyle? Dashes(HelpShape shape) =>
        shape is HelpLine { Dashes.Count: > 0 } line && shape.StrokeWidth > 0
            ? new DashStyle(line.Dashes.Select(dash => dash / shape.StrokeWidth), 0)
            : null;

    /// <summary>
    /// A role, resolved against the running theme — so a figure follows the Commander's palette,
    /// including the Elite HUD recolouring, without the markup knowing a theme exists.
    /// <para>
    /// The fallback is not decoration. A headless render has none of the application's resources
    /// (list.md Phase 39), so without it every captured figure would be blank and the captures
    /// that prove layout would prove nothing.
    /// </para>
    /// </summary>
    private IBrush? Brush(HelpColour? role)
    {
        if (role is not { } colour)
        {
            return null;
        }

        var key = colour switch
        {
            HelpColour.Background => ThemeManager.BackgroundKey,
            HelpColour.Surface => ThemeManager.SurfaceKey,
            HelpColour.SurfaceAlt => ThemeManager.SurfaceAltKey,
            HelpColour.Border => ThemeManager.BorderKey,
            HelpColour.Text => ThemeManager.TextKey,
            HelpColour.TextMuted => ThemeManager.TextMutedKey,
            HelpColour.Accent => ThemeManager.AccentKey,
            HelpColour.AccentMuted => ThemeManager.AccentMutedKey,
            HelpColour.Danger => ThemeManager.DangerKey,
            _ => ThemeManager.InfoKey,
        };

        return this.TryFindResource(key, out var found) && found is IBrush brush
            ? brush
            : new SolidColorBrush(Fallback(colour));
    }

    private static Color Fallback(HelpColour role)
    {
        var palette = Palettes.Elite;

        return role switch
        {
            HelpColour.Background => palette.Background,
            HelpColour.Surface => palette.Surface,
            HelpColour.SurfaceAlt => palette.SurfaceAlt,
            HelpColour.Border => palette.Border,
            HelpColour.Text => palette.Text,
            HelpColour.TextMuted => palette.TextMuted,
            HelpColour.Accent => palette.Accent,
            HelpColour.AccentMuted => palette.AccentMuted,
            HelpColour.Danger => palette.Danger,
            _ => palette.Info,
        };
    }
}

/// <summary>
/// A whole help band as a panel page: the lede, then the numbered steps with their pictures.
/// <para>
/// Drawn over whatever the Commander was looking at rather than as a tab of its own, because the
/// question is always about <em>this</em> page (asked for 2026-08-22). It is pushed as a modal
/// crumb, so every route that would navigate away is refused while it is up and
/// <see cref="PanelNavigator.Back"/> dismisses it — which is the breadcrumb, the controller button
/// and the spoken word already agreeing, with nothing here to arrange.
/// </para>
/// </summary>
public static class HelpPageView
{
    /// <summary>How a help level is keyed, so the page rebuilds from the trail alone.</summary>
    public const string CrumbPrefix = "help:";

    /// <summary>The crumb that takes the panel for one capability's help.</summary>
    public static NavCrumb Crumb(string capabilityId) =>
        new(CrumbPrefix + capabilityId, "Help");

    /// <summary>Whether there is anything to show for this capability.</summary>
    public static bool Exists(string? capabilityId) =>
        capabilityId is { Length: > 0 } id && HelpLibrary.For(id) is not null;

    /// <summary>Draws the band a help crumb names, or says plainly that there is not one yet.</summary>
    /// <param name="openUrl">
    /// How this surface reaches the web, or null where it cannot. The headset is the null: a link
    /// it can do nothing with is drawn as its address rather than as a control that does nothing,
    /// which is the rule <see cref="IFilterablePage"/> already records about the search box.
    /// </param>
    public static Control Build(NavCrumb crumb, PanelNavigator nav, Action<string>? openUrl)
    {
        var id = crumb.Key.StartsWith(CrumbPrefix, StringComparison.Ordinal)
            ? crumb.Key[CrumbPrefix.Length..]
            : crumb.Key;

        return HelpLibrary.For(id) is { } article ? Build(article, nav, openUrl) : Missing(id);
    }

    public static Control Build(HelpArticle article, PanelNavigator nav, Action<string>? openUrl)
    {
        var stack = new StackPanel { Spacing = 26, Margin = new Thickness(0, 4, 0, 24) };

        if (article.Lede.Length > 0)
        {
            var lede = new TextBlock
            {
                Text = article.Lede,
                FontSize = TypeScale.Subheading + 2,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            };

            LoadoutPages.Themed(lede, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
            stack.Children.Add(lede);
        }

        foreach (var section in article.Sections)
        {
            stack.Children.Add(Step(section));
        }

        stack.Children.Add(Next(article, nav, openUrl));

        return new ScrollViewer
        {
            Name = "HelpScroller",
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = stack,
        };
    }

    private static Control Step(HelpSection section)
    {
        var step = new StackPanel { Spacing = 12 };

        step.Children.Add(Heading(section));

        if (section.Figure is { } figure)
        {
            step.Children.Add(new HelpFigureView(figure));
        }

        if (section.Body is { Length: > 0 } body)
        {
            var prose = new TextBlock
            {
                Text = body,
                FontSize = TypeScale.Body,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = TypeScale.Body * 1.5,
            };

            LoadoutPages.Themed(prose, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            step.Children.Add(prose);
        }

        return step;
    }

    private static Control Heading(HelpSection section)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

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

            LoadoutPages.Themed(number, TextBlock.ForegroundProperty, ThemeManager.BackgroundKey);

            // 32 px, which is over the 30 px touch floor the checklist settled on — it is not
            // pressable, but a badge smaller than the things beside it reads as an afterthought
            // on a quad a metre away.
            var badge = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Child = number,
                VerticalAlignment = VerticalAlignment.Top,
            };

            LoadoutPages.Themed(badge, Border.BackgroundProperty, ThemeManager.AccentKey);
            row.Children.Add(badge);
        }

        var heading = new TextBlock
        {
            Text = section.Heading,
            FontSize = TypeScale.Heading,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        LoadoutPages.Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        row.Children.Add(heading);

        return row;
    }

    /// <summary>
    /// Where to go next: what the band names, and then the page it is the short form of.
    /// <para>
    /// <b>The last one is not decoration.</b> The panel draws the band and nothing under it, so
    /// every word of the reference half — the tables, the tool schemas, the working — exists only
    /// on the site. A band with no way through to it would be a help page that quietly hides the
    /// documentation.
    /// </para>
    /// </summary>
    private static Control Next(HelpArticle article, PanelNavigator nav, Action<string>? openUrl)
    {
        var block = new StackPanel { Spacing = 8, Margin = new Thickness(0, 18, 0, 0) };

        var caption = new TextBlock
        {
            Text = "WHERE TO GO NEXT",
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };

        LoadoutPages.Themed(caption, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        block.Children.Add(caption);

        foreach (var link in article.Links)
        {
            block.Children.Add(Card(link, nav, openUrl));
        }

        block.Children.Add(Card(
            new HelpLink
            {
                Title = "Read the full page",
                Blurb = "Everything this leaves out: the detail, the tables and the working.",
                Href = DocsSite.Capability(article.CapabilityId),
            },
            nav,
            openUrl));

        return block;
    }

    /// <summary>
    /// One entry, drawn as whatever this surface can actually do with it.
    /// <para>
    /// A sibling page that is <em>already on this machine</em> becomes another level of help — so
    /// following a link in the headset is a drill, and going back from it is the same word as
    /// going back from anything else. Everything else is an address: a button where there is a
    /// browser, and the address itself where there is not.
    /// </para>
    /// </summary>
    private static Control Card(HelpLink link, PanelNavigator nav, Action<string>? openUrl)
    {
        if (link.Article is { } id && Exists(id))
        {
            return Pressable(link.Title, link.Blurb, () => nav.Take(Crumb(id)));
        }

        var address = Address(link);

        return openUrl is null
            ? Written(link.Title, link.Blurb, address)
            : Pressable(link.Title, link.Blurb, () => openUrl(address));
    }

    /// <summary>
    /// Where a link points, as something a browser can open. A sibling with no band yet still has
    /// a page on the site, which is the honest fallback — and the day somebody writes that band
    /// the same link becomes a drill with no edit here.
    /// </summary>
    private static string Address(HelpLink link)
    {
        if (link.Article is { } id)
        {
            return DocsSite.Capability(id);
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
        var button = new Button
        {
            Content = Stacked(title, blurb, ThemeManager.AccentKey),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(12, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,

            // The ray floor, as everything pressable on this surface has.
            MinHeight = 30,
        };

        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>
    /// A link on a surface that cannot follow it. The address is shown rather than hidden behind
    /// a control, because a Commander with a headset on can read it and type it later — and a
    /// button that does nothing costs them the time to find that out.
    /// </summary>
    private static Control Written(string title, string? blurb, string address)
    {
        var stack = (StackPanel)Stacked(title, blurb, ThemeManager.TextKey);

        var written = new TextBlock
        {
            Text = address,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        LoadoutPages.Themed(written, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        stack.Children.Add(written);

        stack.Margin = new Thickness(12, 8);

        return stack;
    }

    private static Control Stacked(string title, string? blurb, string titleRole)
    {
        var stack = new StackPanel { Spacing = 3 };

        var heading = new TextBlock
        {
            Text = title,
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        };

        LoadoutPages.Themed(heading, TextBlock.ForegroundProperty, titleRole);
        stack.Children.Add(heading);

        if (blurb is { Length: > 0 })
        {
            var line = new TextBlock
            {
                Text = blurb,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            };

            LoadoutPages.Themed(line, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            stack.Children.Add(line);
        }

        return stack;
    }

    /// <summary>
    /// A capability whose page has no band yet. Says so, rather than opening empty: most pages
    /// are in this state and will be for a while, and an empty panel reads as a fault.
    /// </summary>
    private static Control Missing(string id)
    {
        var text = new TextBlock
        {
            Text = $"There is no short-form help for {id} yet. The full page is on the documentation site.",
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };

        LoadoutPages.Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        return text;
    }
}
