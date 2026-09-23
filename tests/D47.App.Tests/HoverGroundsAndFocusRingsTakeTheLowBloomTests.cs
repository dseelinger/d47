using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Hovering a glyph button lights a Low-tier halo around its hover ground without painting over the
/// ground itself. A button, a tab and a segment carry no hover glow; only the selected tab glows
/// (#393, #394). Keyboard focus lights the same halo behind the focus ring without touching the
/// ring's own outline (#379).
/// </summary>
public class HoverGroundsAndFocusRingsTakeTheLowBloomTests
{
    private static ThemeManager Manager() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("primary")]
    public void AHoveredButtonCarriesNoGlow(string weight)
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = new Button { Content = "Go", Width = 120, Height = 44 };
        if (weight.Length > 0)
        {
            button.Classes.Add(weight);
        }

        var window = Open(button);

        Hover(window, button);

        Assert.Empty(button.GetVisualDescendants().OfType<BloomStack>());

        window.Close();
    }

    [AvaloniaFact]
    public void HoveringAGlyphButtonLightsTheLowHaloWithTheGroundStillTransparent()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton();
        var window = Open(button);

        Hover(window, button);

        var halo = button.GetVisualDescendants().OfType<BloomStack>().Single(stack => stack.Name == "HoverGlow");
        Assert.True(halo.IsLit);
        AssertGlowing(halo);

        var ground = button.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Ground");
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)ground.Background!).Color);

        window.Close();
    }

    /// <summary>The halo draws outside the cell and leaves the cell itself as it was.</summary>
    [AvaloniaFact]
    public void AHoveredGlyphButtonsCellIsNotFilledByItsHalo()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton();
        var window = Open(button);

        var inside = At(button, window, 3, 3);
        var outside = At(button, window, -3, 22);

        var restInside = Pixel(window, inside);
        var restOutside = Pixel(window, outside);

        Hover(window, button);

        Assert.Equal(restInside, Pixel(window, inside));
        Assert.NotEqual(restOutside, Pixel(window, outside));

        window.Close();
    }

    [AvaloniaFact]
    public void AHoveredTabDoesNotGlowAndTheSelectedTabDoes()
    {
        using var kit = ControlKitTheme();
        using var tabs = PanelTabsResources();
        Manager().Apply(ThemeCatalog.Elite);

        var theme = (ControlTheme)Application.Current!.FindResource("D47.Tab")!;
        var one = new RadioButton { Theme = theme, Content = "One" };
        var two = new RadioButton { Theme = theme, Content = "Two", IsChecked = true };
        var window = Open(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { one, two } });

        Hover(window, one);

        Assert.False(Glow(one).IsLit);
        Assert.True(Glow(two).IsLit);
        Assert.Equal(BloomTier.Normal, Glow(two).Tier);
        AssertGlowing(Glow(two));

        window.Close();
    }

    [AvaloniaFact]
    public void AHoveredOrChosenSegmentCarriesNoGlow()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var theme = (ControlTheme)Application.Current!.FindResource("D47.Segment")!;
        var one = new RadioButton { Theme = theme, Content = "One" };
        var two = new RadioButton { Theme = theme, Content = "Two", IsChecked = true };
        var window = Open(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { one, two } });

        Hover(window, one);

        Assert.Empty(one.GetVisualDescendants().OfType<BloomStack>());
        Assert.Empty(two.GetVisualDescendants().OfType<BloomStack>());

        window.Close();
    }

    /// <summary>A hovered segment's ground is D47.Tile2.</summary>
    [AvaloniaFact]
    public void AHoveredSegmentsGroundKeepsItsFill()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var segment = new RadioButton { Theme = (ControlTheme)Application.Current!.FindResource("D47.Segment")!, Content = "One" };
        var window = Open(segment);

        Hover(window, segment);

        var fill = ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.Tile2Key]!).Color;
        Assert.Equal(fill, Pixel(window, At(segment, window, 3, 3)));

        window.Close();
    }

    [AvaloniaFact]
    public void LightShowsNoHaloEvenWhileHovering()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Light);

        var button = GlyphButton();
        var window = Open(button);

        Hover(window, button);

        var halo = button.GetVisualDescendants().OfType<BloomStack>().Single(stack => stack.Name == "HoverGlow");
        Assert.All(halo.Ghosts, ghost => Assert.False(ghost.IsVisible));

        window.Close();
    }

    [AvaloniaFact]
    public void FocusLightsALowHaloBehindTheRingWithTheOutlineAtFullStrength()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton();
        var window = Open(button);

        button.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        var layer = AdornerLayer.GetAdornerLayer(button)!;
        var stack = layer.GetVisualDescendants().OfType<BloomStack>().Single();

        Assert.True(stack.IsLit);
        Assert.Equal(BloomTier.Low, stack.Tier);
        AssertGlowing(stack);

        var rectangle = Assert.IsType<Rectangle>(stack.Child);
        Assert.Equal(2, rectangle.StrokeThickness);
        Assert.Null(rectangle.Fill);
        Assert.DoesNotContain(rectangle, stack.Ghosts);

        var accent = (ISolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!;
        Assert.Equal(accent.Color, ((ISolidColorBrush)rectangle.Stroke!).Color);

        window.Close();
    }

    private static Button GlyphButton() => new()
    {
        Theme = (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!,
        Content = "↺",
        Width = 44,
        Height = 44,
    };

    private static void AssertGlowing(BloomStack stack)
    {
        Assert.Equal(BloomTiers.Table(stack.Tier).Count, stack.Ghosts.Count);

        foreach (var ghost in stack.Ghosts)
        {
            Assert.IsType<DropShadowEffect>(ghost.Effect);
            Assert.True(ghost.IsVisible);
        }
    }

    private static BloomStack Glow(Control control) =>
        control.GetVisualDescendants().OfType<BloomStack>().Single(stack => stack.Name == "Glow");

    private static void Hover(Window window, Control control)
    {
        window.MouseMove(At(control, window, control.Bounds.Width / 2, control.Bounds.Height / 2));
        Dispatcher.UIThread.RunJobs();
    }

    private static Point At(Control control, Window window, double x, double y) =>
        control.TranslatePoint(new Point(x, y), window)!.Value;

    /// <summary>The colour the window draws at <paramref name="at"/>, alpha dropped.</summary>
    private static Color Pixel(Window window, Point at)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();

        var x = (int)at.X;
        var y = (int)at.Y;
        var offset = (y * buffer.RowBytes) + (x * 4);
        var bytes = new byte[4];
        Marshal.Copy(buffer.Address + offset, bytes, 0, 4);

        return buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
            ? Color.FromRgb(bytes[2], bytes[1], bytes[0])
            : Color.FromRgb(bytes[0], bytes[1], bytes[2]);
    }

    private static Window Open(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Merges ControlKitTheme.axaml onto Application.Current, removed again on Dispose.</summary>
    private static IDisposable ControlKitTheme()
    {
        var include = new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        };

        Application.Current!.Styles.Add(include);

        return new Removal(include);
    }

    private sealed class Removal(IStyle style) : IDisposable
    {
        public void Dispose() => Application.Current!.Styles.Remove(style);
    }

    /// <summary>Merges PanelTabs.axaml's resource dictionary onto Application.Current, for D47.Tab.</summary>
    private static IDisposable PanelTabsResources()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Panel/PanelTabs.axaml"),
        };

        Application.Current!.Resources.MergedDictionaries.Add(include);

        return new ResourceRemoval(include);
    }

    private sealed class ResourceRemoval(ResourceInclude resources) : IDisposable
    {
        public void Dispose() => Application.Current!.Resources.MergedDictionaries.Remove(resources);
    }
}
