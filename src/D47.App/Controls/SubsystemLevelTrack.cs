using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.App.Controls;

/// <summary>
/// The per-subsystem log level track on the Log file page (#283): one row per subsystem, seven
/// stops from None to Trace, and a row that inherits <c>logging.default</c> until it is given its
/// own level. One <see cref="Grid"/> for the level names and every row, so a stop's column and its
/// name share a horizontal centre by construction rather than by measurement.
/// </summary>
public sealed class SubsystemLevelTrack : ContentControl
{
    private static readonly LogLevel[] Stops =
    [
        LogLevel.None, LogLevel.Critical, LogLevel.Error, LogLevel.Warning,
        LogLevel.Information, LogLevel.Debug, LogLevel.Trace,
    ];

    private static readonly string[] StopNames = ["None", "Critical", "Error", "Warning", "Info", "Debug", "Trace"];

    private const double LabelColumnWidth = 96;
    private const double ReadoutColumnWidth = 66;
    private const double ResetColumnWidth = 34;

    private readonly SettingsService _settings;
    private readonly SettingRow _defaultRow;
    private readonly IReadOnlyList<SettingRow> _rows;

    private readonly List<TextBlock> _nameLabels = [];
    private readonly List<Rectangle> _columnRules = [];
    private readonly List<RowParts> _rowParts = [];

    private TextBlock? _countChip;
    private TextBlock? _legendDefault;

    public SubsystemLevelTrack(SettingsService settings, SettingRow defaultRow, IReadOnlyList<SettingRow> rows)
    {
        _settings = settings;
        _defaultRow = defaultRow;
        _rows = rows;

        Content = Build();
        Refresh();

        Action<SettingsChanged> onChanged = _ => Refresh();
        settings.Changed += onChanged;
        Unloaded += (_, _) => settings.Changed -= onChanged;
    }

    private sealed record StopCell(Rectangle Left, Rectangle Right, Border Marker, Button Button);

    private sealed record RowParts(SettingRow Row, TextBlock Name, Button Readout, Button Reset, IReadOnlyList<StopCell> Cells);

