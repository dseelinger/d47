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

/// <summary>A bind row draws a key slot and a controller button slot at fixed widths, under a KEY / CONTROLLER BUTTON header (#1551).</summary>
[Trait("Category", "Integration")]
public sealed class BindRowsDrawKeyAndButtonSlotsTests
{
    private static SettingsHost Open(string section)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        viewState.Save(viewState.Load() with { SettingsSection = section });

        return SettingsHost.Open(settings, viewState, paths, width: 1280, height: 700);
    }

    private static Grid Row(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.IsEffectivelyVisible)
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    private static List<Border> Chips(Grid row) =>
        row.GetVisualDescendants().OfType<Border>()
            .Where(chip => chip.Classes.Contains(SettingsView.BindingChipClass) && chip.IsEffectivelyVisible)
            .ToList();

    private static Button Named(Grid row, string name) =>
        row.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == name);

    [AvaloniaFact]
    public void EveryBindRowHoldsItsSlotsAndButtonsAtTheSameWidthAndPlace()
    {
        var host = Open("voice-input");
        Dispatcher.UIThread.RunJobs();

        var row = Row(host, "Push-to-talk");

        Assert.All(Chips(row), chip => Assert.Equal(SettingsView.BindSlotWidth, chip.Bounds.Width));
        Assert.Equal(SettingsView.BindButtonWidth, Named(row, "BIND").Bounds.Width);
        Assert.Equal(SettingsView.BindButtonWidth, Named(row, "CLEAR").Bounds.Width);

        host.Close();
    }

    [AvaloniaFact]
    public void ARowWithoutControllersPutsBindWhereARowWithOneDoes()
    {
        var host = Open("voice-input");
        host.View.ShowPlaceOf(InterfaceCapability.ShowOverlayHotkeyKey);
        Dispatcher.UIThread.RunJobs();

        var overlay = Row(host, "Show or hide the overlay");
        var chips = Chips(overlay);

        Assert.Single(chips);
        Assert.Equal(SettingsView.BindSlotWidth, chips[0].Bounds.Width);

        var bindX = Named(overlay, "BIND").TranslatePoint(default, overlay)!.Value.X;
        var slotsEnd = chips[0].TranslatePoint(default, overlay)!.Value.X + (2 * SettingsView.BindSlotWidth);

        Assert.True(bindX >= slotsEnd, $"BIND at {bindX} overlaps the reserved button slot ending at {slotsEnd}");

        host.Close();
    }

    [AvaloniaFact]
    public void AnEmptyKeySlotReadsUnbound()
    {
        var host = Open("voice-input");
        Dispatcher.UIThread.RunJobs();

        var row = Row(host, "Push-to-talk");
        Named(row, "CLEAR").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["UNBOUND"], Chips(row).Select(chip => (chip.Child as TextBlock)?.Text));

        host.Close();
    }

    [AvaloniaFact]
    public void EachGroupWithABindRowHasTheColumnHeader()
    {
        var host = Open("voice-input");
        Dispatcher.UIThread.RunJobs();

        var captions = host.View.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && text.Text is "KEY" or "CONTROLLER BUTTON")
            .Select(text => text.Text)
            .ToList();

        Assert.Contains("KEY", captions);
        Assert.Contains("CONTROLLER BUTTON", captions);

        host.Window.CaptureRenderedFrame()!.SaveCapture("bind-rows-slots.png");
        host.Close();
    }
}
