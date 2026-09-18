namespace D47.App.Controls;

/// <summary>
/// One value chosen from a list — the shape <see cref="Segment"/> and <see cref="Stepper"/> share,
/// so a caller can build either without a branch of its own (#274).
/// </summary>
public interface IChoiceControl
{
    IReadOnlyList<string> ItemsSource { get; set; }

    int SelectedIndex { get; set; }

    string? SelectedItem { get; }

    /// <summary>Raised when the choice changes by a press — never by setting <see cref="SelectedIndex"/>.</summary>
    event EventHandler? SelectionChanged;
}
