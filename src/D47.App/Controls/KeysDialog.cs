using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Controls;

/// <summary>Every key the window answers to, what each does and the key bound to it now (#1531).</summary>
public sealed class KeysDialog : ModalDialog
{
    public const string ChangeKeysName = "KeysChange";

    public const string GroupPrefix = "KeysGroup.";

    public const string ChipName = "KeyChip";

    public const string UnboundText = "Unbound";

    private const double RowHeight = 34;

    private const double ChipHeight = 26;

    public KeysDialog(IReadOnlyList<KeyGroup> groups, Action changeKeys)
    {
        Title = "Keys";
        Width = Modal.Width;

        var body = new StackPanel { Spacing = 20 };

        foreach (var group in groups)
        {
            body.Children.Add(Group(group));
        }

        var change = new Button { Name = ChangeKeysName, Content = "CHANGE KEYS", MinWidth = 110 };
        change.Click += (_, _) =>
        {
            Close();
            changeKeys();
        };

        var closes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Chip("?", ThemeManager.WhiteKey), Grey("or", TypeScale.Description), Chip("Esc", ThemeManager.WhiteKey), Grey("closes this list", TypeScale.Description) },
        };

        var layout = Modal.Build(
            string.Empty,
            "Keys",
            body,
            [change],
            subtitle: "Every key the window answers to, and the key bound to it now.",
            trailing: closes);
        ThemeInk.Bind(this, TemplatedControl.BackgroundProperty, ThemeManager.BarKey);
        Content = layout;

        Opened += (_, _) => Focus();
    }

    private static Control Group(KeyGroup group)
    {
        var title = new TextBlock
        {
            Text = group.Title.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Section,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Section * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        ThemeInk.Bind(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var notes = string.Join(" · ", new[] { group.Where, group.Note }.Where(note => note is { Length: > 0 }));
        var where = Grey(notes, TypeScale.Small);
        where.TextWrapping = TextWrapping.Wrap;
        where.VerticalAlignment = VerticalAlignment.Bottom;

        var kind = new TextBlock
        {
            Text = group.Changeable ? "CHANGEABLE" : "FIXED",
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.MetaSmall,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        ThemeInk.Bind(kind, TextBlock.ForegroundProperty, group.Changeable ? ThemeManager.AKey : ThemeManager.GreyKey);

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        head.Children.Add(title);
        Grid.SetColumn(where, 1);
        head.Children.Add(where);
        Grid.SetColumn(kind, 2);
        head.Children.Add(kind);

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 6), Child = head };
        ThemeInk.Bind(rule, Border.BorderBrushProperty, ThemeManager.AKey);

        var section = new StackPanel { Name = GroupPrefix + group.Title };
        section.Children.Add(rule);

        foreach (var entry in group.Entries)
        {
            section.Children.Add(Row(entry, group.Changeable));
        }

        return section;
    }

    private static Control Row(KeyEntry entry, bool changeable)
    {
        var action = new TextBlock
        {
            Text = entry.Action,
            FontSize = TypeScale.Description,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(action, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        if (entry.Unbound)
        {
            keys.Children.Add(Grey(UnboundText, TypeScale.Description));
        }

        for (var i = 0; i < entry.Keys.Count; i++)
        {
            if (i > 0 && entry.Joiner is { } joiner)
            {
                keys.Children.Add(Grey(joiner, TypeScale.Meta));
            }

            keys.Children.Add(Chip(entry.Keys[i], changeable ? ThemeManager.AKey : ThemeManager.WhiteKey));
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16, MinHeight = RowHeight };
        action.Margin = new Thickness(0, 4);
        row.Children.Add(action);
        Grid.SetColumn(keys, 1);
        row.Children.Add(keys);

        var line = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
        ThemeInk.Bind(line, Border.BorderBrushProperty, ThemeManager.Line2Key);
        return line;
    }

    private static Border Chip(string text, string colour)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(label, TextBlock.ForegroundProperty, colour);

        var chip = new Border
        {
            Name = ChipName,
            Height = ChipHeight,
            Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = label,
        };
        ThemeInk.Bind(chip, Border.BackgroundProperty, ThemeManager.SlabKey);
        return chip;
    }

    private static TextBlock Grey(string text, double size) =>
        Typeset.Text(text, size, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);

}
