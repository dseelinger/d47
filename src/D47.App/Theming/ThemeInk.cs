using Avalonia;
using Avalonia.Controls;

namespace D47.App.Theming;

/// <summary>Binds a property to a theme brush by key.</summary>
public static class ThemeInk
{
    /// <summary>Follows the brush under <paramref name="key"/> in the application resources, so a theme switch recolours <paramref name="target"/>. Disposing the result stops the binding.</summary>
    public static IDisposable Bind(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
