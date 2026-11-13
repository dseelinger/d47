using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>The Exobiology layer fills each region's ground in one of four steps by how many species the catalogue lists there.</summary>
public class TheExobiologyLayerFillsEachRegionByItsSpeciesTests
{
    /// <summary>The share of the map's pixels a colour must cover to count as drawn, so antialiased edges do not count.</summary>
    private const double Covers = 0.005;

    private const string Scale = "EXOBIOLOGY SPECIES KNOWN: FEWER MORE";

    private static (Window Window, GalaxyMapPage Page) Opened()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (window, panel, _) = InWindow(game);
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(panel.Nav, action => action(), leads: true, openChart: panel.OpenChart);

        routing.Open(PageRef.StarChart(StarChartKind.Exobiology));
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

        // Fewer than eight layers, so every switch is on the rail's one page.
        page.ChooseLayers([StarChartKind.Travels, StarChartKind.Regions, StarChartKind.Exobiology]);
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static Color Ink(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static int Apart(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    /// <summary>The ground keys, the region ground and the four steps, that each cover at least <see cref="Covers"/> of the map.</summary>
    private static IReadOnlyList<string> GroundsDrawn(Window window, GalaxyMapPage page)
    {
        Dispatcher.UIThread.RunJobs();

        var map = page.Map();
        var topLeft = map.TranslatePoint(default, window)!.Value;
        string[] keys = [ThemeManager.MapGroundKey, .. ThemeManager.ExobiologyStepKeys];
        var inks = keys.Select(Ink).ToArray();
        var counts = new int[keys.Length];
        var sampled = 0;

        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var bytes = new byte[4];

        for (var y = (int)topLeft.Y; y < (int)(topLeft.Y + map.Bounds.Height); y += 2)
        {
            for (var x = (int)topLeft.X; x < (int)(topLeft.X + map.Bounds.Width); x += 2)
            {
                Marshal.Copy(buffer.Address + (y * buffer.RowBytes) + (x * 4), bytes, 0, 4);

                var drawn = buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
                    ? Color.FromRgb(bytes[2], bytes[1], bytes[0])
                    : Color.FromRgb(bytes[0], bytes[1], bytes[2]);

                sampled++;

                for (var index = 0; index < inks.Length; index++)
                {
                    if (Apart(inks[index], drawn) <= 2)
                    {
                        counts[index]++;
                    }
                }
            }
        }

        return [.. keys.Where((_, index) => counts[index] >= sampled * Covers)];
    }

    [AvaloniaFact]
    public void TickingExobiologyFillsTheGroundInMoreThanOneStepAndUntickingItReturnsItToOne()
    {
        var (window, page) = Opened();

        Assert.True(page.Switch("Exobiology").IsChecked);
        var ticked = GroundsDrawn(window, page);
        Assert.True(ticked.Count(key => ThemeManager.ExobiologyStepKeys.Contains(key)) > 1, string.Join(", ", ticked));

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("galaxy-map-exobiology.png");
        }

        page.Switch("Exobiology").IsChecked = false;

        Assert.Equal([ThemeManager.MapGroundKey], GroundsDrawn(window, page));
    }

    [AvaloniaFact]
    public void WithRegionsUntickedTheFillIsDrawnAlone()
    {
        var (window, page) = Opened();

        page.Switch("Regions").IsChecked = false;

        Assert.True(GroundsDrawn(window, page).Count(key => ThemeManager.ExobiologyStepKeys.Contains(key)) > 1);

        page.Switch("Exobiology").IsChecked = false;

        Assert.Empty(GroundsDrawn(window, page));
    }

    [AvaloniaFact]
    public void TheLegendHasTheScaleWhileTheLayerIsTicked()
    {
        var (_, page) = Opened();

        Assert.Contains(Scale, page.LegendEntries());

        page.Switch("Exobiology").IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Scale, page.LegendEntries());
    }

    [AvaloniaFact]
    public void AFilledMapAndAPlainOneSideBySideKeepTheirGroundImages()
    {
        new ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var filled = new GalaxyMapView { Width = 300, Height = 300 };
        var plain = new GalaxyMapView { Width = 300, Height = 300 };
        filled.Show(GalaxyMapScene.Empty with { Exobiology = true });
        plain.Show(GalaxyMapScene.Empty);

        var window = new Window { Width = 600, Height = 300, Content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { filled, plain } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var field = typeof(GalaxyMapView).GetField("_filledGround", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        using (window.CaptureRenderedFrame())
        {
        }

        var first = Image(field);

        filled.InvalidateVisual();
        plain.InvalidateVisual();
        Dispatcher.UIThread.RunJobs();

        using (window.CaptureRenderedFrame())
        {
        }

        Assert.NotNull(first);
        Assert.Same(first, Image(field));
    }

    private static Avalonia.Media.Imaging.WriteableBitmap? Image(System.Reflection.FieldInfo field) =>
        field.GetValue(null) is (Color, Color[], Avalonia.Media.Imaging.WriteableBitmap image) ? image : null;

    [AvaloniaFact]
    public void AskingForTheLayerFramesTheWholeGalaxy()
    {
        var (_, page) = Opened();

        Assert.Equal(GalaxyViewport.WholeGalaxy.LightYearsAcross, page.Map().Viewport.LightYearsAcross);
        Assert.Equal(0, page.Framing().SelectedIndex);
    }
}
