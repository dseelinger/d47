using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace D47.App.Controls;

/// <summary>
/// One value stepped through a list too long, or too changeable, for <see cref="Segment"/>
/// (#274). <c>◀</c> and <c>▶</c> either side of the current value,
/// each press moving one item and wrapping at the ends. Holding an arrow repeats the move.
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

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The value cell ellipsises past this width; nothing else in the stepper truncates.</summary>
    private const double ValueMaxWidth = 512;

    private readonly TextBlock _value = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = TextAlignment.Center,
        FontFamily = new FontFamily(Theming.Fonts.ProseFamily),
        FontSize = Theming.TypeScale.Body,
    };

    private readonly TextBlock _position = new()
    {
        Name = "StepperPosition",
        HorizontalAlignment = HorizontalAlignment.Left,
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
        FontFamily = new FontFamily(Theming.Fonts.ChromeFamily);

        _value.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.AccentInkKey));
        _position.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.TextFaintKey));
        _consequence.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.TextMutedKey));

        _previous = Arrow("◀", "Previous", -1);
        _next = Arrow("▶", "Next", 1);

        var valueCell = new Border
        {
            Padding = new Thickness(14, 0),
            MaxWidth = ValueMaxWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = _value,
        };

        valueCell.Bind(Border.BackgroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.FillHigherKey));

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(_previous, 0);
        Grid.SetColumn(valueCell, 1);
        Grid.SetColumn(_next, 2);
        row.Children.Add(_previous);
        row.Children.Add(valueCell);
        row.Children.Add(_next);

        var frame = new Border
        {
            Height = Theming.TypeScale.MinimumTarget,
            BorderThickness = new Thickness(1),
            Child = row,
        };

        frame.Bind(Border.BorderBrushProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.RuleKey));

        var caption = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(_position, 0);
        Grid.SetColumn(_consequence, 1);
        caption.Children.Add(_position);
        caption.Children.Add(_consequence);

        Content = new StackPanel { Spacing = 8, Children = { frame, caption } };

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

    public string? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < ItemsSource.Count ? ItemsSource[SelectedIndex] : null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty
            || change.Property == SelectedIndexProperty
            || change.Property == ConsequencesProperty)
        {
            Sync();
        }
    }

    private RepeatButton Arrow(string glyph, string name, int delta)
    {
        var button = new RepeatButton
        {
            Delay = 400,
            Interval = 125,
            Content = glyph,

            // Exactly the handoff's cell, which also meets the minimum interactive target for a VR ray.
            Width = Theming.TypeScale.MinimumTarget,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        button.Bind(
            TemplatedControl.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.AccentKey));

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
        _previous.IsEnabled = !few;
        _next.IsEnabled = !few;

        _position.Text = SelectedIndex >= 0 && ItemsSource.Count > 0
            ? $"{SelectedIndex + 1} / {ItemsSource.Count}"
            : string.Empty;

        var consequence = SelectedIndex >= 0 && SelectedIndex < Consequences.Count
            ? Consequences[SelectedIndex]
            : null;

        _consequence.Text = consequence ?? string.Empty;
        _consequence.IsVisible = !string.IsNullOrEmpty(consequence);
    }
}
