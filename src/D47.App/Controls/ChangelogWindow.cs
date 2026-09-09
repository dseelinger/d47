using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>What changed in each release, from inside the build (#50).</summary>
public sealed class ChangelogWindow : Window
{
    /// <summary>The changelog on GitHub, at the branch rather than at a tag.</summary>
    public const string OnlineUrl = "https://github.com/dseelinger/d47/blob/main/CHANGELOG.md";

    /// <summary>
    /// The community page, which is where the Discord invite lives — deliberately not the invite
    /// itself.
    /// </summary>
    public const string CommunityUrl = "https://dseelinger.github.io/d47/community.html";

    public ChangelogWindow(string text)
    {
        Title = "What changed";
        Width = 760;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var body = new TextBlock
        {
            Name = "ChangelogText",
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,

            // Selectable, because the commonest thing anybody does with a changelog entry is quote it back at
            // somebody.
            [TextBlock.TextAlignmentProperty] = TextAlignment.Left,
        };

        Themed(body, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var close = new Button
        {
            Content = "Close",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };

        close.Click += (_, _) => Close();

        var stack = new StackPanel
        {
            Margin = new Thickness(24),
            Children = { body, close },
        };

        Content = new ScrollViewer
        {
            Name = "ChangelogScroller",
            Content = stack,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        Opened += (_, _) => close.Focus();
    }

    private IDisposable Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));
}
