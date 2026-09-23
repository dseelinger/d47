using Avalonia;
using Avalonia.Controls;

namespace D47.App.Panel;

/// <summary>A transcript turn's frame: as wide as its content, or the whole of its cap once the content wraps.</summary>
internal sealed class TurnBorder : Border
{
    protected override Type StyleKeyOverride => typeof(Border);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsInfinity(availableSize.Width))
        {
            return base.MeasureOverride(availableSize);
        }

        var natural = base.MeasureOverride(availableSize.WithWidth(double.PositiveInfinity));

        if (natural.Width <= availableSize.Width)
        {
            return natural;
        }

        return base.MeasureOverride(availableSize).WithWidth(availableSize.Width);
    }
}
