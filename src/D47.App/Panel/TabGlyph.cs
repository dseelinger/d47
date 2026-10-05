using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The glyph a tab shows in place of its word when the strip is too narrow for the words (#806).</summary>
public static class TabGlyph
{
    /// <summary>The glyph's size in the tab: a 20 × 20 view box, drawn unscaled.</summary>
    public const double Size = 20;

    /// <summary>A glyph tile's width and height.</summary>
    public const double Tile = 44;

    /// <summary>The class a tab carries while it shows its glyph.</summary>
    public const string Class = "glyph";

    private static readonly Dictionary<PanelTab, string> Paths = new()
    {
        [PanelTab.Transcript] = "M3 3 H17 V13 H9 L5 17 V13 H3 Z M6 7 H14 M6 10 H11",
        [PanelTab.Search] = "M6 3 H11 L14 6 V11 L11 14 H6 L3 11 V6 Z M14 14 L18 18",
        [PanelTab.Stories] = "M10 5 L3 3 V15 L10 17 L17 15 V3 Z M10 5 V17",
        [PanelTab.Commander] = "M8.5 2 H11.5 L13.5 4 V7 L11.5 9 H8.5 L6.5 7 V4 Z M3 18 V15.5 L6.5 12 H13.5 L17 15.5 V18",
        [PanelTab.Assets] = "M3 8 L5 4 H15 L17 8 M3 8 H17 V17 H3 Z M9 6.5 H11 V10.5 H9 Z",
        [PanelTab.Navigation] = "M10 2 L17 18 L10 14 L3 18 Z",
        [PanelTab.Settings] = "M3 5 H5 M9 5 H17 M5 3 H9 V7 H5 Z M3 10 H11 M15 10 H17 M11 8 H15 V12 H11 Z M3 15 H7 M11 15 H17 M7 13 H11 V17 H7 Z",
    };

    /// <summary>The path markup of a tab's glyph, or null for a tab that has none.</summary>
    public static string? PathFor(PanelTab tab) => Paths.GetValueOrDefault(tab);

    /// <summary>The geometry a tab's template draws when it carries <see cref="Class"/>.</summary>
    public static readonly AttachedProperty<Geometry?> DataProperty =
        AvaloniaProperty.RegisterAttached<RadioButton, Geometry?>("Data", typeof(TabGlyph));

    public static Geometry? GetData(RadioButton tab) => tab.GetValue(DataProperty);

    public static void SetData(RadioButton tab, Geometry? value) => tab.SetValue(DataProperty, value);
}
