using Avalonia.Controls;
using Avalonia.Layout;

namespace D47.App.Controls;

/// <summary>
/// A <see cref="CheckBox"/> carrying its label, the shape every two-state control in the app takes. The box
/// comes first, the box and label are one row and the whole row toggles.
/// </summary>
public static class LabeledCheckBox
{
    /// <summary>A checkbox whose label is a sentence, in prose.</summary>
    public static (CheckBox Box, TextBlock Label) Build(string label)
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

        return (box, text);
    }

    /// <summary>A checkbox whose label is a control's name, in uppercase; set <see cref="ContentControl.Content"/> to change it.</summary>
    public static CheckBox Caps(string label) =>
        new()
        {
            Content = label,
            VerticalAlignment = VerticalAlignment.Center,
        };
}
