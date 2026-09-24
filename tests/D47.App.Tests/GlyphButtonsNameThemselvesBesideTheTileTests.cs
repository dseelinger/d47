using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A glyph button opens a label beside its tile on hover or keyboard focus, naming it from
/// AutomationProperties.Name, closes it when hover or focus ends or the button hides, and never
/// opens it while disabled. No glyph button carries a tooltip, and each
/// has a hit target of at least 44 x 44. A text field is outlined in A, and in Cyan while focused (#396).
/// </summary>
public class GlyphButtonsNameThemselvesBesideTheTileTests
{
    private static ThemeManager Manager() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaFact]
    public void HoveringAGlyphButtonOpensItsLabelInCapitals()
    {
        using var kit = AppLook.ControlKit();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton("Reset ship voices");
        var window = Open(button);

        Assert.False(Label(button).IsOpen);

        window.MouseMove(button.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Label(button).IsOpen);
        Assert.Equal("RESET SHIP VOICES", LabelText(button).Text);

        window.Close();
    }

    [AvaloniaFact]
    public void TheLabelClosesWhenThePointerLeaves()
    {
        using var kit = AppLook.ControlKit();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton("Reset ship voices");
        var window = Open(button);

        window.MouseMove(button.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Label(button).IsOpen);

        window.MouseMove(new Point(390, 290));
        Dispatcher.UIThread.RunJobs();

        Assert.False(Label(button).IsOpen);

        window.Close();
    }

    [AvaloniaFact]
    public void TheLabelClosesWhenTheButtonHidesUnderThePointer()
    {
        using var kit = AppLook.ControlKit();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton("Reset ship voices");
        var window = Open(button);

        window.MouseMove(button.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Label(button).IsOpen);

        button.IsVisible = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Label(button).IsOpen);

        window.Close();
    }

    [AvaloniaFact]
    public void TheLabelClosesWhenFocusMovesOn()
    {
        using var kit = AppLook.ControlKit();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton("Reset ship voices");
        var next = new TextBox { Width = 200 };
        var window = Open(button, next);

        button.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Label(button).IsOpen);

        next.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Label(button).IsOpen);

        window.Close();
    }

    [AvaloniaFact]
    public void KeyboardFocusOpensTheLabelAndFillsTheTile()
    {
        using var kit = AppLook.ControlKit();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton("Reset ship voices");
        var window = Open(button);

        button.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Label(button).IsOpen);

        var a = ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.AKey]!).Color;
        Assert.Equal(a, ((ISolidColorBrush)Tile(button).Background!).Color);

        window.Close();
    }

    [AvaloniaFact]
    public void ADisabledGlyphButtonOpensNoLabel()
    {
        using var kit = AppLook.ControlKit();
        Manager().Apply(ThemeCatalog.Elite);

        var button = GlyphButton("Reset ship voices");
        button.IsEnabled = false;
        var window = Open(button);

        window.MouseMove(button.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Label(button).IsOpen);

        window.Close();
    }

    [AvaloniaFact]
    public void EveryGlyphButtonInSettingsHasLabelTextNoTooltipAndA44Target()
    {
        using var kit = AppLook.ControlKit();
        var (settings, viewState, paths) = TestSurface.Create();
        Manager().FollowSettings(settings);
        var host = SettingsHost.Open(settings, viewState, paths);
        var theme = (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!;
        var arrow = (ControlTheme)Application.Current!.FindResource("D47.StepperArrow")!;

        var glyphs = host.View.GetVisualDescendants().OfType<Button>()
            .Where(button => ReferenceEquals(button.Theme, theme) || ReferenceEquals(button.Theme, arrow))
            .ToList();

        Assert.Contains(glyphs, button => button is RepeatButton);
        Assert.Contains(glyphs, button => button is not RepeatButton);

        Assert.All(glyphs, button =>
        {
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            Assert.Null(ToolTip.GetTip(button));
        });

        var laidOut = glyphs.Where(button => button.IsEffectivelyVisible && button.Bounds.Width > 0).ToList();
        Assert.NotEmpty(laidOut);
        Assert.All(laidOut, button =>
        {
            Assert.True(button.Bounds.Width >= 44, $"{AutomationProperties.GetName(button)} is {button.Bounds.Width} wide");
            Assert.True(button.Bounds.Height >= 44, $"{AutomationProperties.GetName(button)} is {button.Bounds.Height} tall");
        });

        host.Close();
    }

    [AvaloniaFact]
    public void AFieldIsOutlinedInAAtRestAndCyanWhileFocused()
    {
        using var kit = AppLook.ControlKit();
        Manager().Apply(ThemeCatalog.Elite);

        var field = new TextBox { Width = 200 };
        var window = Open(field);

        var a = ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.AKey]!).Color;
        var cyan = ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.CyanKey]!).Color;
        var grey2 = ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.Grey2Key]!).Color;

        Assert.Equal(new Thickness(1), field.BorderThickness);
        Assert.Equal(a, ((ISolidColorBrush)field.BorderBrush!).Color);
        Assert.Equal(grey2, ((ISolidColorBrush)field.PlaceholderForeground!).Color);

        field.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(cyan, ((ISolidColorBrush)field.BorderBrush!).Color);

        window.Close();
    }

    private static Button GlyphButton(string name)
    {
        var button = new Button
        {
            Theme = (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!,
            Content = "↺",
        };

        AutomationProperties.SetName(button, name);
        return button;
    }

    private static Popup Label(Button button) =>
        button.GetVisualDescendants().OfType<Popup>().Single(popup => popup.Name == "Label");

    private static TextBlock LabelText(Button button) =>
        (TextBlock)((Border)Label(button).Child!).Child!;

    private static Border Tile(Button button) =>
        button.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Tile");

    private static Window Open(params Control[] content)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.AddRange(content);

        var window = new Window
        {
            Content = panel,
            Width = 400,
            Height = 300,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
