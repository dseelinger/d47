using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;

namespace D47.App.Theming;

/// <summary>
/// The keyboard focus ring every control gets: a 2px Accent outline, 2px clear of the edge (#347).
/// Built here rather than as a XAML <c>ControlTemplate</c> resource — a Setter's <c>FocusAdorner</c>
/// wants an <see cref="ITemplate{TControl}"/> of <see cref="Control"/>, and the XAML compiler leaves
/// a <c>ControlTemplate</c> node unbuilt against that property, so Avalonia refuses it at runtime
/// with an <c>InvalidCastException</c>.
/// </summary>
public static class FocusRing
{
    public static ITemplate<Control> Template { get; } = new FuncTemplate<Control>(() =>
    {
        var rectangle = new Rectangle { IsHitTestVisible = false, Margin = new Thickness(-3), StrokeThickness = 2 };

        rectangle.Bind(
            Shape.StrokeProperty,
            Application.Current!.Resources.GetResourceObservable(ThemeManager.AccentKey));

        return rectangle;
    });
}
