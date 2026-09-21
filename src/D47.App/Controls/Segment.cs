using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// 2 to 4 fixed choices in one framed, wrapping group (#348). Built on <see cref="RadioButton"/>, themed <c>D47.Segment</c>, for the mutual exclusion,
/// arrow-key movement and accessibility role that come with it for free, the same reason
/// <c>D47.Tab</c> is.
/// </summary>
public sealed class Segment : ContentControl, IChoiceControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> ItemsSourceProperty =
        AvaloniaProperty.Register<Segment, IReadOnlyList<string>>(nameof(ItemsSource), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Segment, int>(nameof(SelectedIndex), -1);

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    public event EventHandler? SelectionChanged;

    private readonly List<RadioButton> _buttons = [];
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

    public string? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < ItemsSource.Count ? ItemsSource[SelectedIndex] : null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
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
        var row = new WrapPanel();

        _buttons.Clear();

        for (var i = 0; i < ItemsSource.Count; i++)
        {
            var index = i;

            var button = new RadioButton
            {
                Theme = theme,
                GroupName = _group,
                Content = ItemsSource[i],
                Margin = new Thickness(0, 0, 3, 3),
            };

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

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3),
            Child = row,
        };

        frame[!Border.BorderBrushProperty] = new DynamicResourceExtension(ThemeManager.RuleKey);
        frame[!Border.BackgroundProperty] = new DynamicResourceExtension(ThemeManager.FillLowKey);

        Content = frame;
        SyncChecked();
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
    }
}
