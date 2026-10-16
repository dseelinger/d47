using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>Presses the Catalogue's sentence tokens and the options in their panels.</summary>
internal static class CatalogueControls
{
    public static Button Token(PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<Button>().Single(button =>
            button.Classes.Contains(CatalogueFilters.TokenClass) && AutomationProperties.GetName(button) == name);

    public static void PressToken(PanelView panel, string name)
    {
        Token(panel, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Opens the token's panel unless it is open and picks option <paramref name="option"/> in it.</summary>
    public static void Choose(PanelView panel, string name, int option)
    {
        PressToken(panel, name);

        var segment = panel.GetVisualDescendants().OfType<Segment>().Single(control => AutomationProperties.GetName(control) == name + " options");
        segment.GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Opens the Level panel if it is closed and sets the box labelled <paramref name="level"/>.</summary>
    public static void Tick(PanelView panel, string level, bool on = true)
    {
        if (!panel.GetVisualDescendants().OfType<CheckBox>().Any(box => box.IsEffectivelyVisible))
        {
            PressToken(panel, "Level");
        }

        panel.GetVisualDescendants().OfType<CheckBox>().Single(box => TextOf(box) == level).IsChecked = on;
        Dispatcher.UIThread.RunJobs();
    }

    public static string TokenText(PanelView panel, string name) =>
        Token(panel, name).GetVisualDescendants().OfType<TextBlock>().First().Text ?? string.Empty;

    /// <summary>A block's text, whether set as Text or as inlines.</summary>
    public static string TextOf(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(run => run.Text)) : block.Text ?? string.Empty;

    private static string TextOf(CheckBox box) =>
        box.GetVisualDescendants().OfType<TextBlock>().Select(TextOf).FirstOrDefault(text => text.Length > 0) ?? string.Empty;
}
