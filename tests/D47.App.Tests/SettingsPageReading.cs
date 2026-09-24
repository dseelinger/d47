using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;

namespace D47.App.Tests;

/// <summary>Reads the settings page the way the Commander sees it: one place, its head, and the nav.</summary>
internal static class SettingsPageReading
{
    public static StackPanel Page(SettingsView view) => view.FindControl<StackPanel>("Cards")!;

    /// <summary>The open place's head, or null with no place open.</summary>
    public static StackPanel? Head(SettingsView view) =>
        Page(view).Children.OfType<StackPanel>().SingleOrDefault(panel => panel.Name == SettingsView.PageHeadName);

    /// <summary>The open place's title, at Title size.</summary>
    public static TextBlock Title(SettingsView view) =>
        Head(view)!.GetVisualDescendants().OfType<TextBlock>().Single(text => text.FontSize == TypeScale.Title);

    /// <summary>The breadcrumb above the title.</summary>
    public static TextBlock Crumb(SettingsView view) =>
        Head(view)!.GetVisualDescendants().OfType<TextBlock>().First();

    /// <summary>What a block says, whether it is drawn as text or as marked runs.</summary>
    public static string Words(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;

    /// <summary>Whether a control is drawn on the open page.</summary>
    public static bool OnPage(SettingsView view, Control? control) =>
        control is not null && control.IsEffectivelyVisible && control.GetVisualAncestors().Contains(Page(view));

    /// <summary>Every place's nav item, in order.</summary>
    public static List<Border> PlaceItems(SettingsView view) =>
        [.. view.FindControl<StackPanel>("NavItems")!.Children
            .Where(item => item.Classes.Contains(SettingsView.NavPlaceClass))
            .Cast<Border>()];

    /// <summary>Every area's nav heading, in order.</summary>
    public static List<Border> AreaItems(SettingsView view) =>
        [.. view.FindControl<StackPanel>("NavItems")!.Children
            .Where(item => item.Classes.Contains(SettingsView.NavAreaClass))
            .Cast<Border>()];

    /// <summary>A nav item's name.</summary>
    public static TextBlock Name(Border item) => item.GetVisualDescendants().OfType<TextBlock>().First();

    /// <summary>The match count beside a place's name, or null where none is drawn.</summary>
    public static string? Count(Border item) =>
        item.GetVisualDescendants().OfType<TextBlock>().Skip(1)
            .FirstOrDefault(text => text.IsVisible && !string.IsNullOrEmpty(text.Text))?.Text;

    /// <summary>The places the nav marks with a count, by name.</summary>
    public static Dictionary<string, string> Counted(SettingsView view) =>
        PlaceItems(view)
            .Where(item => item.IsVisible && Count(item) is not null)
            .ToDictionary(item => Words(Name(item)), item => Count(item)!);

    /// <summary>Opens a place by id, the way a click on its nav item does.</summary>
    public static void Open(SettingsView view, string placeId)
    {
        view.ShowPlace(view.SectionIds.ToList().IndexOf(placeId));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The block saying nothing on the page matches, or null where it is not drawn.</summary>
    public static StackPanel? NothingMatches(SettingsView view) =>
        Page(view).Children.OfType<StackPanel>()
            .SingleOrDefault(panel => panel.Name == SettingsView.NothingMatchesName && panel.IsVisible);
}
