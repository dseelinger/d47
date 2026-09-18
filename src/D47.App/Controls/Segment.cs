using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace D47.App.Controls;

/// <summary>
/// 2 to 4 fixed choices in one row of adjacent segments (#274). Built on <see cref="RadioButton"/>, themed <c>D47.Segment</c>, for the mutual exclusion,
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
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        _buttons.Clear();

        for (var i = 0; i < ItemsSource.Count; i++)
        {
            var index = i;

            var button = new RadioButton
            {
                Theme = theme,
                GroupName = _group,
                Content = ItemsSource[i],

                // The rightmost segment closes the block; every other one leaves its right edge to
                // the neighbour that draws it as a left edge, so the row reads as one bordered shape.
                BorderThickness = index == ItemsSource.Count - 1
                    ? new Thickness(1)
                    : new Thickness(1, 1, 0, 1),
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

        Content = row;
        SyncChecked();
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
