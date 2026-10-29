using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace D47.App.Controls;

/// <summary>
/// A plain text row of choices — no frame, no box, no ground — wrapping onto a second line
/// rather than clipping or stepping (#356). Built on <see cref="RadioButton"/>, themed
/// <c>D47.TextChoice</c>, for the mutual exclusion, arrow-key movement and accessibility role
/// <see cref="Segment"/> and <see cref="Stepper"/> get the same way.
/// </summary>
public sealed class TextChoice : RovingChoice
{
    private readonly WrapPanel _row = new() { ItemSpacing = 24, LineSpacing = 6 };

    public TextChoice()
    {
        Content = _row;
    }

    /// <summary>The gap between two choices on a line.</summary>
    public double ItemSpacing
    {
        get => _row.ItemSpacing;
        set => _row.ItemSpacing = value;
    }

    protected override void Rebuild()
    {
        var theme = Application.Current?.FindResource("D47.TextChoice") as ControlTheme;

        Buttons.Clear();
        _row.Children.Clear();

        for (var i = 0; i < ItemsSource.Count; i++)
        {
            var index = i;

            var button = new RadioButton
            {
                Theme = theme,
                GroupName = Group,
                Content = ItemsSource[i].ToUpperInvariant(),
            };

            button.IsCheckedChanged += (_, _) =>
            {
                if (Syncing || button.IsChecked != true)
                {
                    return;
                }

                Choose(index);
            };

            Buttons.Add(button);
            _row.Children.Add(button);
        }

        SyncChecked();
    }
}
