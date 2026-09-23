using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A segment's options: Tile cells 2px apart with no frame, and their states (#348, #394).</summary>
public class SegmentOptionsAreTilesThatWrapTests
{
    private static (Window Window, Segment Segment) Open(double width = 900)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        // Not in HeadlessApp — it stands in for App.axaml but leaves the control kit out — so the
        // D47.Segment theme this test exercises has to come from the real file.
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });

        var segment = new Segment
        {
            ItemsSource = ["Reach: near here", "Reach: a session's flying", "Reach: anywhere", "Reach: the edge"],
            SelectedIndex = 0,
        };
        var window = new Window { Content = segment, Width = width, Height = 200 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (window, segment);
    }

    private static object? Resource(string key) => Application.Current!.Resources[key];

    [AvaloniaFact]
    public void TheOptionsAreEqualWidthTilesTwoPixelsApartWithNoFrame()
    {
        var (_, segment) = Open();

        Assert.DoesNotContain(segment.GetVisualDescendants().OfType<Border>(), border => border.BorderThickness != default);

        var buttons = segment.GetVisualDescendants().OfType<RadioButton>().ToList();

        Assert.Single(buttons.Select(button => button.Bounds.Width).Distinct());
        Assert.Equal(Segment.Gap, buttons[1].Bounds.X - buttons[0].Bounds.Right, 3);

        foreach (var button in buttons.Where(button => button.IsChecked != true))
        {
            Assert.Equal(Resource(ThemeManager.TileKey), button.Background);
            Assert.Equal(Resource(ThemeManager.AKey), button.Foreground);
        }
    }

    [AvaloniaFact]
    public void TheChosenOptionIsSolidAWithKnockInk()
    {
        var (_, segment) = Open();

        var selected = segment.GetVisualDescendants().OfType<RadioButton>().Single(button => button.IsChecked == true);

        Assert.Equal(Resource(ThemeManager.AKey), selected.Background);
        Assert.Equal(Resource(ThemeManager.KnockKey), selected.Foreground);
    }

    [AvaloniaFact]
    public void ADisabledSegmentIsSlabWithGreyTwoInk()
    {
        var (_, segment) = Open();

        segment.IsEnabled = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        foreach (var button in segment.GetVisualDescendants().OfType<RadioButton>())
        {
            Assert.Equal(Resource(ThemeManager.SlabKey), button.Background);
            Assert.Equal(Resource(ThemeManager.Grey2Key), button.Foreground);
        }
    }

    [AvaloniaFact]
    public void TheLabelIsDrawnInCapitalsAndTheContentKeepsItsText()
    {
        var (_, segment) = Open();

        var button = segment.GetVisualDescendants().OfType<RadioButton>().First();

        Assert.Equal("Reach: near here", button.Content);
        Assert.Contains(button.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "REACH: NEAR HERE");
    }

    [AvaloniaFact]
    public void FourOptionsWithRoomForThreeSplitTwoAndTwo()
    {
        var (window, segment) = Open();

        var widest = segment.GetVisualDescendants().OfType<RadioButton>().Max(button => button.DesiredSize.Width);
        window.Width = 3 * widest + 2 * Segment.Gap + 1;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var lines = segment.GetVisualDescendants().OfType<RadioButton>()
            .GroupBy(button => button.Bounds.Y)
            .Select(line => line.Count())
            .ToList();

        Assert.Equal([2, 2], lines);
    }

    [AvaloniaFact]
    public void AFourOptionGroupWrapsInsteadOfClippingAtFiveTwelvePixels()
    {
        var (_, segment) = Open(width: 512);

        var rows = segment.GetVisualDescendants().OfType<RadioButton>()
            .Select(button => button.Bounds.Y)
            .Distinct()
            .Count();

        Assert.True(rows > 1, "a four-option group at 512px should wrap onto more than one row");
    }

    [AvaloniaFact]
    public void ArrowKeysStillMoveTheSelection()
    {
        var (window, segment) = Open();

        segment.GetVisualDescendants().OfType<RadioButton>().First().Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, segment.SelectedIndex);
    }
}
