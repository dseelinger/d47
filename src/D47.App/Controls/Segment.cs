using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using D47.App.Theming;
using D47.Core.Capabilities;

namespace D47.App.Controls;

/// <summary>
/// 2 to 4 fixed choices as equal-width cells 2px apart, wrapping rather than clipping (#348). Built
/// on <see cref="RadioButton"/>, themed <c>D47.Segment</c>, for the mutual exclusion, arrow-key
/// movement and accessibility role that come with it, the same reason <c>D47.Tab</c> is. An option with
/// a status carries it on a second line inside its tile.
/// </summary>
public sealed class Segment : RovingChoice
{
    /// <summary>A status line for each option, parallel to <see cref="ItemsSource"/>, drawn under its label.</summary>
    public static readonly StyledProperty<IReadOnlyList<ChoiceStatus?>> StatusesProperty =
        AvaloniaProperty.Register<Segment, IReadOnlyList<ChoiceStatus?>>(nameof(Statuses), []);

    /// <summary>The space between options, across and down.</summary>
    public const double Gap = Gaps.Tile;

    /// <summary>The height of an option that carries a status line.</summary>
    public const double StatusHeight = 56;

    /// <summary>An option's label as drawn; the option's Content keeps the caller's text.</summary>
    public static readonly IValueConverter Uppercase =
        new FuncValueConverter<object?, string?>(value => value?.ToString()?.ToUpperInvariant());

    /// <summary>Presents a label built in code as it is, instead of through the theme's uppercase text template.</summary>
    private static readonly Avalonia.Controls.Templates.FuncDataTemplate<Control> Itself = new((control, _) => control);

    private readonly List<(TextBlock Line, ChoiceTone Tone)?> _statusLines = [];
    private readonly List<IDisposable> _statusInk = [];
    public IReadOnlyList<ChoiceStatus?> Statuses
    {
        get => GetValue(StatusesProperty);
        set => SetValue(StatusesProperty, value);
    }

    /// <summary>Greys out one option, which then cannot be chosen; the choice itself is left alone.</summary>
    public void SetOptionEnabled(int index, bool enabled)
    {
        if (index >= 0 && index < Buttons.Count)
        {
            Buttons[index].IsEnabled = enabled;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StatusesProperty)
        {
            Rebuild();
        }
    }

    protected override void Rebuild()
    {
        var theme = Application.Current?.FindResource("D47.Segment") as ControlTheme;
        var row = new EqualCells();

        Buttons.Clear();
        _statusLines.Clear();

        for (var i = 0; i < ItemsSource.Count; i++)
        {
            var index = i;
            var status = i < Statuses.Count ? Statuses[i] : null;
            TextBlock? line = null;

            var button = new RadioButton
            {
                Theme = theme,
                GroupName = Group,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Content = status is null ? ItemsSource[i] : Labelled(ItemsSource[i], status, out line),
            };

            if (status is not null)
            {
                button.MinHeight = StatusHeight;
                button.ContentTemplate = Itself;
            }

            _statusLines.Add(line is null ? null : (line, status!.Tone));

            button.IsCheckedChanged += (_, _) =>
            {
                if (Syncing || button.IsChecked != true)
                {
                    return;
                }

                // A press that reaches a disabled option, as the headset's can, puts the choice back.
                if (!button.IsEnabled)
                {
                    SyncChecked();
                    return;
                }

                Choose(index);
            };

            Buttons.Add(button);
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
            Text = status.Text.ToUpperInvariant(),
            FontSize = Theming.TypeScale.MetaSmall,
            FontWeight = Avalonia.Media.FontWeight.Medium,
            LetterSpacing = 0.55,
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

    protected override void SyncChecked()
    {
        base.SyncChecked();

        // A chosen tile's status is Brown; the tone is for the tiles not chosen.
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

            _statusInk.Add(status.Line.Bind(
                TextBlock.ForegroundProperty,
                i == SelectedIndex
                    ? Application.Current!.Resources.GetResourceObservable(ThemeManager.BrownKey)
                    : Stepper.Ink(status.Tone)));
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
