using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Search › Bodies draws the type in Sintony 14, marked or not, as the BodySearch card sets it.</summary>
public sealed class ABodyTypeCellFollowsTheBodySearchCardTests
{
    private static BodySummary Star(string spectralClass) => new()
    {
        Name = "Gru Eop AA-A h47",
        SystemName = "Gru Eop AA-A h47",
        Type = "Star",
        Subtype = "A (Blue-White super giant) Star",
        SpectralClass = spectralClass,
        Luminosity = "Ia0",
    };

    [AvaloniaFact]
    public void An_unmarked_type_is_fourteen_points()
    {
        var cell = BodySearchPage.TypeCell(Star("A0"));

        var text = Assert.IsType<TextBlock>(cell);
        Assert.Equal("A (Blue-White super giant) Star", text.Text);
        Assert.Equal(14, text.FontSize);
    }

    [AvaloniaFact]
    public void A_marked_type_is_fourteen_points_above_its_mark()
    {
        var cell = BodySearchPage.TypeCell(Star("A"));

        var stack = Assert.IsType<StackPanel>(cell);
        var type = Assert.IsType<TextBlock>(stack.Children[0]);
        Assert.Equal("A (Blue-White super giant) Star", type.Text);
        Assert.Equal(14, type.FontSize);
        Assert.Equal(11, Assert.IsType<TextBlock>(stack.Children[1]).FontSize);

        var window = new Window
        {
            Width = 320,
            Height = 120,
            Content = new StackPanel { Margin = new Avalonia.Thickness(12), Spacing = 12, Children = { cell, BodySearchPage.TypeCell(Star("A0")) } },
        };
        window.Show();
        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("body-type-cell.png");
        window.Close();
    }
}
