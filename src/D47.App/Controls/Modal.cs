using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// The shared dialog layout on Bar: an A dek, an uppercase White title and an A rule; a scrolling body;
/// a Line2 rule and the dialog's buttons at the left. A <see cref="ModalDialog"/>'s 1px A frame is drawn by
/// <see cref="Windowing.ModalHost"/>.
/// </summary>
public static class Modal
{
    /// <summary>The widest a modal is drawn.</summary>
    public const double Width = 640;

    public const double Inset = 20;

    /// <summary>The space between the blocks of a dialog's body.</summary>
    public const double BlockGap = 18;

    private const double DialogTitle = 24;

    /// <summary>
    /// The layout. <paramref name="figure"/> is a key figure the dialog already shows, set at the top right
    /// of the header; <paramref name="buttons"/> are laid left-aligned in the footer in the order given.
    /// A body that does its own scrolling passes <paramref name="scrolls"/> false and is given the height
    /// between the header and the footer. <paramref name="subtitle"/> is a Grey line under the title.
    /// </summary>
    public static DockPanel Build(
        string context,
        string title,
        Control body,
        IReadOnlyList<Control> buttons,
        Control? figure = null,
        bool scrolls = true,
        string? subtitle = null)
    {
        var layout = new DockPanel { Name = "Modal" };
        Themed(layout, Avalonia.Controls.Panel.BackgroundProperty, ThemeManager.BarKey);

        var header = Header(context, title, figure, subtitle);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        var footer = Footer(buttons);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        layout.Children.Add(new ScrollViewer
        {
            Name = "ModalBody",
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = scrolls
                ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                : Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(Inset, 14), Child = body },
        });

        return layout;
    }

    /// <summary>
    /// Dresses <paramref name="dialog"/> in <see cref="Build"/>'s layout. Esc reaches its host, which
    /// closes a modal and takes a page back.
    /// </summary>
    public static void Apply(
        HostedDialog dialog,
        string context,
        string title,
        Control body,
        IReadOnlyList<Control> buttons,
        Control? figure = null,
        bool scrolls = true)
    {
        Themed(dialog, TemplatedControl.BackgroundProperty, ThemeManager.BarKey);
        dialog.Content = Build(context, title, body, buttons, figure, scrolls);
    }

    /// <summary>A section heading inside a dialog's body: uppercase White over a 1px A rule.</summary>
    public static Control Section(string text)
    {
        var heading = TitleText.Build(text, TypeScale.Section, TitleRank.Group);
        var row = TitleText.GroupRow(heading);
        row.Margin = new Thickness(0, 8, 0, 0);
        return row;
    }

    private static Control Header(string context, string title, Control? figure, string? subtitle)
    {
        var line = new TextBlock
        {
            Name = "ModalContext",
            Text = context.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Control * Fonts.ChromeTracking,
        };
        Themed(line, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var name = TitleText.Build(title, DialogTitle, TitleRank.Screen);
        name.Name = "ModalTitle";
        name.TextWrapping = TextWrapping.Wrap;

        var words = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Bottom, Children = { line, name } };

        if (subtitle is { Length: > 0 })
        {
            var under = new TextBlock
            {
                Name = "ModalSubtitle",
                Text = subtitle,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            };
            Themed(under, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            words.Children.Add(under);
        }

        var top = new DockPanel { Margin = new Thickness(Inset, 18, Inset, 10) };

        if (figure is not null)
        {
            figure.VerticalAlignment = VerticalAlignment.Bottom;
            figure.Margin = new Thickness(16, 0, 0, 0);
            DockPanel.SetDock(figure, Dock.Right);
            top.Children.Add(figure);
        }

        top.Children.Add(words);

        var rule = new Border { Height = 1 };
        Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Children = { top, rule } };
    }

    private static Control Footer(IReadOnlyList<Control> buttons)
    {
        var rule = new Border { Height = 1 };
        Themed(rule, Border.BackgroundProperty, ThemeManager.Line2Key);

        var row = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemSpacing = Gaps.Tile,
            LineSpacing = Gaps.Tile,
            Margin = new Thickness(Inset, 12, Inset, 16),
        };

        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }

        return new StackPanel { Children = { rule, row } };
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
