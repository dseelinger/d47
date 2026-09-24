using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using D47.Core.Capabilities;

namespace D47.App.Controls;

/// <summary>
/// 2 to 4 fixed choices as equal-width cells 2px apart, wrapping rather than clipping (#348). Built
/// on <see cref="RadioButton"/>, themed <c>D47.Segment</c>, for the mutual exclusion, arrow-key
/// movement and accessibility role that come with it, the same reason <c>D47.Tab</c> is. An option with
/// a status carries it on a second line inside its tile.
/// </summary>
public sealed class Segment : ContentControl, IChoiceControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> ItemsSourceProperty =
        AvaloniaProperty.Register<Segment, IReadOnlyList<string>>(nameof(ItemsSource), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Segment, int>(nameof(SelectedIndex), -1);

    /// <summary>A status line for each option, parallel to <see cref="ItemsSource"/>, drawn under its label.</summary>
    public static readonly StyledProperty<IReadOnlyList<ChoiceStatus?>> StatusesProperty =
        AvaloniaProperty.Register<Segment, IReadOnlyList<ChoiceStatus?>>(nameof(Statuses), []);

    /// <summary>The space between options, across and down.</summary>
    public const double Gap = 2;

    /// <summary>An option's label as drawn; the option's Content keeps the caller's text.</summary>
    public static readonly IValueConverter Uppercase =
        new FuncValueConverter<object?, string?>(value => value?.ToString()?.ToUpperInvariant());

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    public event EventHandler? SelectionChanged;

    private readonly List<RadioButton> _buttons = [];
    private readonly List<(TextBlock Line, ChoiceTone Tone)?> _statusLines = [];
    private readonly List<IDisposable> _statusInk = [];
    private bool _syncing;
    private static int _groupSerial;
    private readonly string _group = $"D47Segment{_groupSerial++}";

    public Segment()
    {
        // Focusable itself, so a caller that focuses the control lands
        // somewhere — on the checked segment, or the first one before anything is checked.
        Focusable = true;

        GotFocus += (_, _) =>
            (_buttons.FirstOrDefault(button => button.IsChecked == true) ?? _buttons.FirstOrDefault())?.Focus();

        KeyDown += OnKeyDown;
    }

    public IReadOnlyList<string> ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public IReadOnlyList<ChoiceStatus?> Statuses
    {
        get => GetValue(StatusesProperty);
        set => SetValue(StatusesProperty, value);
    }

    public string? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < ItemsSource.Count ? ItemsSource[SelectedIndex] : null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty || change.Property == StatusesProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            SyncChecked();
        }
    }

    private void Rebuild()
    {
        var theme = Application.Current?.FindResource("D47.Segment") as ControlTheme;
        var row = new EqualCells();

        _buttons.Clear();
        _statusLines.Clear();

        for (var i = 0; i < ItemsSource.Count; i++)
        {
            var index = i;
            var status = i < Statuses.Count ? Statuses[i] : null;
            TextBlock? line = null;

            var button = new RadioButton
            {
                Theme = theme,
                GroupName = _group,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Content = status is null ? ItemsSource[i] : Labelled(ItemsSource[i], status, out line),
            };

            _statusLines.Add(line is null ? null : (line, status!.Tone));

            button.IsCheckedChanged += (_, _) =>
            {
                if (_syncing || button.IsChecked != true)
                {
                    return;
                }

                SelectedIndex = index;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            _buttons.Add(button);
            row.Children.Add(button);
        }

        Content = row;
        SyncChecked();
    }

    /// <summary>The label as the theme draws it, with the status line under it.</summary>
    private static StackPanel Labelled(string label, ChoiceStatus status, out TextBlock line)
    {
        line = new TextBlock
        {
            Text = status.Text,
            FontSize = Theming.TypeScale.Caption,
            FontWeight = Avalonia.Media.FontWeight.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        return new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = label.ToUpperInvariant(),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                line,
            },
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                Move(-1);
                e.Handled = true;
                break;

            case Key.Right:
                Move(1);
                e.Handled = true;
                break;
        }
    }

    private void Move(int delta)
    {
        if (_buttons.Count == 0)
        {
            return;
        }

        // From nothing chosen, the first press reveals an end rather than skipping past it.
        SelectedIndex = SelectedIndex < 0
            ? (delta > 0 ? 0 : _buttons.Count - 1)
            : ((SelectedIndex + delta) % _buttons.Count + _buttons.Count) % _buttons.Count;

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        _buttons[SelectedIndex].Focus();
    }

    private void SyncChecked()
    {
        _syncing = true;

        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsChecked = i == SelectedIndex;
        }

        _syncing = false;

        // A chosen tile's status takes the tile's own dark ink; the tone is for the dim tiles.
        foreach (var ink in _statusInk)
        {
            ink.Dispose();
        }

        _statusInk.Clear();

        for (var i = 0; i < _statusLines.Count; i++)
        {
            if (_statusLines[i] is not { } status)
            {
                continue;
            }

            if (i == SelectedIndex)
            {
                status.Line.ClearValue(TextBlock.ForegroundProperty);
            }
            else
            {
                _statusInk.Add(status.Line.Bind(TextBlock.ForegroundProperty, Stepper.Ink(status.Tone)));
            }
        }
    }

    /// <summary>
    /// Every child as wide as the widest, <see cref="Gap"/> apart, on as few lines as fit, with the
    /// children spread evenly across those lines, and never fewer than two across: where two of the
    /// widest do not fit, the cells narrow and their labels wrap. Given more width, the cells share it.
    /// </summary>
    private sealed class EqualCells : Avalonia.Controls.Panel
    {
        private int _columns = 1;

        protected override Size MeasureOverride(Size availableSize)
        {
            var widest = 0.0;

            foreach (var child in Children)
            {
                child.Measure(Size.Infinity);
                widest = Math.Max(widest, child.DesiredSize.Width);
            }

            var count = Children.Count;
            if (count == 0)
            {
                return default;
            }

            var fit = double.IsInfinity(availableSize.Width)
                ? count
                : (int)Math.Floor((availableSize.Width + Gap) / (widest + Gap));

            var perLine = Math.Clamp(fit, Math.Min(2, count), count);
            var rows = (count + perLine - 1) / perLine;
            _columns = (count + rows - 1) / rows;

            var cell = double.IsInfinity(availableSize.Width)
                ? widest
                : Math.Max(0, Math.Min(widest, (availableSize.Width - (_columns - 1) * Gap) / _columns));

            var tallest = 0.0;

            foreach (var child in Children)
            {
                child.Measure(new Size(cell, double.PositiveInfinity));
                tallest = Math.Max(tallest, child.DesiredSize.Height);
            }

            return new Size(
                _columns * cell + (_columns - 1) * Gap,
                rows * tallest + (rows - 1) * Gap);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var count = Children.Count;
            if (count == 0)
            {
                return finalSize;
            }

            var rows = (count + _columns - 1) / _columns;
            var width = Math.Max(0, (finalSize.Width - (_columns - 1) * Gap) / _columns);
            var height = Math.Max(0, (finalSize.Height - (rows - 1) * Gap) / rows);

            for (var i = 0; i < count; i++)
            {
                var column = i % _columns;
                var line = i / _columns;
                Children[i].Arrange(new Rect(column * (width + Gap), line * (height + Gap), width, height));
            }

            return finalSize;
        }
    }
}
