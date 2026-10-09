using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A hovered button, stepper arrow or tab turns Tile2 and keeps its ink; solid A with Knock ink is for
/// press, keyboard focus and the selected tab (#500).
/// </summary>
public sealed class AHoveredTileLightensToTile2Tests
{
    [AvaloniaFact]
    public void AHoveredButtonIsTile2WithItsInkUnchanged()
    {
        using var look = AppLook.Put();

        AssertHover(new Button { Content = "Go" });
    }

    [AvaloniaFact]
    public void AHoveredStepperArrowIsTile2WithItsInkUnchanged()
    {
        using var look = AppLook.Put();

        AssertHover(new Button { Theme = Theme("D47.StepperArrow"), Content = "+", Width = 44, Height = 44 });
    }

    [AvaloniaFact]
    public void AHoveredTabIsTile2WithItsInkUnchanged()
    {
        using var look = AppLook.Put();
        using var tabs = PanelTabsResources();

        AssertHover(new RadioButton { Theme = Theme("D47.Tab"), Content = "Ship" });
    }

    [AvaloniaFact]
    public void ASelectedTabStaysSolidAWhenHovered()
    {
        using var look = AppLook.Put();
        using var tabs = PanelTabsResources();
        var tab = new RadioButton { Theme = Theme("D47.Tab"), Content = "Ship", IsChecked = true };
        var window = Open(tab);

        Set(tab, ":pointerover");

        Assert.Equal(Colour(ThemeManager.AKey), Solid(tab.Background));
        Assert.Equal(Colour(ThemeManager.KnockKey), Solid(tab.Foreground));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(":pressed")]
    [InlineData(":focus-visible")]
    public void PressAndKeyboardFocusStaySolidA(string state)
    {
        using var look = AppLook.Put();
        using var tabs = PanelTabsResources();
        TemplatedControl[] tiles =
        [
            new Button { Content = "Go" },
            new Button { Theme = Theme("D47.StepperArrow"), Content = "+", Width = 44, Height = 44 },
            new RadioButton { Theme = Theme("D47.Tab"), Content = "Ship" },
        ];

        foreach (var tile in tiles)
        {
            var window = Open(tile);

            Set(tile, state);

            Assert.Equal(Colour(ThemeManager.AKey), Solid(tile.Background));
            Assert.Equal(Colour(ThemeManager.KnockKey), Solid(tile.Foreground));

            window.Close();
        }
    }

    [AvaloniaFact]
    public void AHoveredDestructiveButtonIsStillSolidRed()
    {
        using var look = AppLook.Put();
        var button = new Button { Content = "Delete", Classes = { "destructive" } };
        var window = Open(button);

        Set(button, ":pointerover");

        Assert.Equal(Colour(ThemeManager.RedKey), Solid(button.Background));
        Assert.Equal(Colour(ThemeManager.WhiteKey), Solid(button.Foreground));

        window.Close();
    }

    /// <summary>A resting and a hovered button, stepper arrow and tab, beside a selected tab, for a human to look at.</summary>
    [AvaloniaFact]
    public void EveryTileAtRestAndHovered()
    {
        using var kit = AppLook.ControlKit();
        using var tabs = PanelTabsResources();
        var rows = new StackPanel { Spacing = 8, Margin = new Thickness(24) };

        foreach (var state in new[] { "", ":pointerover" })
        {
            TemplatedControl[] tiles =
            [
                new Button { Content = "Go" },
                new Button { Theme = Theme("D47.StepperArrow"), Content = "+", Width = 44, Height = 44 },
                new RadioButton { Theme = Theme("D47.Tab"), Content = "Ship" },
                new RadioButton { Theme = Theme("D47.Tab"), Content = "Selected", IsChecked = true },
            ];

            if (state.Length > 0)
            {
                foreach (var tile in tiles)
                {
                    Set(tile, state);
                }
            }

            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            row.Children.AddRange(tiles);
            rows.Children.Add(row);
        }

        using var frame = AppLook.Capture(rows, "tile-hover.png", width: 480, height: 170);

        Assert.True(frame.PixelSize.Width > 0);
    }

    private static void AssertHover(TemplatedControl tile)
    {
        var window = Open(tile);
        var ink = Solid(tile.Foreground);

        Set(tile, ":pointerover");

        Assert.Equal(Colour(ThemeManager.Tile2Key), Solid(tile.Background));
        Assert.Equal(ink, Solid(tile.Foreground));

        window.Close();
    }

    private static void Set(Control control, string pseudoClass)
    {
        ((IPseudoClasses)control.Classes).Set(pseudoClass, true);
        Dispatcher.UIThread.RunJobs();
    }

    private static Window Open(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ControlTheme Theme(string key) => (ControlTheme)Application.Current!.FindResource(key)!;

    /// <summary>Merges PanelTabs.axaml's resource dictionary onto Application.Current, for D47.Tab.</summary>
    private static IDisposable PanelTabsResources()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Panel/PanelTabs.axaml"),
        };

        Application.Current!.Resources.MergedDictionaries.Add(include);

        return new Removal(() => Application.Current!.Resources.MergedDictionaries.Remove(include));
    }

    private static Color Colour(string key) =>
        Solid((IBrush?)Application.Current!.FindResource(key));

    private static Color Solid(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private sealed class Removal(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
