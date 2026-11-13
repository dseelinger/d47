using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Controls;

/// <summary>A legend mark drawn in theme inks.</summary>
public sealed class Glyph : Control
{
    private readonly Action<DrawingContext, Func<string, IBrush>> _draw;

    public Glyph(Action<DrawingContext, Func<string, IBrush>> draw)
    {
        _draw = draw;

        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public override void Render(DrawingContext context) =>
        _draw(context, key => this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Transparent);
}
