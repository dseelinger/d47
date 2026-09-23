using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every button class draws a flat Tile with no outline, corner or glow: solid A with Knock ink on
/// hover or keyboard focus, Slab with Grey2 ink when disabled; a destructive one uses Red (#393).
/// </summary>
public class EveryButtonIsAFlatTileTests
{
    public static TheoryData<string> Weights => ["", "destructive"];

    private static Window Open(Button button)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        // Not in HeadlessApp — it stands in for App.axaml but leaves the control kit out — so the
        // Button theme this test exercises has to come from the real file.
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });

        var window = new Window { Content = button, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static Button Weighted(string weight)
    {
        var button = new Button { Content = "go" };
        if (weight.Length > 0)
        {
            button.Classes.Add(weight);
        }

        return button;
    }

    private static Border Shape(Button button) =>
        button.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Shape");

    private static Color Resource(string key) =>
        ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Colour(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    [AvaloniaTheory]
    [MemberData(nameof(Weights))]
    public void AtRestItIsAFlatTileWithNoOutlineCornerOrGlow(string weight)
    {
        var button = Weighted(weight);
        Open(button);

        var shape = Shape(button);
        Assert.Equal(default, shape.BorderThickness);
        Assert.Equal(default, shape.CornerRadius);
        Assert.Equal(Resource(ThemeManager.TileKey), Colour(shape.Background));
        Assert.Empty(button.GetVisualDescendants().OfType<BloomStack>());

        var ink = weight == "destructive" ? ThemeManager.RedKey : ThemeManager.AKey;
        Assert.Equal(Resource(ink), Colour(button.Foreground));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Weights))]
    public void KeyboardFocusFillsItSolid(string weight)
    {
        var button = Weighted(weight);
        Open(button);

        button.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        var (fill, ink) = weight == "destructive"
            ? (ThemeManager.RedKey, ThemeManager.WhiteKey)
            : (ThemeManager.AKey, ThemeManager.KnockKey);
        Assert.Equal(Resource(fill), Colour(Shape(button).Background));
        Assert.Equal(Resource(ink), Colour(button.Foreground));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Weights))]
    public void DisabledIsSlabWithGreyInk(string weight)
    {
        var button = Weighted(weight);
        button.IsEnabled = false;
        Open(button);

        Assert.Equal(Resource(ThemeManager.SlabKey), Colour(Shape(button).Background));
        Assert.Equal(Resource(ThemeManager.Grey2Key), Colour(button.Foreground));
        Assert.Equal(1, button.Opacity);
    }

    [AvaloniaFact]
    public void TheLabelIsDrawnInCapitals()
    {
        var button = new Button { Content = "Rescan" };
        Open(button);

        Assert.Equal("RESCAN", button.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Equal("Rescan", button.Content);
    }

    [AvaloniaFact]
    public void ControlContentIsDrawnAsItself()
    {
        var picture = new Border { Width = 40, Height = 40 };
        var button = new Button { Content = new Grid { Children = { picture } } };
        Open(button);

        Assert.Contains(picture, button.GetVisualDescendants());
        Assert.DoesNotContain(button.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("GRID") == true);
    }

    [AvaloniaFact]
    public void ABorderTheCallerSetsStillDraws()
    {
        var button = new Button { Content = "ship", BorderThickness = new Thickness(2), BorderBrush = Brushes.Orange };
        Open(button);

        Assert.Equal(new Thickness(2), Shape(button).BorderThickness);
        Assert.Equal(Colors.Orange, Colour(Shape(button).BorderBrush));
    }
}
