using Avalonia;
using Avalonia.Headless.XUnit;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

public sealed class AClampedBlurbEndsInAnEllipsisWhenWordsAreCutTests
{
    private const string Long =
        "A voice in the cockpit knows a song it should not, and a pilot who has never met the singer follows it across four systems "
        + "to a station that closed before either of them was born, where the first verse is waiting on a dock nobody uses.";

    [AvaloniaFact]
    public void LongTextIsCutToThreeLinesWithAnEllipsis()
    {
        foreach (var width in new[] { 180.0, 233, 247, 260, 301, 340 })
        {
            var text = new ClampedText { FullText = Long, FontSize = 14 };
            text.Measure(new Size(width, double.PositiveInfinity));

            var one = new ClampedText { FullText = "A", FontSize = 14 };
            one.Measure(new Size(width, double.PositiveInfinity));

            Assert.EndsWith("…", text.Text, StringComparison.Ordinal);
            Assert.True(text.DesiredSize.Height <= (one.DesiredSize.Height * 3) + 1, $"{width}");
        }
    }

    [AvaloniaFact]
    public void TextThatFitsIsLeftAlone()
    {
        var text = new ClampedText { FullText = "Two pilots, one hull.", FontSize = 14 };
        text.Measure(new Size(300, double.PositiveInfinity));

        Assert.Equal("Two pilots, one hull.", text.Text);
    }
}
