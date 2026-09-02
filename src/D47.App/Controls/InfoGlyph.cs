using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace D47.App.Controls;

/// <summary>
/// The <c>ⓘ</c> a pop-up window carries beside its <c>?</c>
/// (<a href="https://github.com/dseelinger/d47/issues/269">#269</a>): the reasoning behind what the
/// window is asking for, one press away, without leaving the window.
/// <para>
/// <b>It exists because #252 could only trim half.</b> That issue moved the mechanism to the site
/// and kept every fact on the surface, which was right and still left a paragraph of *why* attached
/// to each fact. Reasoning is not a disclosure — a Commander who never reads it has still been told
/// what leaves and where it goes — so it belongs behind a press. What it must never hold is a
/// disclosure; see <c>HelpImproveWindow.IntroText</c> for the line that rule is drawn on.
/// </para>
/// <para>
/// <b>Three levels, increasing in detail, and the glyphs say which is which.</b> The intro is the
/// facts, this flyout is the reasoning, and the <c>?</c> beside it opens the whole page with its
/// diagrams. So this flyout ends by pointing at that page rather than repeating it — one authored
/// copy, on the site, exactly as <see cref="SiteHelpMark"/> already assumed.
/// </para>
/// <para>
/// <b>A flyout rather than a second dialog</b>, because a dialog over a dialog is a stack a
/// Commander has to unwind, and because dismissing this one must cost nothing: whatever they had
/// chosen on the window — a scale, a toggle, a corpus that took minutes to read — is still there
/// behind it. That is the same argument <see cref="SiteHelpMark"/> records for not dismissing the
/// dialog to reach the help level.
/// </para>
/// </summary>
public static class InfoGlyph
{
    /// <summary>
    /// An <c>ⓘ</c> whose flyout shows <paramref name="text"/> and offers <paramref name="url"/>.
    /// </summary>
    /// <param name="name">
    /// The control's name, so a test can find this particular glyph. Windows have more than one
    /// button and none of them is reachable by role alone — the same reason
    /// <see cref="SiteHelpMark.For"/> takes one.
    /// </param>
    public static Button For(string text, string url, string name)
    {
        // Wrapped and capped, because this is paragraphs rather than a label: an uncapped flyout
        // measures its content unconstrained and lays the whole thing out on one line.
        var body = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = Theming.TypeScale.Secondary,
            MaxWidth = 460,
        };

        // The site is where the full page lives, so the flyout names it rather than growing into
        // it. The address is the tooltip for the reason SiteHelpMark records: a control that
        // launches a browser says where it is going before it goes.
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

    /// <summary>
    /// The theme's presenter, with sideways scrolling turned off
    /// (<a href="https://github.com/dseelinger/d47/issues/271">#271</a>).
    /// <para>
    /// The presenter caps its own width — 456 in Fluent, and 430 of that is left for content once
    /// its padding and border are off — and then puts the content in a scroll viewer that scrolls
    /// sideways when the content asks for more. So the cap above was never the one that bound: the
    /// text wrapped at 460 and was shown through a hole 430 wide, clipping every line and hanging a
    /// horizontal scrollbar under paragraphs a Commander was meant to read. With the sideways
    /// scroll disabled the viewer measures its content at the width it will show, and the text
    /// wraps there. The cap above stays as the guard for a theme that has no presenter to base
    /// this on, where the flyout is the stock one and would otherwise measure unconstrained.
    /// </para>
    /// </summary>
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
