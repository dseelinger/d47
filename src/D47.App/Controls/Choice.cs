using Avalonia.Controls.Primitives;

namespace D47.App.Controls;

/// <summary>
/// Picks <see cref="Segment"/> or <see cref="Stepper"/> for one <see cref="IChoiceControl"/>, by
/// option count. Both raise SelectionChanged on every press, including each value a stepper passes
/// through, so a caller whose change costs something — a download, a plan — stages the choice and
/// applies it on a separate confirm (#274).
/// </summary>
public static class Choice
{
    /// <summary>Above this many options, a row of segments stops reading as one control.</summary>
    public const int SegmentLimit = 4;

    /// <summary>
    /// Builds the control and returns it as both its concrete type, for layout properties, and the
    /// shared interface, for wiring. <paramref name="alwaysStepper"/> is for a list that can change at
    /// run time — a stepper never has to decide whether it still fits a row of segments (#274).
    /// </summary>
    public static (TemplatedControl View, IChoiceControl Choice) Build(
        IReadOnlyList<string> items, int selectedIndex, bool alwaysStepper = false)
    {
        IChoiceControl control = !alwaysStepper && items.Count is > 0 and <= SegmentLimit
            ? new Segment()
            : new Stepper();

        control.ItemsSource = items;
        control.SelectedIndex = selectedIndex;

        return ((TemplatedControl)control, control);
    }
}
