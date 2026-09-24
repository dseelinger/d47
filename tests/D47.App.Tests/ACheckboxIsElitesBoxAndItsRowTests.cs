using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Elite's checkbox (#395): a 16px outlined box, an 8px mark when checked, and one tile row with its
/// label that toggles from anywhere on it, by mouse, Space or Enter.
/// </summary>
public sealed class ACheckboxIsElitesBoxAndItsRowTests
{
    [AvaloniaFact]
    public void TheMarkShowsOnlyWhenChecked()
    {
        using var look = AppLook.Put();
        var (box, window) = Shown("Include journal history");

        Assert.False(Part<Border>(box, "Mark").IsVisible);

        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var mark = Part<Border>(box, "Mark");
        Assert.True(mark.IsVisible);
        Assert.Equal(8, mark.Bounds.Width);
        Assert.Equal(16, Part<Border>(box, "Box").Bounds.Width);
        Assert.Equal(Colour(ThemeManager.AKey), Solid(Part<Border>(box, "Box").BorderBrush));

        window.Close();
    }

    [AvaloniaFact]
    public void ADisabledBoxIsOutlinedInGrey()
    {
        using var look = AppLook.Put();
        var (box, window) = Shown("Unavailable");

        box.IsEnabled = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Colour(ThemeManager.Grey2Key), Solid(Part<Border>(box, "Box").BorderBrush));

        window.Close();
    }

    [AvaloniaFact]
    public void ClickingTheLabelTicksTheBox()
    {
        using var look = AppLook.Put();
        var (box, window) = Shown("Include journal history");
        var label = (TextBlock)box.Content!;

        var middle = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(box.IsChecked);
        Assert.True(box.Bounds.Height >= 44);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(Key.Space, PhysicalKey.Space, " ")]
    [InlineData(Key.Enter, PhysicalKey.Enter, "\r")]
    public void TheKeyboardTicksAndUnticksIt(Key key, PhysicalKey physical, string symbol)
    {
        using var look = AppLook.Put();
        var (box, window) = Shown("Include journal history");
        box.Focus(NavigationMethod.Tab);

        window.KeyPress(key, RawInputModifiers.None, physical, symbol);
        window.KeyRelease(key, RawInputModifiers.None, physical, symbol);
        Dispatcher.UIThread.RunJobs();
        Assert.True(box.IsChecked);

        window.KeyPress(key, RawInputModifiers.None, physical, symbol);
        window.KeyRelease(key, RawInputModifiers.None, physical, symbol);
        Dispatcher.UIThread.RunJobs();
        Assert.False(box.IsChecked);

        window.Close();
    }

    [AvaloniaFact]
    public void TabbingToItLightsTheRow()
    {
        using var look = AppLook.Put();
        var (box, window) = Shown("Include journal history");

        Assert.Equal(Colour(ThemeManager.TileKey), Solid(Part<Border>(box, "Row").Background));

        box.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Colour(ThemeManager.Tile2Key), Solid(Part<Border>(box, "Row").Background));

        window.Close();
    }

    [AvaloniaFact]
    public void LabelFirstPutsTheBoxAfterTheLabel()
    {
        using var look = AppLook.Put();
        var (box, window) = Shown("Keyboard", labelFirst: true);
        var label = (TextBlock)box.Content!;

        var boxLeft = Part<Border>(box, "Box").TranslatePoint(default, box)!.Value.X;
        var labelLeft = label.TranslatePoint(default, box)!.Value.X;

        Assert.True(boxLeft > labelLeft, $"box at {boxLeft}, label at {labelLeft}");

        window.Close();
    }

    /// <summary>A settings toggle is the same box and row, drawn as a tile with its help on the label (#441).</summary>
    [AvaloniaFact]
    public void ASettingsToggleIsThisRowAsATile()
    {
        using var look = AppLook.Put();
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        SettingsPageReading.Open(host.View, "voice-input");

        var box = (CheckBox)host.View.ControlFor(D47.Core.Capabilities.Builtin.ListeningCapability.EchoKey)!;
        var label = (TextBlock)box.Content!;
        var tile = box.FindAncestorOfType<Control>(includeSelf: false)!;

        Assert.Contains("sentence", box.Classes);
        Assert.DoesNotContain("bare", box.Classes);
        Assert.True(box.Bounds.Height >= 44);
        Assert.IsType<TextBlock>(ToolTip.GetTip(label));
        Assert.Equal(3, ((Border)tile).BorderThickness.Left);
        Assert.Equal(Colors.Transparent, Solid(((Border)tile).BorderBrush));

        host.Close();
    }

    /// <summary>Checked, unchecked, focused and disabled rows on each theme, for a human to look at.</summary>
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void EveryStateOnEveryTheme(string themeId)
    {
        var (on, _) = LabeledCheckBox.Build("Include journal history", labelFirst: false);
        on.IsChecked = true;
        var (off, _) = LabeledCheckBox.Build("Hide on-foot engineers", labelFirst: false);
        var (focused, _) = LabeledCheckBox.Build("Tabbed to", labelFirst: false);
        var (disabled, _) = LabeledCheckBox.Build("Disabled and checked", labelFirst: false);
        disabled.IsChecked = true;
        disabled.IsEnabled = false;

        var column = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(24),
            Width = 360,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Children =
            {
                new CheckBox { Content = "Raw", IsChecked = true },
                new CheckBox { Content = "Keyboard" },
                on,
                off,
                focused,
                disabled,
                new CheckBox { IsChecked = true, Classes = { "bare" } },
            },
        };

        focused.Loaded += (_, _) => focused.Focus(NavigationMethod.Tab);

        var path = AppLook.Capture(column, $"checkbox-{themeId}.png", themeId, width: 420, height: 440);

        Assert.True(File.Exists(path));
    }

    private static (CheckBox Box, Window Window) Shown(string label, bool labelFirst = false)
    {
        var (box, _) = LabeledCheckBox.Build(label, labelFirst);
        box.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;

        var window = new Window { Content = box, Width = 400, Height = 120 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (box, window);
    }

    private static T Part<T>(CheckBox box, string name) where T : Control =>
        box.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static Color Colour(string key) =>
        Solid((IBrush?)Application.Current!.FindResource(key));

    private static Color Solid(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
