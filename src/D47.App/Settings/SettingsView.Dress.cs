using System.Globalization;
using D47.Core.Audio;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Settings;

/// <summary>The dress of a group head, a row, a dropdown tile and a mixer row, shared with the Control Kit.</summary>
public partial class SettingsView
{
    /// <summary>A row's label: prose, white, wrapping.</summary>
    internal static TextBlock RowLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Ink(label, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return label;
    }

    /// <summary>The reset glyph a row, a mixer channel and a group head each carry.</summary>
    internal static Button ResetGlyph(string name, string says)
    {
        var reset = new Button
        {
            Name = name,
            Theme = GlyphButtonTheme,
            Width = TypeScale.MinimumTarget,
            Height = TypeScale.MinimumTarget,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = Glyphs.Text(Glyphs.ResetText, TypeScale.Glyph),
        };

        AutomationProperties.SetName(reset, says);

        return reset;
    }

    /// <summary>
    /// A group's head: its title and description on one line, the description wrapping under the title
    /// only when it cannot fit, <paramref name="reset"/> on the right, and an accent rule under the line.
    /// </summary>
    internal static (StackPanel Head, TextBlock Heading, TextBlock Note) GroupHead(string title, string? help, Button reset)
    {
        var heading = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        TitleText.Style(heading, TypeScale.Secondary, TitleRank.Group);
        TitleText.Show(heading, title);

        var note = new TextBlock
        {
            Text = help,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Ink(note, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var words = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(heading);
        words.Children.Add(note);

        reset.Margin = new Thickness(8, 0, 0, 0);

        var line = new DockPanel { MinHeight = TypeScale.MinimumTarget };
        DockPanel.SetDock(reset, Dock.Right);
        line.Children.Add(reset);
        line.Children.Add(words);

        var rule = new Border { Height = 1 };
        Ink(rule, Border.BackgroundProperty, ThemeManager.AKey);

        var head = new StackPanel
        {
            Name = GroupHeadName,
            Margin = new Thickness(0, 16, 0, 4),
            Children = { line, rule },
        };

        return (head, heading, note);
    }

    /// <summary>
    /// A compact row's columns: the caption, the control and the reset gutter, fixed so every row's
    /// columns line up down the page.
    /// </summary>
    internal static Grid RowColumns(Control caption, Control control, Button? reset)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(LabelColumnWidth, GridUnitType.Pixel),
                new ColumnDefinition(RowColumnGap, GridUnitType.Pixel),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(RowColumnGap, GridUnitType.Pixel),
                new ColumnDefinition(ResetGutterWidth, GridUnitType.Pixel),
            ],
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        // Not a styling hook: tests find the rows this view builds by the class rather than by
        // shape, since a three-column grid is also what Avalonia builds a TextBox out of.
        grid.Classes.Add(CompactRowClass);

        Grid.SetColumn(caption, 0);
        Grid.SetColumn(control, 2);
        grid.Children.Add(caption);
        grid.Children.Add(control);

        // A control built to a fixed width caps to the control column rather than running past it.
        if (!double.IsNaN(control.Width))
        {
            grid.SizeChanged += (_, e) => control.MaxWidth = Math.Max(
                0, e.NewSize.Width - LabelColumnWidth - (2 * RowColumnGap) - ResetGutterWidth);
        }

        if (reset is not null)
        {
            Grid.SetColumn(reset, 4);
            reset.HorizontalAlignment = HorizontalAlignment.Center;
            reset.Margin = new Thickness(0);
            grid.Children.Add(reset);
        }

        return grid;
    }

    /// <summary>
    /// A compact row's frame: the Line2 rule under it, and the left bar every row carries so every label
    /// starts at one x, drawn in A only on a protected row.
    /// </summary>
    internal static Border RowFrame(Control line, bool protectedRow)
    {
        var ruled = new Border
        {
            MinHeight = RowMinHeight,
            Padding = new Thickness(RowHorizontalPadding, RowVerticalPadding),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = line,
        };
        Ink(ruled, Border.BorderBrushProperty, ThemeManager.Line2Key);

        var bar = new Border
        {
            BorderThickness = new Thickness(RowBarWidth, 0, 0, 0),
            BorderBrush = Brushes.Transparent,
            Child = ruled,
        };

        if (protectedRow)
        {
            Ink(bar, Border.BorderBrushProperty, ThemeManager.AKey);
        }

        return bar;
    }

