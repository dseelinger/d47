using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;

namespace D47.App.Controls;

/// <summary>
/// A plain text row of choices — no frame, no box, no ground — wrapping onto a second line
/// rather than clipping or stepping (#356). Built on <see cref="RadioButton"/>, themed
/// <c>D47.TextChoice</c>, for the mutual exclusion, arrow-key movement and accessibility role
/// <see cref="Segment"/> and <see cref="Stepper"/> get the same way.
/// </summary>
public sealed class TextChoice : ContentControl, IChoiceControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> ItemsSourceProperty =
        AvaloniaProperty.Register<TextChoice, IReadOnlyList<string>>(nameof(ItemsSource), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<TextChoice, int>(nameof(SelectedIndex), -1);

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    public event EventHandler? SelectionChanged;

    private readonly List<RadioButton> _buttons = [];
    private readonly WrapPanel _row = new() { ItemSpacing = 24, LineSpacing = 6 };
    private bool _syncing;
    private static int _groupSerial;
    private readonly string _group = $"D47TextChoice{_groupSerial++}";

    public TextChoice()
    {
        // Focusable itself, so a caller that focuses the control lands somewhere — on the checked
        // item, or the first one before anything is checked.
        Focusable = true;
        Content = _row;

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
        var theme = Application.Current?.FindResource("D47.TextChoice") as ControlTheme;

        _buttons.Clear();
        _row.Children.Clear();

        for (var i = 0; i < ItemsSource.Count; i++)
        {
            var index = i;

            var button = new RadioButton
            {
                Theme = theme,
                GroupName = _group,
                Content = ItemsSource[i].ToUpperInvariant(),
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
            _row.Children.Add(button);
        }

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
