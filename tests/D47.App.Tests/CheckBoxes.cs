using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace D47.App.Tests;

/// <summary>Finds a checkbox by the label it carries, whether a string or a <c>TextBlock</c>.</summary>
internal static class CheckBoxes
{
    public static IEnumerable<CheckBox> Labelled(Visual root, string label) =>
        root.GetVisualDescendants().OfType<CheckBox>().Where(box => Label(box) == label);

    public static CheckBox Single(Visual root, string label) => Labelled(root, label).Single();

    public static string Label(CheckBox box) => box.Content switch
    {
        string text => text,
        TextBlock block => block.Text ?? string.Empty,
        _ => string.Empty,
    };
}