    /// <summary>A dropdown's tile: the value with its status line under it, and the chevron at the right.</summary>
    internal static (Button Tile, TextBlock Value, TextBlock Status) DropdownTile(string says)
    {
        var value = new TextBlock
        {
            FontSize = TypeScale.Body,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var status = new TextBlock
        {
            Name = "DropdownStatus",
            FontSize = TypeScale.Caption,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsVisible = false,
        };

        var chevron = new TextBlock
        {
            Text = "▼",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Ink(chevron, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var layout = new DockPanel();
        DockPanel.SetDock(chevron, Dock.Right);
        layout.Children.Add(chevron);
        layout.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { value, status },
        });

        var button = new Button
        {
            Name = "DropdownTile",
            Content = layout,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 4),
            BorderThickness = new Thickness(0),
            FontSize = TypeScale.Body,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Ink(button, Button.BackgroundProperty, ThemeManager.SlabKey);
        AutomationProperties.SetName(button, says);

        // The button's own tip, carrying what the column clipped — only while it actually did (#382).
        TruncationTip.Watch(value, () => value.Text, button);

        return (button, value, status);
    }

    /// <summary>
    /// One mixer channel's row: its name, a cell per column — a dash where <paramref name="cells"/> holds
    /// null — the channel's message line under the cells, and <paramref name="reset"/> in the last column.
    /// </summary>
    internal static (Grid Row, TextBlock Name) MixerChannelRow(
        string name,
        string? help,
        IReadOnlyList<string> columns,
        IReadOnlyList<Control?> cells,
        IReadOnlyList<bool> checks,
        StatusLine message,
        Button reset)
    {
        var grid = MixerGrid();
        grid.Classes.Add(MixerRowClass);
        grid.MinHeight = TypeScale.MinimumTarget;
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddMixerColumns(grid, checks);

        var nameText = RowLabel(name);
        grid.Children.Add(nameText);

        if (!string.IsNullOrWhiteSpace(help))
        {
            AttachHelp(nameText, HoverText(help));
        }

        Grid.SetRow(message, 1);
        Grid.SetColumn(message, 2);
        Grid.SetColumnSpan(message, 2 * columns.Count);
        grid.Children.Add(message);

        for (var c = 0; c < columns.Count; c++)
        {
            var column = 2 + (2 * c);

            if (cells[c] is not { } control)
            {
                var absent = new TextBlock
                {
                    Name = MixerAbsentName,
                    Text = "—",
                    FontSize = TypeScale.Body,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Ink(absent, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
                Grid.SetColumn(absent, column);
                grid.Children.Add(absent);
                continue;
            }

            if (control is Level level)
            {
                level.Width = double.NaN;
            }

            control.HorizontalAlignment = control is CheckBox ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            control.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(control, $"{name} {columns[c]}");
            Grid.SetColumn(control, column);
            grid.Children.Add(control);
        }

        Grid.SetColumn(reset, grid.ColumnDefinitions.Count - 1);
        grid.Children.Add(reset);

        return (grid, nameText);
    }

    private static void Ink(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);

    /// <summary>Marks the effects list, for a test to find it.</summary>
    public const string GuardianEffectsName = "GuardianEffects";

    /// <summary>Marks one effect's strip in the list.</summary>
    public const string GuardianStripClass = "guardian-effect";

    /// <summary>Marks the position number on a strip.</summary>
    public const string GuardianNumberName = "GuardianNumber";

    /// <summary>Marks the value a strip's stepper shows.</summary>
    public const string GuardianValueName = "GuardianValue";

    /// <summary>Marks the drag handle on a strip.</summary>
    public const string GuardianHandleName = "GuardianHandle";

    /// <summary>Marks the SAVE AS button, for a test to find it.</summary>
    public const string GuardianSaveAsName = "GuardianSaveAs";

    /// <summary>Marks the RENAME button, for a test to find it.</summary>
    public const string GuardianRenameName = "GuardianRename";

    /// <summary>Marks the UPDATE button, for a test to find it.</summary>
    public const string GuardianUpdateName = "GuardianUpdate";

    /// <summary>Marks the DELETE button, for a test to find it.</summary>
    public const string GuardianDeleteName = "GuardianDelete";

    /// <summary>Marks the UNDO button in the notice, for a test to find it.</summary>
    public const string GuardianUndoName = "GuardianUndo";

    /// <summary>Marks the name row, shown only while creating or renaming a preset.</summary>
    public const string GuardianNameRowName = "GuardianNameRow";

    /// <summary>Marks the name row's label: "Preset name" or "New name".</summary>
    public const string GuardianNameLabelName = "GuardianNameLabel";

    /// <summary>Marks the name row's text box.</summary>
    public const string GuardianNameFieldName = "GuardianNameField";

    /// <summary>Marks the name row's SAVE or RENAME button.</summary>
    public const string GuardianNameActionName = "GuardianNameAction";

    /// <summary>Marks the name row's CANCEL button.</summary>
    public const string GuardianNameCancelName = "GuardianNameCancel";

    /// <summary>Marks the name row's message line.</summary>
    public const string GuardianNameMessageName = "GuardianNameMessage";

    /// <summary>Marks the notice shown after a preset action.</summary>
    public const string GuardianNoticeName = "GuardianNotice";

    /// <summary>How long the notice stays up before it hides itself.</summary>
    public static readonly TimeSpan GuardianNoticeDuration = TimeSpan.FromSeconds(6);

    /// <summary>The narrowest the level track is drawn.</summary>
    public const double GuardianTrackMinWidth = 180;

    /// <summary>The handle's tooltip.</summary>
    public const string GuardianHandleTip = "Drag to reorder. Arrow keys also move it.";

    /// <summary>An effect's parameter at a level, in the unit its table names.</summary>
    internal static string GuardianShown(GuardianEffect effect, int level)
    {
        var value = effect.Value(level);

        return effect.Unit switch
        {
            "%" => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(value * 100):0}%"),
            "st" => string.Create(CultureInfo.InvariantCulture, $"{value:0.0} st").Replace('-', '−'),
            "×" => string.Create(CultureInfo.InvariantCulture, $"{value:0.0}×"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{value:0.##} {effect.Unit}"),
        };
    }

    /// <summary>Every effect off, default order and levels, basis cleared: <see cref="GuardianPresets.Reset"/>.</summary>
    private void ResetGuardianVoice() =>
        _settings!.Replace(
            "Guardian voice reset",
            settings => GuardianPresets.Reset(settings.Speech).Settings is { } speech ? settings with { Speech = speech } : settings);
}
