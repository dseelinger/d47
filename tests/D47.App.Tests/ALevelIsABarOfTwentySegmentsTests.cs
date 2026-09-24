using Avalonia;
using Avalonia.Controls;
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

/// <summary>A 0–1 settings row is a bar of segments, one per 0.05, with its value beside it (#440).</summary>
public class ALevelIsABarOfTwentySegmentsTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open(out SettingsService settings, double width = 1180)
    {
        (settings, var viewState, var paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        return SettingsHost.Open(settings, viewState, paths, width: width, height: 800);
    }

    /// <summary>The bar that writes <paramref name="key"/>: moved one step, it is the one whose setting changes.</summary>
    private static Level LevelOn(SettingsHost host, SettingsService settings, string key)
    {
        host.View.ShowPlaceOf(key);
        Jobs();

        foreach (var level in host.View.GetVisualDescendants().OfType<Level>().Where(level => level.IsEffectivelyVisible))
        {
            var before = settings.Read(key);

            level.Focus();
            host.Window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
            Jobs();

            var moved = settings.Read(key) != before;

            host.Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            Jobs();

            if (moved)
            {
                return level;
            }
        }

        throw new InvalidOperationException($"no bar writes {key}");
    }

    /// <summary>Clicks the middle of segment <paramref name="segment"/>, counted from 1.</summary>
    private static void Click(SettingsHost host, Level level, int segment)
    {
        var bar = level.GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count == 20);
        var x = bar.Bounds.Width / 20 * (segment - 0.5);
        var point = bar.TranslatePoint(new Point(x, bar.Bounds.Height / 2), host.Window)!.Value;

        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        Jobs();
    }

    [Theory]
    [InlineData(17, 0.0, 0.85)]
    [InlineData(20, 0.0, 1.0)]
    [InlineData(1, 0.0, 0.05)]
    [InlineData(1, 0.1, 0.1)]
    [InlineData(2, 0.1, 0.1)]
    [InlineData(3, 0.1, 0.15)]
    public void ASegmentSetsItsWorthButNeverLessThanTheMinimum(int segment, double minimum, double expected) =>
        Assert.Equal(expected, Level.ValueAt(segment, 0.05, minimum, 1), 6);

    [Theory]
    [InlineData(0.85, 1, 0.9)]
    [InlineData(1.0, 1, 1.0)]
    [InlineData(0.15, -1, 0.1)]
    [InlineData(0.1, -1, 0.1)]
    public void AnArrowKeyStepsOneSegmentAndStopsAtTheEnds(double from, int direction, double expected) =>
        Assert.Equal(expected, Level.Stepped(from, direction, 0.05, 0.1, 1), 6);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(199, 20)]
    [InlineData(165, 17)]
    [InlineData(500, 20)]
    public void APointFallsInTheSegmentUnderIt(double x, int expected) =>
        Assert.Equal(expected, Level.SegmentAt(x, 200, 20));

    [AvaloniaFact]
    public void AnAudioLevelIsTwentySegmentsAndClickingTheSeventeenthWritesPointEightFive()
    {
        using var _ = AppLook.ControlKit();
        var host = Open(out var settings);
        var key = AudioCapability.LevelKey(AudioChannel.Cue);
        var level = LevelOn(host, settings, key);

        Assert.Equal(20, level.GetVisualDescendants().OfType<Grid>().Single(grid => grid.ColumnDefinitions.Count == 20).Children.Count);
        Assert.Equal("1.00", level.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "LevelReadout").Text);

        Click(host, level, 17);

        Assert.Equal("0.85", settings.Read(key));

        host.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetOpacityCannotBeSetBelowItsMinimumFromTheBar()
    {
        using var _ = AppLook.ControlKit();
        var host = Open(out var settings);

        settings.Apply(VrCapability.EnabledKey, "true", SettingsCaller.Panel);
        Jobs();

        var level = LevelOn(host, settings, VrCapability.OpacityKey);

        Assert.Equal(0.1, level.Minimum, 6);

        Click(host, level, 1);

        Assert.Equal("0.1", settings.Read(VrCapability.OpacityKey));

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1180)]
    [InlineData(924)]
    public void SoundsAndLevelsAreCaptured(double width)
    {
        using var look = AppLook.ControlKit();
        var host = Open(out var settings, width);

        LevelOn(host, settings, AudioCapability.LevelKey(AudioChannel.Cue));

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"level-bar-sounds-and-levels-{width}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }
}
