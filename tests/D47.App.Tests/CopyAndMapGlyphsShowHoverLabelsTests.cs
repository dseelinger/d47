using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The copy and map glyphs name themselves on hover in the window and not on the headset's panel (#2718).</summary>
public class CopyAndMapGlyphsShowHoverLabelsTests
{
    private static Task<bool> Copy(string value) => Task.FromResult(true);

    private static (Window Window, Control Host) Hosted(Control glyph, bool headset)
    {
        if (!headset)
        {
            var plain = new StackPanel { Margin = new Thickness(20), Children = { glyph } };
            var window = new Window { Content = plain, Width = 400, Height = 300 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            return (window, plain);
        }

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Classes.Add("headset");
        var hosting = new Window { Content = panel, Width = 800, Height = 600 };
        hosting.Show();
        Dispatcher.UIThread.RunJobs();
        panel.GetVisualDescendants().OfType<StackPanel>().First().Children.Add(glyph);
        Dispatcher.UIThread.RunJobs();

        return (hosting, panel);
    }

    private static Button FirstButton(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetName(button) == name);

    [AvaloniaFact]
    public void AMapGlyphInTheWindowHasTheTipShowOnGalaxyMap()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var glyph = MapGlyph.For(() => { });
        var (window, _) = Hosted(glyph, headset: false);

        Assert.Equal("Show on galaxy map", GlyphLabel.GetTip(glyph));
        Assert.Equal("Show on galaxy map", AutomationProperties.GetName(glyph));

        window.MouseMove(glyph.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        var label = glyph.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().Single(popup => popup.Name == "Label");
        Assert.True(label.IsOpen);
        Assert.Equal("SHOW ON GALAXY MAP", ((TextBlock)((Border)label.Child!).Child!).Text);

        window.Close();
    }

    [AvaloniaFact]
    public void AMapGlyphOnTheBigPanelHasNoTip()
    {
        using var kit = AppLook.ControlKit();

        var glyph = MapGlyph.For(() => { });
        var (window, _) = Hosted(glyph, headset: true);

        Assert.Equal(string.Empty, GlyphLabel.GetTip(glyph));
        Assert.Equal("Show on galaxy map", AutomationProperties.GetName(glyph));

        window.Close();
    }

    [AvaloniaFact]
    public void ACopyGlyphBesideASystemNameInTheWindowHasTheTipCopySystemName()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var beside = CopyGlyph.Beside(new TextBlock { Text = "Sol" }, "Sol", Copy);
        var (window, host) = Hosted(beside, headset: false);
        var glyph = FirstButton(host, CopyGlyph.Name);

        Assert.Equal("Copy system name", GlyphLabel.GetTip(glyph));

        window.MouseMove(glyph.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        var label = glyph.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().Single(popup => popup.Name == "Label");
        Assert.Equal("COPY SYSTEM NAME", ((TextBlock)((Border)label.Child!).Child!).Text);

        window.Close();
    }

    [AvaloniaFact]
    public void ACopyGlyphBesideASystemNameOnTheBigPanelHasNoTip()
    {
        using var kit = AppLook.ControlKit();

        var beside = CopyGlyph.LinkBeside(new TextBlock { Text = "Sol" }, "Sol", Copy);
        var (window, host) = Hosted(beside, headset: true);
        var glyph = FirstButton(host, CopyGlyph.Name);

        Assert.Equal(string.Empty, GlyphLabel.GetTip(glyph));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePressChangesTheAccessibleNameAndLeavesTheTip()
    {
        using var kit = AppLook.ControlKit();

        var glyph = CopyGlyph.For("Sol", Copy);
        var (window, _) = Hosted(glyph, headset: false);

        CopyGlyph.Show(glyph, worked: true);

        Assert.Equal(CopyGlyph.Copied, AutomationProperties.GetName(glyph));
        Assert.Equal("Copy system name", GlyphLabel.GetTip(glyph));

        CopyGlyph.Show(glyph, worked: false);

        Assert.Equal(CopyGlyph.Failed, AutomationProperties.GetName(glyph));
        Assert.Equal("Copy system name", GlyphLabel.GetTip(glyph));

        window.Close();
    }
}
