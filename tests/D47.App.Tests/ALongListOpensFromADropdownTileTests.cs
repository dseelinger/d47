using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A Choice row with more than <see cref="SettingsView.LongListThreshold"/> options is a ▼ tile that opens
/// the picker page (#439).
/// </summary>
public class ALongListOpensFromADropdownTileTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static IReadOnlyList<string> Devices(int count) =>
        [.. Enumerable.Range(1, count).Select(i => $"Microphone {i}")];

    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("en-GB-RyanNeural", "Ryan", "en-GB", "male"),
        new("en-GB-SoniaNeural", "Sonia", "en-GB", "female"),
    ];

    private static SettingsHost Open(int devices, out SettingsService settings, double width = 1180)
    {
        (settings, var viewState, var paths) = TestSurface.Create(voices: Voices(), inputDevices: Devices(devices));

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: 800);

        host.View.ShowPlaceOf(ListeningCapability.DeviceKey);
        Jobs();

        return host;
    }

    private static Grid Row(SettingsView view, string label) =>
        view.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    private static Button? Tile(SettingsView view, string label) =>
        Row(view, label).GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "DropdownTile");

    [AvaloniaFact]
    public void NineMicrophonesAreATileAndChoosingOneWritesItAndReturns()
    {
        var host = Open(9, out var settings);

        var tile = Tile(host.View, "Microphone");

        Assert.NotNull(tile);
        Assert.Empty(Row(host.View, "Microphone").GetVisualDescendants().OfType<Stepper>());

        tile!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        var page = host.Panel.GetVisualDescendants().OfType<PickerPage>().Single();

        Assert.True(host.Panel.Nav.Modal);

        page.GetControl<TextBox>("FilterBox").Text = "Microphone 6";
        Jobs();

        page.GetControl<Button>("AcceptButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.Equal("Microphone 6", settings.Read(ListeningCapability.DeviceKey));
        Assert.False(host.Panel.Nav.Modal);
        Assert.Empty(host.Panel.GetVisualDescendants().OfType<PickerPage>());
        Assert.Contains(
            Tile(host.View, "Microphone")!.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Microphone 6");

        host.Close();
    }

    [AvaloniaFact]
    public void SevenMicrophonesStayAStepper()
    {
        var host = Open(SettingsView.LongListThreshold, out _);

        Assert.Null(Tile(host.View, "Microphone"));
        Assert.NotEmpty(Row(host.View, "Microphone").GetVisualDescendants().OfType<Stepper>());

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1180)]
    [InlineData(924)]
    public void VoiceInputAndItsVoiceAreCaptured(double width)
    {
        var host = Open(9, out _, width);

        Capture(host.Window, $"dropdown-tile-voice-input-{width}");

        host.View.ShowPlaceOf(SpeechCapability.VoiceKey);
        Jobs();

        Assert.NotNull(Tile(host.View, "Voice"));

        Capture(host.Window, $"dropdown-tile-its-voice-{width}");

        host.Close();
    }

    private static void Capture(Window window, string name)
    {
        Jobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"{name}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
