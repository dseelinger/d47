using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using D47.Core.Capabilities;

namespace D47.App.Controls;

/// <summary>
/// One value stepped through a list too long, or too changeable, for <see cref="Segment"/>
/// (#274). <c>◄</c> and <c>►</c> tiles either side of the value, 2px apart, each press moving one
/// item and wrapping at the ends. Holding an arrow repeats the move. The value box also carries the
/// position and the value's status line; the cost line sits under the whole control.
/// </summary>
public sealed class Stepper : ContentControl, IChoiceControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> ItemsSourceProperty =
        AvaloniaProperty.Register<Stepper, IReadOnlyList<string>>(nameof(ItemsSource), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Stepper, int>(nameof(SelectedIndex), -1);

    /// <summary>
    /// What choosing each item costs or changes, parallel to <see cref="ItemsSource"/> — null or empty
    /// where no item in the list has anything to say (#336).
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string?>> ConsequencesProperty =
        AvaloniaProperty.Register<Stepper, IReadOnlyList<string?>>(nameof(Consequences), []);

    /// <summary>A status line for each item, parallel to <see cref="ItemsSource"/>, drawn under the value.</summary>
    public static readonly StyledProperty<IReadOnlyList<ChoiceStatus?>> StatusesProperty =
        AvaloniaProperty.Register<Stepper, IReadOnlyList<ChoiceStatus?>>(nameof(Statuses), []);

    /// <summary>Whether a press past either end wraps round; when false, the arrow at that end is disabled.</summary>
    public static readonly StyledProperty<bool> WrapsProperty =
        AvaloniaProperty.Register<Stepper, bool>(nameof(Wraps), true);

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The widest a stepper is drawn; the value ellipsises inside it.</summary>
    public const double MaximumWidth = 420;

    private IDisposable? _statusInk;

    private readonly TextBlock _value = new()
    {
        Name = "StepperValue",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = TextAlignment.Center,
        FontFamily = new FontFamily(Theming.Fonts.ProseFamily),
        FontSize = Theming.TypeScale.Body,
    };

    private readonly TextBlock _status = new()
    {
        Name = "StepperStatus",
        HorizontalAlignment = HorizontalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        FontSize = Theming.TypeScale.Caption,
        IsVisible = false,
    };

    private readonly TextBlock _position = new()
    {
        Name = "StepperPosition",
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
        FontFamily = new FontFamily(Theming.Fonts.MonoFamily),
        FontSize = Theming.TypeScale.Meta,
    };

    private readonly TextBlock _consequence = new()
    {
        Name = "StepperConsequence",
        HorizontalAlignment = HorizontalAlignment.Right,
        FontFamily = new FontFamily(Theming.Fonts.MonoFamily),
        FontSize = Theming.TypeScale.Meta,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = TextAlignment.Right,
        IsVisible = false,
    };

    private readonly RepeatButton _previous;
    private readonly RepeatButton _next;

    public Stepper()
    {
        Focusable = true;
        MaxWidth = MaximumWidth;
        FontFamily = new FontFamily(Theming.Fonts.ChromeFamily);

        _value.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.WhiteKey));
        _position.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.GreyKey));
        _consequence.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.GreyKey));

        _previous = Arrow("◄", "Previous", -1);
        _next = Arrow("►", "Next", 1);

        // The value cell's own truncation tip, not the whole control's — an arrow carries none (#382).
        TruncationTip.Watch(_value, () => _value.Text);

        var value = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _value, _status },
        };

        var inside = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(value, 0);
        Grid.SetColumn(_position, 1);
        inside.Children.Add(value);
        inside.Children.Add(_position);

        var valueCell = new Border
        {
            Padding = new Thickness(14, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = inside,
        };

        valueCell.Bind(Border.BackgroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.SlabKey));

        var row = new Grid
        {
            Height = Theming.TypeScale.MinimumTarget,
            ColumnSpacing = Segment.Gap,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        };
        Grid.SetColumn(_previous, 0);
        Grid.SetColumn(valueCell, 1);
        Grid.SetColumn(_next, 2);
        row.Children.Add(_previous);
        row.Children.Add(valueCell);
        row.Children.Add(_next);

        Content = new StackPanel { Spacing = 8, Children = { row, _consequence } };

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

    public IReadOnlyList<string?> Consequences
    {
        get => GetValue(ConsequencesProperty);
        set => SetValue(ConsequencesProperty, value);
    }

    public IReadOnlyList<ChoiceStatus?> Statuses
    {
        get => GetValue(StatusesProperty);
        set => SetValue(StatusesProperty, value);
    }

    public bool Wraps
    {
        get => GetValue(WrapsProperty);
        set => SetValue(WrapsProperty, value);
    }

    public string? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < ItemsSource.Count ? ItemsSource[SelectedIndex] : null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty
            || change.Property == SelectedIndexProperty
            || change.Property == ConsequencesProperty
            || change.Property == StatusesProperty
            || change.Property == WrapsProperty)
        {
            Sync();
        }
    }

    private RepeatButton Arrow(string glyph, string name, int delta)
    {
        var button = new RepeatButton
        {
            Theme = Application.Current!.FindResource("D47.StepperArrow") as ControlTheme,
            Delay = 400,
            Interval = 125,
            Content = glyph,

            // Exactly the handoff's cell, which also meets the minimum interactive target for a VR ray.
            Width = Theming.TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => Move(delta);

        return button;
    }

    private void Move(int delta)
    {
        if (ItemsSource.Count == 0)
        {
            return;
        }

        if (!Wraps && SelectedIndex >= 0
            && SelectedIndex + delta is var wanted && (wanted < 0 || wanted >= ItemsSource.Count))
        {
            return;
        }

        // From nothing chosen, the first press reveals an end rather than skipping past it: Next lands
        // on the first item, Previous on the last.
        SelectedIndex = SelectedIndex < 0
            ? (delta > 0 ? 0 : ItemsSource.Count - 1)
            : ((SelectedIndex + delta) % ItemsSource.Count + ItemsSource.Count) % ItemsSource.Count;

        SelectionChanged?.Invoke(this, EventArgs.Empty);
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

    private void Sync()
    {
        _value.Text = SelectedItem ?? string.Empty;

        var few = ItemsSource.Count <= 1;
        _previous.IsEnabled = !few && (Wraps || SelectedIndex != 0);
        _next.IsEnabled = !few && (Wraps || SelectedIndex != ItemsSource.Count - 1);

        _position.Text = SelectedIndex >= 0 && ItemsSource.Count > 0
            ? $"{SelectedIndex + 1} / {ItemsSource.Count}"
            : string.Empty;

        var consequence = SelectedIndex >= 0 && SelectedIndex < Consequences.Count
            ? Consequences[SelectedIndex]
            : null;

        _consequence.Text = consequence ?? string.Empty;
        _consequence.IsVisible = !string.IsNullOrEmpty(consequence);

        var status = SelectedIndex >= 0 && SelectedIndex < Statuses.Count ? Statuses[SelectedIndex] : null;

        _status.Text = status?.Text ?? string.Empty;
        _status.IsVisible = status is not null;
        _statusInk?.Dispose();
        _statusInk = status is null ? null : _status.Bind(TextBlock.ForegroundProperty, Ink(status.Tone));
    }

    /// <summary>The theme brush a status tone is drawn in.</summary>
    internal static IObservable<object?> Ink(ChoiceTone tone) =>
        Application.Current!.Resources.GetResourceObservable(tone == ChoiceTone.Yellow
            ? Theming.ThemeManager.YellowKey
            : Theming.ThemeManager.GreyKey);
}
