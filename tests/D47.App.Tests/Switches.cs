using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace D47.App.Tests;

/// <summary>
/// Finds a labelled <c>ToggleSwitch</c> by the text beside it (#223): the switch itself carries no
/// <c>OnContent</c>/<c>OffContent</c>, so a test locates the label's <c>TextBlock</c> and reads the
/// switch among its siblings.
/// </summary>
internal static class Switches
{
    public static IEnumerable<ToggleSwitch> Labelled(Visual root, string label) =>
        root.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Text == label)
            .Select(text => text.GetVisualParent())
            .OfType<Control>()
            .SelectMany(box => box.GetVisualDescendants().OfType<ToggleSwitch>());

    public static ToggleSwitch Single(Visual root, string label) => Labelled(root, label).Single();
}
