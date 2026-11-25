using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>Drives a Search page's distance dropdown the way a Commander does.</summary>
internal static class SearchDistance
{
    public static InlinePicker Of(Control page) =>
        page.GetVisualDescendants().OfType<InlinePicker>().Single(picker => picker.Name == DistancePicker.PickerName);

    public static IReadOnlyList<string> Options(Control page) =>
        Of(page).GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains(InlinePicker.OptionClass))
            .Select(button => (string)((TextBlock)button.GetVisualDescendants().OfType<TextBlock>().First()).Text!)
            .ToList();

    public static string Shown(Control page) =>
        Of(page).GetVisualDescendants().OfType<TextBlock>().First(block => block.Name == "InlinePickerValue").Inlines!.OfType<Avalonia.Controls.Documents.Run>().First().Text!;

    public static void Pick(Control page, double distance) => Choose(page, distance.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture));

    public static void Choose(Control page, string label)
    {
        var picker = Of(page);
        picker.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        var option = picker.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass) && button.GetVisualDescendants().OfType<TextBlock>().First().Text == label);
        option.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
