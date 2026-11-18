using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace D47.App.Theming;

/// <summary>
/// A table's header and rows in a box of their own. While the columns' minimum widths and the spacing fit the box,
/// the table fills it; otherwise the table is laid out at that minimum and scrolls sideways in the box.
/// </summary>
public sealed class TableScroll : Decorator
{
    private readonly Sizer _sizer;

    public TableScroll(TableLayout layout, Control table)
    {
        _sizer = new Sizer(layout) { Child = table };
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _sizer,
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _sizer.Given = availableSize.Width;
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Measures the table at the box's width, and again at its minimum when that is wider.</summary>
    private sealed class Sizer(TableLayout layout) : Decorator
    {
        private double _given = double.PositiveInfinity;

        public double Given
        {
            set
            {
                if (value.Equals(_given))
                {
                    return;
                }

                _given = value;
                InvalidateMeasure();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Child is not { } table || !double.IsFinite(_given))
            {
                return base.MeasureOverride(availableSize);
            }

            table.Measure(availableSize.WithWidth(_given));

            var shortfall = layout.Shortfall;

            if (shortfall <= 0)
            {
                return new Size(_given, table.DesiredSize.Height);
            }

            table.Measure(availableSize.WithWidth(_given + shortfall));

            return new Size(_given + shortfall, table.DesiredSize.Height);
        }
    }
}
