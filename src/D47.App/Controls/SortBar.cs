using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Panel;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>A labelled Segmented that orders a single-label list, at least 44px tall so it presses in the headset.</summary>
public static class SortBar
{
    public static Segment Choice(string name, params string[] options) => new()
    {
        Name = name,
        ItemsSource = options,
        SelectedIndex = 0,
        Width = 110.0 * options.Length,
        Height = TypeScale.MinimumTarget,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>The choice under a "Sort" label; leaves the row it was in before, so a redraw can place it again.</summary>
    public static Control Bar(Segment choice, Thickness margin = default)
    {
        (choice.Parent as Avalonia.Controls.Panel)?.Children.Remove(choice);

        var label = TitleText.Build("Sort", TypeScale.Control, TitleRank.Subgroup);
        label.FontFamily = Fonts.ChromeFamily;
        label.VerticalAlignment = VerticalAlignment.Center;
        LoadoutPages.Themed(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        AutomationProperties.SetName(choice, "Sort");

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = margin,
            Children = { label, choice },
        };
    }
}
