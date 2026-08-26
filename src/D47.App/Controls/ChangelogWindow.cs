using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// What changed in each release, from inside the build
/// (<a href="https://github.com/dseelinger/d47/issues/50">#50</a>).
/// <para>
/// <b>The whole file, newest first, which is the order it is already written in</b> — the
/// Commander's call, 2026-08-26, over showing the running build's section alone. A changelog is
/// read by scrolling and the top of it is the newest thing; a window that opened on one section
/// would be answering a narrower question than the one being asked.
/// </para>
/// <para>
/// <b>Plain text rather than rendered markdown.</b> <c>HelpLibrary</c> parses banded help articles
/// — front matter, an ELI5 band, anchored sections — and this is a release list that fits none of
/// that. Running it through a parser it does not match would produce either an error or a lie
/// about its shape, and what anybody wants from a changelog is the words in the order they were
/// written.
/// </para>
/// <para>
/// Built in code rather than as an axaml pair, like <see cref="ConfirmWindow"/> and
/// <see cref="SpendWindow"/> beside it: one layout, no state of its own.
/// </para>
/// <para>
/// <b>A second window, and second windows have bitten once.</b> Phase 48: <c>ShutdownMode</c> was
/// the default and d47 had only ever had one window, so closing the panel quit the app by accident
/// of arithmetic. It is <c>OnMainWindowClose</c> in <c>App.axaml.cs</c> now and this inherits that
/// — checked rather than assumed.
/// </para>
/// </summary>
public sealed class ChangelogWindow : Window
{
    /// <summary>
    /// The changelog on GitHub, at the branch rather than at a tag.
    /// <para>
    /// <b>Kept beside the shipped copy rather than replaced by it</b> (#50). The embedded one
    /// reads with no internet, which this never did; this one is the only way to read a release
    /// <em>newer</em> than the one running, which is exactly what a Commander one release behind
    /// is asking for — and is why it points at the branch. Held to the same repository prefix the
    /// update path pins itself to, for the reason recorded there: <c>UseShellExecute</c> resolves
    /// anything, not just http.
    /// </para>
    /// </summary>
    public const string OnlineUrl = "https://github.com/dseelinger/d47/blob/main/CHANGELOG.md";

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

            // Selectable, because the commonest thing anybody does with a changelog entry is
            // quote it back at somebody.
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
