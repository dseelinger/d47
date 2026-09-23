using Avalonia.Controls;
using Avalonia.Layout;

namespace D47.App.Controls;

/// <summary>
/// A <see cref="CheckBox"/> carrying its label as a sentence, the shape every two-state control in the
/// app takes. The box and label are one row and the whole row toggles.
/// </summary>
public static class LabeledCheckBox
{
    /// <summary>
    /// <paramref name="labelFirst"/> follows where the control sits in its containing panel: true for a
    /// right-hand or middle cluster and a row spanning the whole panel, false for a cluster on the
    /// panel's left.
    /// </summary>
    public static (CheckBox Box, TextBlock Label) Build(string label, bool labelFirst = true)
    {
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var box = new CheckBox
        {
            Content = text,
            VerticalAlignment = VerticalAlignment.Center,
        };

        box.Classes.Add("sentence");

        if (labelFirst)
        {
            box.Classes.Add("box-last");
        }

        return (box, text);
    }
}
