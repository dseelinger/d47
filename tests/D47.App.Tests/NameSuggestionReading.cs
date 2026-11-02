using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>Reads and picks from the name-suggestion list beside a field.</summary>
internal static class NameSuggestionReading
{
    public static TextBox Box(this PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<TextBox>().Last(box => AutomationProperties.GetName(box) == name);

    public static string[] Listed(this PanelView panel) => [.. Buttons(panel).Select(button => AutomationProperties.GetName(button) ?? string.Empty)];

    public static void Pick(this PanelView panel, string name) =>
        Buttons(panel).Single(button => AutomationProperties.GetName(button) == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<Button> Buttons(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.Name == NameSuggestions.ListName && border.IsEffectivelyVisible)
            .SelectMany(border => border.GetVisualDescendants().OfType<Button>());
}
