using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// "Show every setting" is a checkbox tile in the page bar beside the filter field, not a row on any
/// page (#60, #435).
/// </summary>
public class TheFoldToggleIsInThePageBarTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static SettingsHost Folded(out SettingsService settings)
    {
        var (created, viewState, paths) = TestSurface.Create();
        settings = created;

        var host = SettingsHost.Open(created, viewState, paths);

        // SettingsHost shows the whole page for every test that is about a row.
        created.Apply(InterfaceCapability.ShowEverySettingKey, "false", SettingsCaller.Panel);
        Jobs();

        SettingsPageReading.Open(host.View, "language-model");

        return host;
    }

    /// <summary>Every tile named for the toggle, anywhere in the panel.</summary>
    private static List<CheckBox> Tiles(SettingsHost host) =>
        [.. host.Panel.GetVisualDescendants().OfType<CheckBox>()
            .Where(box => box.IsEffectivelyVisible && AutomationProperties.GetName(box) == "Show every setting")];

    /// <summary>A mouse press and release at a distance along the tile.</summary>
    private static void ClickAt(SettingsHost host, CheckBox tile, double x)
    {
        var at = tile.TranslatePoint(new Point(x, tile.Bounds.Height / 2), host.Window)!.Value;

        host.Window.MouseDown(at, MouseButton.Left);
        host.Window.MouseUp(at, MouseButton.Left);
        Jobs();
    }

    private static List<string> Drawn(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(block.Text))
            .Select(block => block.Text!)];

    /// <summary>On screen with the page folded — which is the state it exists to get a Commander out of.</summary>
    [AvaloniaFact]
    public void TheToggleIsDrawnOnceInThePageBar()
    {
        var host = Folded(out _);

        var tile = Assert.Single(Tiles(host));

        Assert.Contains(host.Panel.FindControl<Control>("SearchRow")!, tile.GetVisualAncestors());
        Assert.DoesNotContain(Page(host.View), tile.GetVisualAncestors());

        host.Close();
    }

    /// <summary>It sits in the same row as the filter field.</summary>
    [AvaloniaFact]
    public void TheToggleSitsBesideTheFilterField()
    {
        var host = Folded(out _);

        var tile = Tiles(host).Single();
        var field = host.Panel.FindControl<TextBox>("SearchInput")!;

        var tileMiddle = tile.TranslatePoint(new Point(0, tile.Bounds.Height / 2), host.Panel)!.Value.Y;
        var fieldTop = field.TranslatePoint(new Point(0, 0), host.Panel)!.Value.Y;

        Assert.InRange(tileMiddle, fieldTop, fieldTop + field.Bounds.Height);

        host.Close();
    }

    /// <summary>A click anywhere on the tile flips the setting, and the folded rows follow.</summary>
    [AvaloniaFact]
    public void ClickingTheTileFlipsTheSettingAndTheFoldedRowsFollow()
    {
        var host = Folded(out var settings);
        var tile = Tiles(host).Single();

        Assert.False(tile.IsChecked);

        var folded = Drawn(Page(host.View)).Count;

        // On the words, at the far end from the box.
        ClickAt(host, tile, tile.Bounds.Width - 6);

        Assert.True(settings.Current.Ui.ShowEverySetting);
        Assert.True(Drawn(Page(host.View)).Count > folded, "the folded rows did not appear");

        // And on the box.
        ClickAt(host, tile, 20);

        Assert.False(settings.Current.Ui.ShowEverySetting);
        Assert.Equal(folded, Drawn(Page(host.View)).Count);

        host.Close();
    }

    /// <summary>The page still folds.</summary>
    [AvaloniaFact]
    public void TheRestOfThePageIsStillFolded()
    {
        var host = Folded(out _);

        var drawn = Drawn(Page(host.View));

        Assert.Contains("Provider", drawn, StringComparer.Ordinal);
        Assert.DoesNotContain("Endpoint", drawn, StringComparer.Ordinal);

        host.Close();
    }

    /// <summary>Another tab carries no tile.</summary>
    [AvaloniaFact]
    public void AnotherTabShowsNoTile()
    {
        var host = Folded(out _);

        host.Panel.Tab = PanelTab.Transcript;
        Jobs();

        Assert.Empty(Tiles(host));

        host.Close();
    }
}
