using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>A control pressed in the headset is at least <see cref="TypeScale.MinimumTarget"/> tall (#1077).</summary>
[Trait("Category", "Integration")]
public class EveryPressedPanelControlIsFortyFourTallTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static void IsTallEnough(Control control, string what) =>
        Assert.True(
            control.Bounds.Height >= TypeScale.MinimumTarget,
            $"{what} is {control.Bounds.Height}px tall, under {TypeScale.MinimumTarget}.");

    private static SettingsHost Show(string section)
    {
        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(
            voices:
            [
                .. Enumerable.Range(1, 6).Select(i => new VoiceInfo($"voice-{i:00}", $"Voice number {i}", "en-GB", "female")),
            ],
            audition: (_, _, _) => Task.CompletedTask);

        var host = SettingsHost.Open(settings, viewState, paths, width: 800, height: 500);
        Open(host.View, section);
        return host;
    }

    [AvaloniaFact]
    public void TheInlinePickerButtonAndItsOptionsAreFortyFourTall()
    {
        var host = Show("voice");
        var picker = Page(host.View).GetVisualDescendants().OfType<InlinePicker>().Single();

        IsTallEnough(picker.GetVisualDescendants().OfType<Button>().Single(b => b.Name == InlinePicker.ButtonName), "the picker's button");

        picker.IsOpen = true;
        Jobs();

        var options = picker.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains(InlinePicker.OptionClass)).ToList();

        Assert.NotEmpty(options);
        Assert.All(options, option => IsTallEnough(option, "a picker option"));

        host.Window.Close();
    }

    [AvaloniaFact]
    public void EveryLevelCellIsFortyFourTallAndTheTrackIsNotClipped()
    {
        var (settings, viewState, _) = TestSurface.Create();
        viewState.Save(viewState.Load().With("log-levels", expanded: true));

        var view = new SettingsView();
        view.Attach(settings, viewState, tabPlaceId: "log-levels");

        var window = new Window { Content = view, Width = 900, Height = 900 };
        window.Show();
        Jobs();

        var track = view.GetVisualDescendants().OfType<SubsystemLevelTrack>().Single();
        var cells = track.GetVisualDescendants().OfType<Button>()
            .Where(b => b.IsEffectivelyVisible && (AutomationProperties.GetName(b) ?? string.Empty).Contains(": ", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(cells);
        Assert.All(cells, cell => IsTallEnough(cell, "a level cell"));

        foreach (var cell in cells)
        {
            var top = cell.TranslatePoint(default, track)!.Value.Y;
            Assert.True(top + cell.Bounds.Height <= track.Bounds.Height + 0.5, "a level cell runs past the track");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheBreadcrumbAbovePickerPageIsFortyFourTall()
    {
        var host = Show("voice");
        var voice = Page(host.View).GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Voice"))
            .GetVisualDescendants().OfType<Button>()
            .First(button => button.Content is DockPanel panel && panel.Children.OfType<TextBlock>().Any(text => text.Text == "▼"));

        voice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        var crumbs = host.Panel.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Theme == host.Panel.FindResource("D47.Crumb")).ToList();

        Assert.NotEmpty(crumbs);
        Assert.All(crumbs, crumb => IsTallEnough(crumb, "a breadcrumb"));

        host.Window.Close();
    }
}
