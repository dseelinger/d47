using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>A <see cref="SettingsGroupLayout.Mixer"/> group: one table row per row group, a column per label.</summary>
public partial class SettingsView
{
    /// <summary>Marks a mixer table, for a test to find it.</summary>
    public const string MixerName = "Mixer";

    /// <summary>Marks one channel's row of a mixer table.</summary>
    public const string MixerRowClass = "mixer-row";

    /// <summary>Marks a mixer column header.</summary>
    public const string MixerHeaderClass = "mixer-header";

    /// <summary>Marks the dash drawn where a channel has no row for a column.</summary>
    public const string MixerAbsentName = "MixerAbsent";

    private const double MixerCheckColumnWidth = 64;

    private sealed record MixerChannel(
        string Name, Grid Row, TextBlock NameText, Button Reset, IReadOnlyList<RowView> Views);

    private sealed record MixerView(Control Table, IReadOnlyList<MixerChannel> Channels);

    private readonly List<MixerView> _mixers = [];

    /// <summary>
    /// Builds the table and registers one <see cref="RowView"/> per setting, each with a detached container
    /// that <see cref="Refresh"/> uses only to record whether that setting would be shown.
    /// </summary>
    private Control BuildMixer(IReadOnlyList<SettingRow> rows, int section, int groupIndex)
    {
        var columns = rows.Select(row => row.Label).Distinct(StringComparer.Ordinal).ToList();
        var checks = columns.Select(label => rows.First(row => row.Label == label).Kind == SettingKind.Toggle).ToList();

        var table = new StackPanel { Name = MixerName, Spacing = 2 };
        table.Children.Add(BuildMixerHeader(rows, columns, checks));

        var channels = new List<MixerChannel>();

        foreach (var channel in rows.GroupBy(row => row.Group ?? row.Label, StringComparer.Ordinal))
        {
            channels.Add(BuildMixerChannel(channel.Key, [.. channel], columns, checks, section, groupIndex));
            table.Children.Add(channels[^1].Row);
        }

        _mixers.Add(new MixerView(table, channels));

        return table;
    }

    private static Grid MixerGrid()
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };

        grid.ColumnDefinitions.Add(new ColumnDefinition(LabelColumnWidth, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(RowColumnGap, GridUnitType.Pixel));

        return grid;
    }

    /// <summary>A setting column's width: a checkbox is narrow, everything else shares what is left.</summary>
    private static ColumnDefinition MixerColumn(bool check) =>
        check
            ? new ColumnDefinition(MixerCheckColumnWidth, GridUnitType.Pixel)
            : new ColumnDefinition(1, GridUnitType.Star);

    private static void AddMixerColumns(Grid grid, IReadOnlyList<bool> checks)
    {
        foreach (var check in checks)
        {
            grid.ColumnDefinitions.Add(MixerColumn(check));
            grid.ColumnDefinitions.Add(new ColumnDefinition(RowColumnGap, GridUnitType.Pixel));
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(ResetGutterWidth, GridUnitType.Pixel));
    }

    internal static Grid BuildMixerHeader(IReadOnlyList<SettingRow> rows, IReadOnlyList<string> columns, IReadOnlyList<bool> checks)
    {
        var header = MixerGrid();
        AddMixerColumns(header, checks);

        header.Children.Add(MixerHeading("Channel", null, 0));

        for (var c = 0; c < columns.Count; c++)
        {
            var help = rows.First(row => row.Label == columns[c]).Help;
            var heading = MixerHeading(columns[c], help, 2 + (2 * c));

            if (checks[c])
            {
                heading.HorizontalAlignment = HorizontalAlignment.Center;
                heading.TextAlignment = TextAlignment.Center;
            }

            header.Children.Add(heading);
        }

        return header;
    }

    private static TextBlock MixerHeading(string text, string? help, int column)
    {
        var heading = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 4, 0, 4),
        };
        heading.Classes.Add(MixerHeaderClass);
        Ink(heading, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        Grid.SetColumn(heading, column);

        if (!string.IsNullOrWhiteSpace(help))
        {
            AttachHelp(heading, HoverText(help));
        }

        return heading;
    }

    private static TextBlock HoverText(string text)
    {
        var spoken = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        };
        Ink(spoken, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return spoken;
    }

    private MixerChannel BuildMixerChannel(
        string name,
        IReadOnlyList<SettingRow> rows,
        IReadOnlyList<string> columns,
        IReadOnlyList<bool> checks,
        int section,
        int groupIndex)
    {
        // One line for whichever of the channel's controls last refused a value.
        var message = new StatusLine { Margin = new Thickness(0, 4, 0, 0) };

        var cells = new Control?[columns.Count];
        var views = new List<RowView>();

        for (var c = 0; c < columns.Count; c++)
        {
            if (rows.FirstOrDefault(row => row.Label == columns[c]) is not { } row)
            {
                continue;
            }

            var (control, refresh, _) = _controls.Build(row, message);
            cells[c] = control;

            var view = new RowView(row, new Avalonia.Controls.Panel(), refresh)
            {
                Section = section,
                GroupIndex = groupIndex,
                Control = control,
                Heading = name,
            };

            _rows.Add(view);
            views.Add(view);
        }

        var reset = ResetGlyph(RowResetName, $"Reset {name}");
        reset.IsVisible = false;

        reset.Click += (_, _) =>
        {
            foreach (var key in rows.SelectMany(row => row.BoundKeys))
            {
                _settings!.Reset(key, SettingsCaller.Panel);
            }

            Refresh();
        };

        var help = rows.Select(row => row.GroupHelp).FirstOrDefault(help => !string.IsNullOrWhiteSpace(help));
        var (grid, nameText) = MixerChannelRow(name, help, columns, cells, checks, message, reset);

        return new MixerChannel(name, grid, nameText, reset, views);
    }

    /// <summary>
    /// Shows a channel while any of its settings would be shown, and the table while any channel is.
    /// Runs after every row's own visibility is settled.
    /// </summary>
    private void RefreshMixers()
    {
        foreach (var mixer in _mixers)
        {
            foreach (var channel in mixer.Channels)
            {
                channel.Row.IsVisible = channel.Views.Any(view => view.Container.IsVisible);
                channel.Reset.IsVisible = channel.Views.Any(
                    view => view.Row.Applies(_settings!.Current) && view.Row.BoundKeys.Any(_settings.IsChanged));

                Paint(channel.NameText, channel.Name);
            }

            mixer.Table.IsVisible = mixer.Channels.Any(channel => channel.Row.IsVisible);
        }
    }
}
