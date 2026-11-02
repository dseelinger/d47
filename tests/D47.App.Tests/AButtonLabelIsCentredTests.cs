using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A button's string label is centred in the tile whatever its width (#1486).</summary>
public class AButtonLabelIsCentredTests
{
    private static Window Open(Control content) => KitWindow.ShowInStrip(content);

    [AvaloniaFact]
    public void AWideTileCentresItsLabel()
    {
        var button = new Button { Content = "Close", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Left };
        var window = Open(button);

        var text = button.GetVisualDescendants().OfType<TextBlock>().Single();
        var left = text.TranslatePoint(new Point(0, 0), button)!.Value.X;

        Assert.Equal((button.Bounds.Width - text.DesiredSize.Width) / 2, left, 1);

        window.Close();
    }

    [AvaloniaFact]
    public void ATileThatSetsItsOwnAlignmentKeepsIt()
    {
        var button = new Button
        {
            Content = "Row",
            Width = 300,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        var window = Open(button);

        Assert.Equal(HorizontalAlignment.Stretch, button.HorizontalContentAlignment);

        window.Close();
    }
}
