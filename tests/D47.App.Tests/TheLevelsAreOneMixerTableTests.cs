using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Sounds and levels draws every channel's level, mute and duck as one table (#444).</summary>
public class TheLevelsAreOneMixerTableTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open(out SettingsService settings, double width = 1180)
    {
        (settings, var viewState, var paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: 800);

        host.View.Reveal(AudioCapability.Id);
        Jobs();

        return host;
    }

    private static string Words(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;

    private static Control Mixer(SettingsHost host) =>
        host.View.GetVisualDescendants().OfType<Control>().Single(control => control.Name == SettingsView.MixerName);

    private static List<Grid> Channels(SettingsHost host) =>
        [.. host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.MixerRowClass) && grid.IsEffectivelyVisible)];

    private static string NameOf(Grid channel) =>
        Words(channel.Children.OfType<TextBlock>().First(text => Grid.GetColumn(text) == 0));

    private static Grid Channel(SettingsHost host, string name) => Channels(host).Single(grid => NameOf(grid) == name);

    /// <summary>Level, mute, duck: the controls in a channel row by column, the dash standing in for a missing one.</summary>
    private static Control Cell(Grid channel, int column) =>
        channel.Children.Single(child => Grid.GetColumn(child) == 2 + (2 * column) && Grid.GetRow(child) == 0);

    private static Button Reset(Grid channel) =>
        channel.Children.OfType<Button>().Single(button => button.Name == SettingsView.RowResetName);

    private static void Click(SettingsHost host, Control target, Point within)
    {
        var point = target.TranslatePoint(within, host.Window)!.Value;

        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        Jobs();
    }

    private static void ClickSegment(SettingsHost host, Level level, int segment)
    {
        var bar = level.GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count == 20);

        Click(host, bar, new Point(bar.Bounds.Width / 20 * (segment - 0.5), bar.Bounds.Height / 2));
    }

    [AvaloniaFact]
    public void FiveChannelsAreNamedInOrderUnderOneHeader()
    {
        using var look = AppLook.ControlKit();
        var host = Open(out _);

        Assert.Equal(
            ["Thinking bed", "Ambient music", "Sound cues", "Speech", "Alerts"],
            Channels(host).Select(NameOf));

        Assert.Equal(
            ["CHANNEL", "LEVEL", "MUTE", "DUCK WHILE D47 SPEAKS"],
            Mixer(host).GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.Classes.Contains(SettingsView.MixerHeaderClass))
                .Select(text => text.Text));

        host.Close();
    }

    [AvaloniaFact]
    public void OnlyTheChannelsThatDuckHaveADuckBar()
    {
        using var look = AppLook.ControlKit();
        var host = Open(out _);

        foreach (var channel in Channels(host))
        {
            var ducks = NameOf(channel) is "Thinking bed" or "Ambient music" or "Sound cues";

            Assert.IsType<Level>(Cell(channel, 0));
            Assert.IsType<CheckBox>(Cell(channel, 1));
            Assert.Equal(ducks, Cell(channel, 2) is Level);
            Assert.Equal(!ducks, Cell(channel, 2).Name == SettingsView.MixerAbsentName);
        }

        host.Close();
    }

    [AvaloniaFact]
    public void TheTenthSegmentOfSpeechWritesAHalfAndItsResetPutsItBack()
    {
        using var look = AppLook.ControlKit();
        var host = Open(out var settings);
        var key = AudioCapability.LevelKey(AudioChannel.Speech);
        var before = settings.Read(key);
        var speech = Channel(host, "Speech");

        Assert.False(Reset(speech).IsVisible);

        ClickSegment(host, (Level)Cell(speech, 0), 10);

        Assert.Equal(0.5, double.Parse(settings.Read(key)!, System.Globalization.CultureInfo.InvariantCulture), 6);
        Assert.True(Reset(speech).IsVisible);

        Click(host, Reset(speech), new Point(TypeScale.MinimumTarget / 2, TypeScale.MinimumTarget / 2));

        Assert.Equal(before, settings.Read(key));
        Assert.False(Reset(speech).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void TickingMuteOnAlertsMutesAlerts()
    {
        using var look = AppLook.ControlKit();
        var host = Open(out var settings);
        var mute = (CheckBox)Cell(Channel(host, "Alerts"), 1);

        Click(host, mute, new Point(mute.Bounds.Width / 2, mute.Bounds.Height / 2));

        Assert.Equal("true", settings.Read(AudioCapability.MuteKey(AudioChannel.Alert)));

        host.Close();
    }

    [AvaloniaFact]
    public void FilteringByAChannelNameLeavesThatChannelAndTheHeader()
    {
        using var look = AppLook.ControlKit();
        var host = Open(out _);

        host.View.Filter("music");
        Jobs();

        Assert.True(Mixer(host).IsEffectivelyVisible);
        Assert.Equal(["Ambient music"], Channels(host).Select(NameOf));

        host.Close();
    }

    [AvaloniaFact]
    public void ADuckSetElsewhereShowsInTheTable()
    {
        using var look = AppLook.ControlKit();
        var host = Open(out var settings);

        settings.Apply(AudioCapability.DuckKey(AudioChannel.Bed), "0.6", SettingsCaller.Panel);
        host.View.Refresh();
        Jobs();

        Assert.Equal(0.6, ((Level)Cell(Channel(host, "Thinking bed"), 2)).Value, 6);

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1180)]
    [InlineData(924)]
    public void EveryColumnLinesUpAcrossTheChannels(double width)
    {
        using var look = AppLook.ControlKit();
        var host = Open(out _, width);
        var header = (Grid)((StackPanel)Mixer(host)).Children[0];
        Grid[] grids = [header, .. Channels(host)];

        // Each column's left edge on the window, the same on the header and on every channel row.
        static string Edges(Grid grid, Visual window)
        {
            var x = grid.TranslatePoint(default, window)!.Value.X;
            var edges = new List<double>();

            foreach (var definition in grid.ColumnDefinitions)
            {
                edges.Add(Math.Round(x, 1));
                x += definition.ActualWidth;
            }

            return string.Join(",", edges);
        }

        Assert.Single(grids.Select(grid => Edges(grid, host.Window)).Distinct());
        Assert.Equal(9, header.ColumnDefinitions.Count);

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"mixer-sounds-and-levels-{width}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }
}
