using Avalonia.Controls;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// A tooltip's content for a keybind, path or id — mono rather than the prose the tooltip theme
/// draws a plain string in (#381).
/// </summary>
public static class MachineTip
{
    public static TextBlock For(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily(Fonts.MonoFamily),
        FontSize = TypeScale.Small,
    };
}
