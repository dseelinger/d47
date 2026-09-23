using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// The shared dialog layout on Bar: an orange context line, a White title and an A rule; a scrolling body;
/// a Line2 rule and the dialog's buttons. The 1px A frame is the native window border, painted by
/// <see cref="Windowing.Dialogs.Over(Window, Window, bool, string)"/>.
/// </summary>
public static class Modal
{
    public const double Inset = 24;

    /// <summary>
    /// The layout. <paramref name="figure"/> is a key figure the dialog already shows, set at the top right
    /// of the header; <paramref name="buttons"/> are laid right-aligned in the footer in the order given.
    /// </summary>
    public static DockPanel Build(
        string context, string title, Control body, IReadOnlyList<Control> buttons, Control? figure = null)
    {
        var layout = new DockPanel { Name = "Modal" };
        Themed(layout, Avalonia.Controls.Panel.BackgroundProperty, ThemeManager.BarKey);

        var header = Header(context, title, figure);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        var footer = Footer(buttons);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        layout.Children.Add(new ScrollViewer
        {
            Name = "ModalBody",
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(Inset, 18), Child = body },
        });

        return layout;
    }

    /// <summary>Dresses <paramref name="window"/> in <see cref="Build"/>'s layout and closes it on Esc.</summary>
    public static void Apply(
        Window window, string context, string title, Control body, IReadOnlyList<Control> buttons, Control? figure = null)
    {
        Themed(window, Window.BackgroundProperty, ThemeManager.BarKey);
        window.Content = Build(context, title, body, buttons, figure);
        CloseOnEscape(window);
    }

    /// <summary>Closes <paramref name="window"/> when Esc reaches it unhandled.</summary>
    public static void CloseOnEscape(Window window) =>
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                window.Close();
            }
        };

    private static Control Header(string context, string title, Control? figure)
    {
        var line = new TextBlock
        {
            Name = "ModalContext",
            Text = context.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
        };
        Themed(line, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var name = TitleText.Build(title, TypeScale.Heading, TitleRank.Screen, sentence: true);
        name.Name = "ModalTitle";
        name.TextWrapping = TextWrapping.Wrap;

        var words = new StackPanel { Spacing = 4, Children = { line, name } };

        var top = new DockPanel();

        if (figure is not null)
        {
            figure.VerticalAlignment = VerticalAlignment.Bottom;
            figure.Margin = new Thickness(16, 0, 0, 0);
            DockPanel.SetDock(figure, Dock.Right);
            top.Children.Add(figure);
        }

        top.Children.Add(words);

        var rule = new Border { Height = 1, Margin = new Thickness(0, 14, 0, 0) };
        Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel
        {
            Margin = new Thickness(Inset, 20, Inset, 0),
            Children = { top, rule },
        };
    }

    private static Control Footer(IReadOnlyList<Control> buttons)
    {
        var rule = new Border { Height = 1 };
        Themed(rule, Border.BackgroundProperty, ThemeManager.Line2Key);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = Segment.Gap,
            Margin = new Thickness(0, 14, 0, 18),
        };

        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }

        return new StackPanel { Margin = new Thickness(Inset, 0, Inset, 0), Children = { rule, row } };
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
