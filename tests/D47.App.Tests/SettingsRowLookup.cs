using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.App.Tests;

/// <summary>
/// Finds a setting's row on a settings view the way a Commander does: by the place the layout puts it in,
/// and by the words on its label.
/// </summary>
internal static class SettingsRowLookup
{
    /// <summary>The settings page's place ids, in nav order.</summary>
    public static IReadOnlyList<string> PlaceIds { get; } =
        [.. SettingsLayout.Areas.SelectMany(area => area.Places).Select(place => place.Id)];

    /// <summary>The id of the place the page has open, or null with none open.</summary>
    public static string? OpenPlaceId(this SettingsView view) =>
        view.ActiveSection is var index and >= 0 && index < PlaceIds.Count ? PlaceIds[index] : null;

    /// <summary>Presses an area's heading in the nav, which opens the area's first shown place.</summary>
    public static void SelectArea(this SettingsView view, int areaIndex)
    {
        var title = SettingsLayout.Areas[areaIndex].Title;
        var item = SettingsPageReading.AreaItems(view).Single(heading => SettingsPageReading.Name(heading).Text == title);

        item.RaiseEvent(new PointerPressedEventArgs(
            item,
            new Pointer(1, PointerType.Mouse, isPrimary: true),
            view,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Opens the place holding a setting's row, and says whether there is one.</summary>
    public static bool ShowPlaceOf(this SettingsView view, SettingsService settings, string key)
    {
        var index = PlaceIndexOf(settings, key);

        view.ShowPlace(index);
        Dispatcher.UIThread.RunJobs();

        return index >= 0;
    }

    /// <summary>The index of the place whose rows draw a setting, or −1 where no place does.</summary>
    public static int PlaceIndexOf(SettingsService settings, string key)
    {
        var drawnBy = key.StartsWith("speech.guardianVoice.", StringComparison.Ordinal)
            ? SpeechCapability.GuardianPresetKey
            : key;

        for (var i = 0; i < PlaceIds.Count; i++)
        {
            if (settings.RowsForPlace(PlaceIds[i]).Any(
                    row => row.Key == drawnBy || row.AlsoBinds == drawnBy || row.Beside == drawnBy))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The label carrying a setting's caption, or null where the view draws none.</summary>
    public static TextBlock? LabelFor(this SettingsView view, SettingsService settings, string key) =>
        settings.Find(key) is { } row && OnItsPlace(view, settings, key)
            ? Single(key, Labels(view, row.CaptionFor(settings.Current)).Where(label => RowControlOf(view, label) is not null))
            : null;

    /// <summary>
    /// The control a setting's row draws: the one beside its label, or for a row a group draws, the control
    /// named for it. Searches the open page and the page bar's tool.
    /// </summary>
    public static Control? ControlFor(this SettingsView view, SettingsService settings, string key)
    {
        if (key == SpeechCapability.GuardianTestKey)
        {
            return view.GetLogicalDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "GuardianTest");
        }

        if (settings.Find(key) is not { } row || !OnItsPlace(view, settings, key))
        {
            return null;
        }

        var caption = row.CaptionFor(settings.Current);

        return Single(key, RowControls(view, caption))
            ?? Named(view.GetLogicalDescendants(), caption)
            ?? (row.Group is { } channel ? Named(view.GetLogicalDescendants(), $"{channel} {caption}") : null)
            ?? Named(view.BarTool?.GetLogicalDescendants() ?? [], caption);
    }

    /// <summary>
    /// Whether the open page is the one the layout puts a setting on, so a caption another place's row
    /// shares cannot answer for it. A tab's strip, and a setting no place holds, are searched as they are.
    /// </summary>
    private static bool OnItsPlace(SettingsView view, SettingsService settings, string key) =>
        view.TabPlaceId is not null
        || PlaceIndexOf(settings, key) is var place && (place < 0 || place == view.ActiveSection);

    private static IEnumerable<Control> RowControls(SettingsView view, string caption) =>
        Labels(view, caption).Select(label => RowControlOf(view, label)).OfType<Control>().Distinct();

    /// <summary>The one control found, null for none; a caption on two rows cannot name either, so it throws.</summary>
    private static T? Single<T>(string key, IEnumerable<T> found)
        where T : class
    {
        var all = found.ToList();

        return all.Count <= 1
            ? all.FirstOrDefault()
            : throw new InvalidOperationException($"{key}'s caption labels {all.Count} rows on this page.");
    }

    /// <summary>Every row label drawn on the open page.</summary>
    public static IEnumerable<TextBlock> RowLabels(this SettingsView view) =>
        SettingsPageReading.Page(view).GetLogicalDescendants().OfType<TextBlock>()
            .Where(label => label.IsEffectivelyVisible && RowControlOf(view, label) is not null && !InsideARowsControl(label));

    /// <summary>Whether a block sits inside the control a row draws, as a checkbox inside a key editor does.</summary>
    private static bool InsideARowsControl(TextBlock label) =>
        label.GetLogicalAncestors().OfType<Control>().Any(
            ancestor => ancestor.Parent is Grid { ColumnDefinitions.Count: 3 or 5 } && Grid.GetColumn(ancestor) == 2);

    /// <summary>The control carrying the landing mark: the one whose background is the theme's Tile2.</summary>
    public static Control? LandedOn(this SettingsView view) => view.Marked().FirstOrDefault();

    /// <summary>Every control on the page drawn in the mark's ink.</summary>
    public static IEnumerable<Control> Marked(this SettingsView view) =>
        view.FindControl<StackPanel>("Cards")!.GetLogicalDescendants().OfType<Control>().Where(IsMarked);

    private static bool IsMarked(Control control) =>
        Avalonia.Application.Current!.FindResource(ThemeManager.Tile2Key) is { } mark
        && Background(control) is { } background
        && background.Equals(mark);

    /// <summary>Whether the landing mark is on the frame holding a setting's label.</summary>
    public static bool MarksRow(this SettingsView view, SettingsService settings, string key) =>
        view.LabelFor(settings, key) is { } label
        && label.GetLogicalAncestors().OfType<Control>().TakeWhile(control => control != view).Any(IsMarked);

    private static object? Background(Control control) => control switch
    {
        Border border => border.Background,
        Avalonia.Controls.Panel panel => panel.Background,
        Avalonia.Controls.Primitives.TemplatedControl templated => templated.Background,
        _ => null,
    };

    private static IEnumerable<TextBlock> Labels(SettingsView view, string caption) =>
        view.GetLogicalDescendants().OfType<TextBlock>().Where(block => SettingsPageReading.Words(block) == caption);

    private static Control? Named(IEnumerable<ILogical> controls, string name) =>
        controls.OfType<Control>().FirstOrDefault(
            control => control is not TextBlock && AutomationProperties.GetName(control) == name);

    /// <summary>
    /// The control a row label belongs to: the checkbox a toggle's label sits in, or the control in the third
    /// column of the row whose first column holds the label.
    /// </summary>
    private static Control? RowControlOf(SettingsView view, TextBlock label)
    {
        Control child = label;

        foreach (var ancestor in label.GetLogicalAncestors().OfType<Control>())
        {
            if (ancestor == view)
            {
                return null;
            }

            if (ancestor is CheckBox box)
            {
                return box;
            }

            if (ancestor is Grid grid
                && grid.ColumnDefinitions.Count is 3 or 5
                && Grid.GetColumn(child) == 0
                && grid.Children.FirstOrDefault(other => Grid.GetColumn(other) == 2) is { } control)
            {
                return control;
            }

            if (ancestor is Button)
            {
                return null;
            }

            child = ancestor;
        }

        return null;
    }
}
