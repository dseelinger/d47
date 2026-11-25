using Avalonia;
using Avalonia.Controls;

namespace D47.App.Theming;

/// <summary>Binds a property to a theme brush by key.</summary>
public static class ThemeInk
{
    /// <summary>
    /// Follows the brush under <paramref name="key"/> as <paramref name="target"/> finds it in its tree, so a theme
    /// switch recolours it. Disposing the result stops the binding.
    /// </summary>
    /// <remarks>
    /// The subscription is held by the target itself, so a dropped control is collected with its binding. A target
    /// draws nothing from the key until it is attached to a tree that reaches the application.
    /// </remarks>
    public static IDisposable Bind(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Observable(target, key));

    /// <summary>
    /// The brush under <paramref name="key"/> as <paramref name="target"/> finds it; from the application's
    /// resources for a target that is not a resource host.
    /// </summary>
    public static IObservable<object?> Observable(AvaloniaObject target, string key) =>
        target is IResourceHost host
            ? host.GetResourceObservable(key)
            : Application.Current!.Resources.GetResourceObservable(key);
}
