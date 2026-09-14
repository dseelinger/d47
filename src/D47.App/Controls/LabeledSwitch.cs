using Avalonia.Controls;
using Avalonia.Layout;

namespace D47.App.Controls;

/// <summary>
/// A <see cref="ToggleSwitch"/> beside its label, the shape every two-state control in the app takes
/// (#223). The switch carries no <c>OnContent</c> or <c>OffContent</c>; the label lives outside it.
/// </summary>
public static class LabeledSwitch
{
    private const double Spacing = 6;

    /// <summary>
    /// A switch and its label in one row. <paramref name="labelFirst"/> follows where the control sits
    /// in its containing panel: true for a right-hand or middle cluster and a row spanning the whole
    /// panel, false for a cluster on the panel's left.
    /// </summary>
    public static (StackPanel Box, TextBlock Label, ToggleSwitch Switch) Build(string label, bool labelFirst = true)
    {
        var toggle = new ToggleSwitch
        {
            OnContent = null,
            OffContent = null,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var box = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Spacing };

        if (labelFirst)
        {
            box.Children.Add(text);
            box.Children.Add(toggle);
        }
        else
        {
            box.Children.Add(toggle);
            box.Children.Add(text);
        }

        return (box, text, toggle);
    }
}
