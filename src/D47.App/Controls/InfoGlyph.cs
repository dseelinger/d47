using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace D47.App.Controls;

/// <summary>
/// The <c>ⓘ</c> a pop-up window carries beside its <c>?</c> (#269): the reasoning behind what the
/// window is asking for, one press away, without leaving the window.
/// </summary>
public static class InfoGlyph
{
    /// <summary>
    /// An <c>ⓘ</c> whose flyout shows <paramref name="text"/> and offers <paramref name="url"/>.
    /// </summary>
    /// <paramref name="text"/>and offers <paramref name="url"/>.</paramref>
    public static Button For(string text, string url, string name)
    {
        // Wrapped and capped, because this is paragraphs rather than a label: an uncapped flyout measures its
        // content unconstrained and lays the whole thing out on one line.
        var body = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = Theming.TypeScale.Secondary,
            MaxWidth = 460,
        };

        // The site is where the full page lives, so the flyout names it rather than growing into it.
        var more = new Button
        {
            Name = name + "More",
            Content = "Read the full page on the website",
            FontSize = Theming.TypeScale.Small,
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            [ToolTip.TipProperty] = url,
        };

        more.Click += (_, _) => SiteHelpMark.Open(url);

        var glyph = new Button
        {
            Name = name,
            Content = "ⓘ",
            FontSize = Theming.TypeScale.Secondary,
            Padding = new Thickness(7, 1),
            VerticalAlignment = VerticalAlignment.Center,
            [ToolTip.TipProperty] = "Why this helps, and what the scrub does",
            Flyout = new Flyout
            {
                Placement = PlacementMode.BottomEdgeAlignedRight,
                FlyoutPresenterTheme = WrapsRatherThanScrolls(),
                Content = new StackPanel
                {
                    MaxWidth = 460,
                    Children = { body, more },
                },
            },
        };

        return glyph;
    }

    /// <summary>The theme's presenter, with sideways scrolling turned off (#271).</summary>
    private static ControlTheme? WrapsRatherThanScrolls()
    {
        var application = Application.Current;

        if (application is null
            || !application.TryGetResource(typeof(FlyoutPresenter), application.ActualThemeVariant, out var found)
            || found is not ControlTheme stock)
        {
            return null;
        }

        return new ControlTheme(typeof(FlyoutPresenter))
        {
            BasedOn = stock,
            Setters =
            {
                new Setter(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled),
            },
        };
    }
}
