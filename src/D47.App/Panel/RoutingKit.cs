using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The parts every Routing page is drawn from (#403).</summary>
internal static class RoutingKit
{
    /// <summary>The widest a line of prose is set.</summary>
    public const double ProseWidth = 520;

    /// <summary>
    /// The page's Screen title over a 1px A rule, at Heading size while the panel is mini. The text
    /// can be changed through the returned block.
    /// </summary>
    public static (Control Row, SelectableTextBlock Text) Title(string text)
    {
        var block = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            TypeScale.Title,
            TitleRank.Screen,
            sentence: true);

        TitleText.Show(block, text, sentence: true);

        var row = TitleText.GroupRow(block);
        row.Margin = new Thickness(0, 0, 0, 10);

        IDisposable? mode = null;

        row.AttachedToVisualTree += (_, _) =>
            mode = row.GetSelfAndVisualAncestors()
                .OfType<PanelView>()
                .FirstOrDefault()
                ?.GetObservable(PanelView.ModeProperty)
                .Subscribe(new AnonymousObserver<PanelMode>(panel =>
                {
                    var mini = panel == PanelMode.Mini;

                    block.FontSize = mini ? TypeScale.Heading : TypeScale.Title;
                    row.Margin = new Thickness(0, 0, 0, mini ? 4 : 10);
                }));

        row.DetachedFromVisualTree += (_, _) =>
        {
            mode?.Dispose();
            mode = null;
        };

        return (row, block);
    }

    /// <summary>An uppercase White section heading over a 1px A rule, with <paramref name="trailing"/> at its right.</summary>
    public static Control Section(string text, Control? trailing = null)
    {
        if (trailing is null)
        {
            return LoadoutPages.Section(text);
        }

        var heading = new SelectableTextBlock { VerticalAlignment = VerticalAlignment.Bottom };

        TitleText.Style(heading, TypeScale.Section, TitleRank.Group);
        TitleText.Show(heading, text);

        trailing.Margin = new Thickness(8, 0, 0, 0);

        var row = new DockPanel();

        DockPanel.SetDock(trailing, Dock.Right);
        row.Children.Add(trailing);
        row.Children.Add(heading);

        var section = TitleText.GroupRow(row);
        section.Margin = new Thickness(0, 14, 0, 6);

        return section;
    }

    /// <summary>A section's HELP mark, opening <paramref name="page"/>.</summary>
    public static Button Help(PanelNavigator nav, string page, string title)
    {
        var mark = D47.App.Controls.Glyphs.Quiet(new Button(), "HELP", $"About {title}");
        mark.Click += (_, _) => D47.Core.Help.HelpLevel.Open(nav, page);

        return mark;
    }

    /// <summary>Helper prose in Grey, wrapped at <see cref="ProseWidth"/>.</summary>
    public static TextBlock Prose(string text, double size = TypeScale.Small) =>
        Ink(text, size, ThemeManager.GreyKey, wrap: true);

    /// <summary>A line of text in the ink <paramref name="key"/> names.</summary>
    public static TextBlock Ink(string text, double size, string key, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? ProseWidth : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    /// <summary>A status line, hidden until <see cref="Say"/> writes to it.</summary>
    public static TextBlock Status()
    {
        var status = Ink(string.Empty, TypeScale.Secondary, ThemeManager.GreyKey, wrap: true);
        status.IsVisible = false;

        return status;
    }

    /// <summary>Shows <paramref name="text"/> on a status line, in Red where it is an error.</summary>
    public static void Say(TextBlock status, string text, bool error = false)
    {
        status.IsVisible = true;
        status.Text = text;
        Themed(status, TextBlock.ForegroundProperty, error ? ThemeManager.RedKey : ThemeManager.GreyKey);
    }

    /// <summary>A word on a row, uppercase in the ink <paramref name="key"/> names.</summary>
    public static TextBlock Tag(string word, string key)
    {
        var tag = new TextBlock
        {
            Text = word.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(tag, TextBlock.ForegroundProperty, key);

        return tag;
    }

    /// <summary>Buttons in a row that wraps rather than clips.</summary>
    public static WrapPanel Actions(params Control[] buttons)
    {
        var row = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(0, 4, 0, 0) };

        foreach (var button in buttons)
        {
            button.VerticalAlignment = VerticalAlignment.Top;
            row.Children.Add(button);
        }

        return row;
    }

    /// <summary>Form fields in a row that wraps rather than clips.</summary>
    public static WrapPanel Fields(params Control[] fields)
    {
        var row = new WrapPanel { ItemSpacing = 10, LineSpacing = 8 };

        foreach (var field in fields)
        {
            row.Children.Add(field);
        }

        return row;
    }

    /// <summary>A switch sized to its label, with the box first.</summary>
    public static CheckBox Switch(string label)
    {
        var (box, _) = D47.App.Controls.LabeledCheckBox.Build(label, labelFirst: false);
        box.HorizontalAlignment = HorizontalAlignment.Left;

        return box;
    }

    /// <summary>
    /// A system name in A, Cyan where it is the Commander's current system and Grey where it is behind them.
    /// </summary>
    public static string SystemKey(string system, string? here, bool behind = false) =>
        IsHere(system, here) ? ThemeManager.CyanKey
        : behind ? ThemeManager.GreyKey
        : ThemeManager.AKey;

    public static bool IsHere(string system, string? here) =>
        here is { Length: > 0 } && string.Equals(system, here, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A list row: <paramref name="lines"/> stacked 2px apart, a copy glyph at the right where there is a
    /// clipboard, and the whole row copying <paramref name="system"/> when pressed.
    /// </summary>
    public static Border Row(IEnumerable<Control> lines, string system, Func<string, Task<bool>>? copy)
    {
        var body = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

        foreach (var line in lines)
        {
            body.Children.Add(line);
        }

        var layout = new DockPanel();

        if (copy is not null)
        {
            var glyph = D47.App.Controls.CopyWord.For(system, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(10, 0, 0, 0);

            DockPanel.SetDock(glyph, Dock.Right);
            layout.Children.Add(glyph);
        }

        layout.Children.Add(body);

        var row = ListRow.Dress(new Border { Padding = new Thickness(12, 6), Child = layout });

        if (copy is not null)
        {
            // The clipboard is the part of plotting that always works, whatever the map is doing.
            row.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            row.Tapped += (_, _) => _ = copy(system);
        }

        return row;
    }

    /// <summary>A row's first line: its name, then any tags.</summary>
    public static WrapPanel Named(string name, string key, params Control[] tags)
    {
        var line = new WrapPanel { ItemSpacing = 8, LineSpacing = 2 };

        var text = Ink(name, TypeScale.Body, key);
        text.TextWrapping = TextWrapping.Wrap;
        text.VerticalAlignment = VerticalAlignment.Center;

        line.Children.Add(text);

        foreach (var tag in tags)
        {
            line.Children.Add(tag);
        }

        return line;
    }

    /// <summary>A row's second line: values in A, or Grey where the row is behind the Commander.</summary>
    public static TextBlock Values(string text, bool behind = false)
    {
        var line = Ink(text, TypeScale.Secondary, behind ? ThemeManager.GreyKey : ThemeManager.AKey);
        line.TextWrapping = TextWrapping.Wrap;
        line.MaxWidth = double.PositiveInfinity;

        return line;
    }

    /// <summary>What a page says in place of its form when the galaxy setting is off.</summary>
    public static Control SwitchedOff(string heading, string text, Action? openSettings)
    {
        var body = new StackPanel { Spacing = 8 };

        body.Children.Add(Section(heading));
        body.Children.Add(Ink(text, TypeScale.Body, ThemeManager.GreyKey, wrap: true));

        if (openSettings is { } open)
        {
            var button = new Button { Content = "Open settings" };
            button.Click += (_, _) => open();
            body.Children.Add(Actions(button));
        }

        return body;
    }

    public static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
