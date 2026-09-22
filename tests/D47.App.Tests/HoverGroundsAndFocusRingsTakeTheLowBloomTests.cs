using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Hovering a button, a glyph button, an unselected tab or an unselected segment lights a Low-tier
/// halo behind its ground; the primary button, the selected tab and the selected segment, already
/// lit at Normal, gain no second one. Keyboard focus lights the same halo behind the focus ring
/// without touching the ring's own outline (#379).
/// </summary>
public class HoverGroundsAndFocusRingsTakeTheLowBloomTests
{
    private static ThemeManager Manager() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaFact]
    public void HoveringAPlainButtonLightsTheLowHaloBehindIt()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = new Button { Content = "Go", Width = 120, Height = 44 };
        var window = Open(button);

        var halo = Halo(button);
        Assert.False(halo.IsLit);

        Hover(window, button);

        Assert.True(halo.IsLit);
        Assert.Equal(BloomTier.Low, halo.Tier);
        AssertGlowing(halo);

        window.Close();
    }

    [AvaloniaFact]
    public void HoveringThePrimaryButtonAddsNoSecondHalo()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = new Button { Content = "Go", Width = 120, Height = 44, Classes = { "primary" } };
        var window = Open(button);

        Hover(window, button);

        Assert.False(Halo(button).IsLit);

        window.Close();
    }

    [AvaloniaFact]
    public void HoveringAGlyphButtonLightsTheLowHaloWithTheGroundStillTransparent()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = new Button
        {
            Theme = (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!,
            Content = "↺",
            Width = 44,
            Height = 44,
        };
        var window = Open(button);

        Hover(window, button);

        var halo = Halo(button);
        Assert.True(halo.IsLit);
        AssertGlowing(halo);

        var ground = button.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Ground");
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)ground.Background!).Color);

        window.Close();
    }

    [AvaloniaFact]
    public void HoveringAnUnselectedTabLightsTheLowHalo()
    {
        using var kit = ControlKitTheme();
        using var tabs = PanelTabsResources();
        Manager().Apply(ThemeCatalog.Elite);

        var theme = (ControlTheme)Application.Current!.FindResource("D47.Tab")!;
        var one = new RadioButton { Theme = theme, Content = "One" };
        var two = new RadioButton { Theme = theme, Content = "Two", IsChecked = true };
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { one, two } };
        var window = Open(panel);

        Hover(window, one);

        Assert.True(Halo(one).IsLit);
        AssertGlowing(Halo(one));

        // The already-lit selected tab gains no second halo.
        Hover(window, two);
        Assert.False(Halo(two).IsLit);

        window.Close();
    }

    [AvaloniaFact]
    public void HoveringAnUnselectedSegmentLightsTheLowHalo()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var one = new RadioButton { Theme = (ControlTheme)Application.Current!.FindResource("D47.Segment")!, Content = "One" };
        var two = new RadioButton { Theme = (ControlTheme)Application.Current!.FindResource("D47.Segment")!, Content = "Two", IsChecked = true };
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { one, two } };
        var window = Open(panel);

        Hover(window, one);

        Assert.True(Halo(one).IsLit);
        AssertGlowing(Halo(one));

        // The already-lit selected segment gains no second halo.
        Hover(window, two);
        Assert.False(Halo(two).IsLit);

        window.Close();
    }

    [AvaloniaFact]
    public void LightShowsNoHaloEvenWhileHovering()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Light);

        var button = new Button { Content = "Go", Width = 120, Height = 44 };
        var window = Open(button);

        Hover(window, button);

        Assert.All(Halo(button).Ghosts, ghost => Assert.False(ghost.IsVisible));

        window.Close();
    }

    [AvaloniaFact]
    public void FocusLightsALowHaloBehindTheRingWithTheOutlineAtFullStrength()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        var button = new Button { Content = "Go", Width = 120, Height = 44 };
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
        Assert.DoesNotContain(rectangle, stack.Ghosts);

        var accent = (ISolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!;
        Assert.Equal(accent.Color, ((ISolidColorBrush)rectangle.Stroke!).Color);

        window.Close();
    }

    private static void AssertGlowing(BloomStack stack)
    {
        Assert.Equal(BloomTiers.Table(stack.Tier).Count, stack.Ghosts.Count);

        foreach (var ghost in stack.Ghosts)
        {
            Assert.IsType<DropShadowEffect>(ghost.Effect);
            Assert.True(ghost.IsVisible);
        }
    }

    /// <summary>The Low-tier <c>HoverGlow</c> bloom stack behind <paramref name="control"/>'s own ground.</summary>
    private static BloomStack Halo(Control control) =>
        control.GetVisualDescendants().OfType<BloomStack>().Single(stack => stack.Name == "HoverGlow");

    private static void Hover(Window window, Control control)
    {
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(centre);
        Dispatcher.UIThread.RunJobs();
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
