using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Updates;

namespace D47.App.Controls;

/// <summary>
/// What a local build worked, listed from inside it
/// (<a href="https://github.com/dseelinger/d47/issues/207">#207</a>).
/// <para>
/// <b>A build cut from a working tree is a build for testing, and this says what to test.</b> The
/// badge already said the build was local; what it could not say is which changes are in it. The
/// list is the issues the commits since the newest tag say they close, stamped in at publish time
/// because nothing in a running d47 can discover them — see <see cref="LocalBuildNotes"/>.
/// </para>
/// <para>
/// <b>Each number behaves the way GitHub's own reference chip does</b>: hovering shows the state,
/// the <c>owner/repo #number</c> line, the title and the labels; clicking opens the issue in a
/// browser. Baked rather than fetched on hover, so it is instant, works offline and cannot render
/// text that arrived after the build was made.
/// </para>
/// <para>
/// <b>The avatar GitHub's card carries is deliberately left out.</b> It is the one element that
/// needs a download and says the least — state, reference, title and labels carry the whole
/// meaning, and a build that draws nothing from the network is a build whose popup opens the same
/// way on a machine with no internet.
/// </para>
/// <para>
/// Built in code rather than as an axaml pair, beside <see cref="ChangelogWindow"/>,
/// <see cref="CoverageWindow"/> and <see cref="SpendWindow"/>: one layout, no state of its own.
/// </para>
/// </summary>
public sealed class LocalBuildWindow : Window
{
    /// <summary>
    /// The list, or the sentence that goes where a list would.
    /// </summary>
    /// <param name="stamp">
    /// The whole version string — <c>0.92.0-local+8b21b3d</c> — rather than the semantic part.
    /// This is the one window whose entire subject is which binary is running, so it shows the
    /// stamp About shows rather than the one the title bar does.
    /// </param>
    public LocalBuildWindow(string stamp, IReadOnlyList<LocalBuildIssue> worked)
    {
        Title = "This build";
        Width = 640;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var stack = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 10,
            Children =
            {
                Heading("Built from a working tree"),
                Muted(stamp, TypeScale.Secondary),
            },
        };

        if (worked.Count == 0)
        {
            // Plainly, rather than an empty box. A local build cut from a tree whose commits named
            // no issue is an ordinary thing, and a window that opened on nothing would read as the
            // feature being broken.
            stack.Children.Add(Muted(
                "No commit in this build names an issue, so there is nothing to list.",
                TypeScale.Body));
        }
        else
        {
            stack.Children.Add(Muted(
                worked.Count == 1 ? "It worked one issue:" : $"It worked {worked.Count} issues:",
                TypeScale.Body));

            foreach (var issue in worked)
            {
                stack.Children.Add(Bullet(issue));
            }
        }

        // On every path, full and empty alike. The caveat is a property of how the list is
        // gathered rather than of what happens to be in it, so a reader is owed it either way —
        // and it is the sentence that keeps an empty list from reading as "nothing was done".
        stack.Children.Add(Muted(LocalBuildNotes.Caveat, TypeScale.Small));

        var close = new Button
        {
            Content = "Close",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
        };

        close.Click += (_, _) => Close();
        stack.Children.Add(close);

        Content = new ScrollViewer
        {
            Name = "LocalBuildScroller",
            Content = stack,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        Opened += (_, _) => close.Focus();
    }

    /// <summary>
    /// One issue: the chip, then enough of a line to recognise what was attempted without opening
    /// anything — which is the point of the build.
    /// </summary>
    private static Control Bullet(LocalBuildIssue issue)
    {
        var said = issue.Title is { Length: > 0 } title
            ? title
            : "Title withheld — it was written by somebody the Commander has not vouched for, "
              + "or GitHub could not be asked when this build was made.";

        var words = new TextBlock
        {
            Text = said,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(words, TextBlock.ForegroundProperty,
            issue.Title is { Length: > 0 } ? ThemeManager.TextKey : ThemeManager.TextMutedKey);

        var row = new DockPanel { Margin = new Thickness(6, 0, 0, 0) };
        var chip = Chip(issue);

        DockPanel.SetDock(chip, Dock.Left);
        row.Children.Add(chip);
        row.Children.Add(words);

        return row;
    }

    /// <summary>
    /// The number, as GitHub draws one: a link out, with the card on hover.
    /// <para>
    /// A <c>Button</c> rather than a hyperlink control, because that is what every other
    /// out-to-the-browser affordance in this app already is — <see cref="CoverageWindow"/>'s help
    /// mark, the changelog's online link — and one shape for one act is worth more than a control
    /// that looks like the web.
    /// </para>
    /// </summary>
    private static Control Chip(LocalBuildIssue issue)
    {
        var chip = new Button
        {
            Name = "LocalBuildIssue",
            Content = $"#{issue.Number}",
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(8, 2),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand),
            [ToolTip.TipProperty] = Hovercard(issue),
        };

        // Built from the number rather than from anything stamped, so a list baked at publish time
        // cannot carry a link anywhere else. UseShellExecute resolves whatever it is given, which
        // is exactly why the string it is given is not somebody else's to write.
        chip.Click += (_, _) => Process.Start(new ProcessStartInfo(issue.Url) { UseShellExecute = true });

        return chip;
    }

    /// <summary>
    /// GitHub's own card, minus the avatar: the state pill, <c>owner/repo #number</c>, the title
    /// on its own line, and the labels.
    /// </summary>
    private static Control Hovercard(LocalBuildIssue issue)
    {
        var card = new StackPanel { Spacing = 6, MaxWidth = 380 };

        var head = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { Pill(issue.State), Muted(issue.Reference, TypeScale.Small) },
        };

        card.Children.Add(head);

        card.Children.Add(new TextBlock
        {
            Text = issue.Title is { Length: > 0 } title ? title : "(title withheld)",
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        });

        if (issue.Labels.Count > 0)
        {
            card.Children.Add(Muted(string.Join(" · ", issue.Labels), TypeScale.Small));
        }

        return card;
    }

    /// <summary>
    /// The state, as GitHub's green Open and purple-or-grey Closed read.
    /// <para>
    /// <c>unknown</c> is a third state and shows as itself: GitHub was unreachable when the build
    /// was made, and dressing that as either of the other two would be the same lie
    /// <see cref="ReleaseChannel.Unknown"/> exists to refuse.
    /// </para>
    /// </summary>
    private static Control Pill(string state)
    {
        var text = new TextBlock
        {
            Text = state,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.Bold,
        };

        var pill = new Border
        {
            Padding = new Thickness(7, 1),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            Child = text,
        };

        var key = state switch
        {
            "open" => ThemeManager.AccentKey,
            "closed" => ThemeManager.TextMutedKey,
            _ => ThemeManager.TextMutedKey,
        };

        Themed(text, TextBlock.ForegroundProperty, key);
        Themed(pill, Border.BorderBrushProperty, key);

        return pill;
    }

    private static TextBlock Heading(string text)
    {
        var heading = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Heading,
            FontWeight = FontWeight.Medium,
        };

        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        return heading;
    }

    private static TextBlock Muted(string text, double size)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        return block;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
