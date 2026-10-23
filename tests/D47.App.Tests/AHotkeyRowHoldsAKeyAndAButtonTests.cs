using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each hotkey row draws a stick button beside its key, on one control (#1550). Captures go to <see cref="TestSurface.CaptureDirectory"/>.</summary>
[Trait("Category", "Integration")]
public sealed class AHotkeyRowHoldsAKeyAndAButtonTests
{
    private const string Stick = "hid:stick";

    private static Grid? Row(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.IsEffectivelyVisible)
            .FirstOrDefault(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    private static IReadOnlyList<string?> Chips(Grid row) =>
        row.GetVisualDescendants().OfType<Border>()
            .Where(chip => chip.Classes.Contains(SettingsView.BindingChipClass) && chip.IsEffectivelyVisible)
            .Select(chip => (chip.Child as TextBlock)?.Text)
            .ToList();

    [AvaloniaTheory]
    [InlineData(InterfaceCapability.FindHotkeyKey, InterfaceCapability.FindButtonKey, "Find", "find-key-and-button.png")]
    [InlineData(InterfaceCapability.ShowOverlayHotkeyKey, InterfaceCapability.ShowOverlayButtonKey, "Show or hide the overlay", "overlay-key-and-button.png")]
    public void TheRowShowsTheKeyAndTheButtonAndNoRowOfItsOwnForTheButton(string key, string buttonKey, string label, string file)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(buttonKey, $"{Stick}#4", SettingsCaller.Panel);

        var host = SettingsHost.Open(settings, viewState, paths, width: 820, height: 1100);
        host.View.ShowPlaceOf(key);
        Dispatcher.UIThread.RunJobs();

        host.Window.CaptureRenderedFrame()!.SaveCapture(file);

        var row = Row(host, label);
        Assert.True(row is not null, $"the {label} row is not on the page");

        var chips = Chips(row!);
        Assert.Equal(2, chips.Count);
        Assert.Equal("BUTTON 5", chips[1]);
        Assert.Null(Row(host, label + " button"));

        host.Close();
    }
}