    private Control Build()
    {
        var chevron = new TextBlock
        {
            Text = "▾",
            FontSize = TypeScale.Body,
            Width = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var heading = new TextBlock
        {
            Text = "PER-SUBSYSTEM OVERRIDE",
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var (chip, chipText) = BuildChip("NONE");
        _countChip = chipText;

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headerRow.Children.Add(chevron);
        headerRow.Children.Add(heading);
        headerRow.Children.Add(chip);

        var header = new Button
        {
            Content = headerRow,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

        AutomationProperties.SetName(header, "Per-subsystem override");

        var grid = BuildGrid();
        var legend = BuildLegend();

        var content = new StackPanel { Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
        content.Children.Add(grid);
        content.Children.Add(legend);

        header.Click += (_, _) =>
        {
            content.IsVisible = !content.IsVisible;
            chevron.Text = content.IsVisible ? "▾" : "▸";
        };

        var root = new StackPanel { Spacing = 2 };
        root.Children.Add(header);
        root.Children.Add(content);

        return root;
    }

    private Grid BuildGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(LabelColumnWidth, GridUnitType.Pixel),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(ReadoutColumnWidth, GridUnitType.Pixel),
                new ColumnDefinition(ResetColumnWidth, GridUnitType.Pixel),
            ],
        };

        var totalRows = _rows.Count + 1;

        for (var r = 0; r < totalRows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // One rule per stop, running the full height of the list so the last row still reads
        // against the names above it.
        for (var c = 0; c < Stops.Length; c++)
        {
            var rule = new Rectangle
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            Grid.SetColumn(rule, c + 1);
            Grid.SetRow(rule, 0);
            Grid.SetRowSpan(rule, totalRows);
            grid.Children.Add(rule);
            _columnRules.Add(rule);
        }

        for (var c = 0; c < Stops.Length; c++)
        {
            var name = new TextBlock
            {
                Text = StopNames[c],
                FontSize = TypeScale.Small,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6),
            };

            Grid.SetColumn(name, c + 1);
            Grid.SetRow(name, 0);
            grid.Children.Add(name);
            _nameLabels.Add(name);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            BuildRow(grid, i + 1, _rows[i]);
        }

        return grid;
    }

    private void BuildRow(Grid grid, int gridRow, SettingRow row)
    {
        var name = new TextBlock
        {
            Text = row.Label,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        Grid.SetColumn(name, 0);
        Grid.SetRow(name, gridRow);
        grid.Children.Add(name);

        var cells = new List<StopCell>();

        for (var c = 0; c < Stops.Length; c++)
        {
            // Each stop's line is a left and a right half-segment inside its own cell, so the
            // filled length needs no measuring — it is just which halves are coloured.
            var left = new Rectangle
            {
                Height = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var right = new Rectangle
            {
                Height = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var halves = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            halves.Children.Add(left);
            halves.Children.Add(right);

            var marker = new Border
            {
                Width = 10,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
            };

            var stack = new Avalonia.Controls.Panel();
            stack.Children.Add(halves);
            stack.Children.Add(marker);

            var index = c;
            var button = new Button
            {
                Content = stack,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MinHeight = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };

            button.Click += (_, _) => PressStop(row, index);

            Grid.SetColumn(button, c + 1);
            Grid.SetRow(button, gridRow);
            grid.Children.Add(button);

            cells.Add(new StopCell(left, right, marker, button));
        }

        var readout = new Button
        {
            FontSize = TypeScale.Small,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        AutomationProperties.SetName(readout, $"{row.Label} level");
        readout.Click += (_, _) => ClearRow(row);
        Grid.SetColumn(readout, Stops.Length + 1);
        Grid.SetRow(readout, gridRow);
        grid.Children.Add(readout);

        var reset = new Button
        {
            Theme = Application.Current?.FindResource("D47.GlyphButton") as ControlTheme,
            Content = Glyphs.Text(Glyphs.ResetText, TypeScale.Secondary),
            Width = TypeScale.MinimumTarget,
            Height = TypeScale.MinimumTarget,
            IsVisible = false,
        };

        AutomationProperties.SetName(reset, $"Reset {row.Label}");
        reset.Click += (_, _) => ClearRow(row);
        Grid.SetColumn(reset, Stops.Length + 2);
        Grid.SetRow(reset, gridRow);
        grid.Children.Add(reset);

        _rowParts.Add(new RowParts(row, name, readout, reset, cells));
    }

    private void PressStop(SettingRow row, int index)
    {
        if (row.Binding is not { } binding)
        {
            return;
        }

        var current = ParseLevel(binding.Read(_settings.Current));

        if (current == Stops[index])
        {
            _settings.Reset(row.Key, SettingsCaller.Panel);
            return;
        }

        _settings.Apply(row.Key, Stops[index].ToString(), SettingsCaller.Panel);
    }

    private void ClearRow(SettingRow row)
    {
        if (row.Binding?.Read(_settings.Current) is null)
        {
            return;
        }

        _settings.Reset(row.Key, SettingsCaller.Panel);
    }

    /// <summary>Re-reads every row from settings, without rebuilding the grid.</summary>
    public void Refresh()
    {
        var current = _settings.Current;
        var defaultLevel = ParseLevel(_defaultRow.Binding?.Read(current)) ?? LogLevel.Information;
        var defaultIndex = Math.Max(Array.IndexOf(Stops, defaultLevel), 0);

        for (var c = 0; c < _nameLabels.Count; c++)
        {
            Themed(_nameLabels[c], TextBlock.ForegroundProperty, c == defaultIndex ? ThemeManager.WhiteKey : ThemeManager.GreyKey);
        }

        for (var c = 0; c < _columnRules.Count; c++)
        {
            Themed(_columnRules[c], Rectangle.FillProperty, ThemeManager.LineKey);
            _columnRules[c].Opacity = c == defaultIndex ? 0.35 : 1;
        }

        var ownCount = 0;

        foreach (var parts in _rowParts)
        {
            var explicitLevel = ParseLevel(parts.Row.Binding?.Read(current));
            var hasOwn = explicitLevel is not null;
            var activeIndex = hasOwn ? Array.IndexOf(Stops, explicitLevel!.Value) : defaultIndex;

            if (hasOwn)
            {
                ownCount++;
            }

            Themed(parts.Name, TextBlock.ForegroundProperty, hasOwn ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

            var filledKey = hasOwn ? ThemeManager.AKey : ThemeManager.GreyKey;

            for (var c = 0; c < parts.Cells.Count; c++)
            {
                var cell = parts.Cells[c];

                var leftFilled = c >= 1 && c <= activeIndex;
                var rightFilled = c < activeIndex;

                Themed(cell.Left, Rectangle.FillProperty, leftFilled ? filledKey : ThemeManager.LineKey);
                cell.Left.Opacity = leftFilled ? 1 : 0.3;

                Themed(cell.Right, Rectangle.FillProperty, rightFilled ? filledKey : ThemeManager.LineKey);
                cell.Right.Opacity = rightFilled ? 1 : 0.3;

                var atStop = c == activeIndex;
                cell.Marker.IsVisible = atStop;

                if (atStop)
                {
                    if (hasOwn)
                    {
                        cell.Marker.BorderThickness = new Thickness(0);
                        Themed(cell.Marker, Border.BackgroundProperty, ThemeManager.AKey);
                    }
                    else
                    {
                        cell.Marker.Background = Brushes.Transparent;
                        cell.Marker.BorderThickness = new Thickness(1.5);
                        Themed(cell.Marker, Border.BorderBrushProperty, ThemeManager.BlueKey);
                    }
                }

                AutomationProperties.SetName(cell.Button, $"{parts.Row.Label}: {StopNames[c]}");
            }

            parts.Readout.Content = hasOwn ? StopNames[activeIndex] : "DEFAULT";
            Themed(parts.Readout, ContentControl.ForegroundProperty, hasOwn ? ThemeManager.AKey : ThemeManager.GreyKey);

            parts.Reset.IsVisible = hasOwn;
        }

        _countChip!.Text = ownCount == 0 ? "NONE" : $"{ownCount} OF {_rowParts.Count}";
        _legendDefault!.Text = $"INHERITED — {StopNames[defaultIndex]}";
    }

    private Control BuildLegend()
    {
        var inherited = LegendItem(hollow: true, "INHERITED — None", out var inheritedText);
        _legendDefault = inheritedText;

        var own = LegendItem(hollow: false, "SET ON THIS ROW", out _);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };
        row.Children.Add(inherited);
        row.Children.Add(own);

        return row;
    }

    private static Control LegendItem(bool hollow, string text, out TextBlock label)
    {
        var square = new Border
        {
            Width = 10,
            Height = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (hollow)
        {
            square.Background = Brushes.Transparent;
            square.BorderThickness = new Thickness(1.5);
            Themed(square, Border.BorderBrushProperty, ThemeManager.BlueKey);
        }
        else
        {
            Themed(square, Border.BackgroundProperty, ThemeManager.AKey);
        }

        label = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        stack.Children.Add(square);
        stack.Children.Add(label);

        return stack;
    }

    private static (Border Chip, TextBlock Text) BuildChip(string initial)
    {
        var text = new TextBlock { Text = initial, FontSize = TypeScale.Small };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var chip = new Border
        {
            Padding = new Thickness(6, 1),
            BorderThickness = new Thickness(1),
            Child = text,
        };
        Themed(chip, Border.BorderBrushProperty, ThemeManager.LineKey);

        return (chip, text);
    }

    private static LogLevel? ParseLevel(string? value) =>
        Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : null;

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
