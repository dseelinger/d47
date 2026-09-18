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
/// (#274). <c>◂</c> and <c>▸</c> either side of the current value,
/// each press moving one item and wrapping at the ends.
/// </summary>
public sealed class Stepper : ContentControl, IChoiceControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> ItemsSourceProperty =
        AvaloniaProperty.Register<Stepper, IReadOnlyList<string>>(nameof(ItemsSource), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Stepper, int>(nameof(SelectedIndex), -1);

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    public event EventHandler? SelectionChanged;

    private readonly TextBlock _value = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = TextAlignment.Center,
    };

    private readonly Button _previous;
    private readonly Button _next;

    public Stepper()
    {
        Focusable = true;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(2, 0);
        FontFamily = new FontFamily("avares://d47/Assets/Fonts/SairaSemiCondensed-Regular.ttf#Saira Semi Condensed");

        this.Bind(BackgroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.FillLowKey));
        this.Bind(BorderBrushProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.RuleKey));
        _value.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.TextKey));

        _previous = Arrow("◂", "Previous", -1);
        _next = Arrow("▸", "Next", 1);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(_previous, 0);
        Grid.SetColumn(_value, 1);
        Grid.SetColumn(_next, 2);
        row.Children.Add(_previous);
        row.Children.Add(_value);
        row.Children.Add(_next);

        Content = row;

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

    public string? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < ItemsSource.Count ? ItemsSource[SelectedIndex] : null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty || change.Property == SelectedIndexProperty)
        {
            Sync();
        }
    }

    private Button Arrow(string glyph, string name, int delta)
    {
        var button = new Button
        {
            Content = glyph,
            Padding = new Thickness(8, 0),
            MinWidth = 0,

            // Tall enough for a VR ray to land on.
            MinHeight = 30,
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
    }
}
