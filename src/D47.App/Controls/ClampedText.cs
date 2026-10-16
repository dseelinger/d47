using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Controls;

/// <summary>Wrapping text cut to <see cref="Lines"/> lines, ending in an ellipsis whenever words were cut.</summary>
public sealed class ClampedText : TextBlock
{
    private string _full = string.Empty;
    private double _width = double.NaN;

    public int Lines { get; init; } = 3;

    /// <summary>The whole text; <see cref="TextBlock.Text"/> holds what fits.</summary>
    public string FullText
    {
        get => _full;
        set
        {
            _full = value;
            _width = double.NaN;
            Text = value;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_full.Length > 0 && !double.IsInfinity(availableSize.Width) && availableSize.Width != _width)
        {
            _width = availableSize.Width;

            var fitted = Fitted(availableSize.Width);

            if (Text != fitted)
            {
                Text = fitted;
            }
        }

        return base.MeasureOverride(availableSize);
    }

    private string Fitted(double width)
    {
        var probe = new TextBlock
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            LetterSpacing = LetterSpacing,
            LineHeight = LineHeight,
            TextWrapping = TextWrapping.Wrap,
        };

        probe.Text = "A";
        probe.Measure(new Size(width, double.PositiveInfinity));
        var limit = (probe.DesiredSize.Height * Lines) + 0.5;

        bool Fits(string text)
        {
            probe.Text = text;
            probe.Measure(new Size(width, double.PositiveInfinity));
            return probe.DesiredSize.Height <= limit;
        }

        if (Fits(_full))
        {
            return _full;
        }

        var low = 0;
        var high = _full.Length;

        while (low < high)
        {
            var mid = (low + high + 1) / 2;

            if (Fits(_full[..mid].TrimEnd() + "…"))
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return _full[..low].TrimEnd() + "…";
    }
}
