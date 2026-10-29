using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace D47.App.Controls;

/// <summary>Shared keyboard, focus and selection sync for a list of choices drawn as <see cref="RadioButton"/>s.</summary>
public abstract class RovingChoice : ContentControl, IChoiceControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> ItemsSourceProperty =
        AvaloniaProperty.Register<RovingChoice, IReadOnlyList<string>>(nameof(ItemsSource), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<RovingChoice, int>(nameof(SelectedIndex), -1);

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    public event EventHandler? SelectionChanged;

    private static int _groupSerial;

    protected readonly List<RadioButton> Buttons = [];
    protected readonly string Group;

    protected bool Syncing { get; private set; }

    protected RovingChoice()
    {
        Group = $"D47{GetType().Name}{_groupSerial++}";

        // Focusable itself, so a caller that focuses the control lands somewhere — on the checked
        // item, or the first one before anything is checked.
        Focusable = true;

        GotFocus += (_, _) =>
            (Buttons.FirstOrDefault(button => button.IsChecked == true) ?? Buttons.FirstOrDefault())?.Focus();
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

    protected abstract void Rebuild();

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

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

    /// <summary>Records a press on option <paramref name="index"/> as the choice.</summary>
    protected void Choose(int index)
    {
        SelectedIndex = index;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void SyncChecked()
    {
        Syncing = true;

        for (var i = 0; i < Buttons.Count; i++)
        {
            Buttons[i].IsChecked = i == SelectedIndex;
        }

        Syncing = false;
    }

    private void Move(int delta)
    {
        if (Buttons.Count == 0)
        {
            return;
        }

        // From nothing chosen, the first press reveals an end rather than skipping past it.
        SelectedIndex = SelectedIndex < 0
            ? (delta > 0 ? 0 : Buttons.Count - 1)
            : ((SelectedIndex + delta) % Buttons.Count + Buttons.Count) % Buttons.Count;

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Buttons[SelectedIndex].Focus();
    }
}
