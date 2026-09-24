using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A compact row puts the caption and the control side by side.</summary>
public class RowWidthTests
{
    /// <summary>The longest choice label on the surface, and the one that broke the row.</summary>
    private const string LongestLabel = "small.en";

    /// <summary>The label column's width (<c>SettingsView.LabelColumnWidth</c>, #437).</summary>
    private const double LabelColumnMaxWidth = 240;

    /// <summary>What layout rounding is allowed to add.</summary>
    private const double Rounding = 1.0;

    [AvaloniaFact]
    public void ALongChoiceLabelDoesNotSqueezeOutTheCaption()
    {
        var host = OpenWith(LongestLabel);

        var row = CompactRowFor(host, "Speech model");
        var label = row.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Speech model");

        var caption = row.ColumnDefinitions[0].ActualWidth;

        // The caption is sized to what the label actually needs, not squeezed to whatever the control
        // left behind (#332).
        Assert.True(
            caption >= label.Bounds.Width - Rounding,
            $"the caption is {caption:0} wide but the label itself measured {label.Bounds.Width:0}");

        host.Close();
    }

    /// <summary>
    /// The same maximum applies to every compact row, so no future label can reintroduce raggedness by
    /// being verbose (#332).
    /// </summary>
    [AvaloniaFact]
    public void NoCompactRowLetsItsCaptionColumnPastTheCeiling()
    {
        var host = OpenWith(LongestLabel);

        var laidOut = CompactRows(host).Where(row => row.Bounds.Width > 0).ToList();

        Assert.NotEmpty(laidOut);

        foreach (var row in laidOut)
        {
            Assert.True(
                row.ColumnDefinitions[0].ActualWidth <= LabelColumnMaxWidth + Rounding,
                $"a row gave its caption {row.ColumnDefinitions[0].ActualWidth:0}, "
                + $"past the {LabelColumnMaxWidth:0} maximum");
        }

        host.Close();
    }

    /// <summary>A label longer than the caption column wraps inside it rather than running under the control.</summary>
    [AvaloniaFact]
    public void NoLabelRunsUnderItsControl()
    {
        var host = OpenWith(LongestLabel);

        var laidOut = CompactRows(host).Where(row => row.Bounds.Width > 0).ToList();

        Assert.NotEmpty(laidOut);

        foreach (var row in laidOut)
        {
            var column = row.ColumnDefinitions[0].ActualWidth;
            var widest = row.Children.Where(child => Grid.GetColumn(child) == 0)
                .SelectMany(child => child.GetVisualDescendants().OfType<TextBlock>())
                .Where(text => text.IsVisible)
                .Select(text => text.Bounds.Width)
                .DefaultIfEmpty(0)
                .Max();

            Assert.True(
                widest <= column + Rounding,
                $"a label in a {column:0} wide caption column measured {widest:0}");
        }

        host.Close();
    }

    /// <summary>
    /// The row that broke, captured with the offending label selected, so "does it look right now" has
    /// an artifact to answer with rather than needing the app driven by hand.
    /// </summary>
    [AvaloniaFact]
    public void TheSpeechModelRowRendersToACapture()
    {
        var host = OpenWith(LongestLabel);

        CompactRowFor(host, "Speech model").BringIntoView();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "settings-speech-model.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }

    /// <summary>
    /// The reset gutter is a column of the row's own grid, reserved whether or not this row draws a
    /// reset — so it holds the same width on a row that draws one and a row that does not (#332).
    /// </summary>
    [AvaloniaFact]
    public void TheResetGutterIsTheSameWidthWithOrWithoutAReset()
    {
        var host = OpenWith(LongestLabel);

        var laidOut = CompactRows(host).Where(row => row.Bounds.Width > 0).ToList();

        Assert.NotEmpty(laidOut);

        var gutters = laidOut.Select(row => row.ColumnDefinitions[^1].ActualWidth).Distinct().ToList();

        Assert.True(
            gutters.Count == 1,
            $"the reset gutter varies across rows: {string.Join(", ", gutters.Select(w => w.ToString("0")))}");

        host.Close();
    }

    /// <summary>
    /// The whole label is reachable off the value cell even though the closed box cannot hold it — the
    /// stepper's own tooltip, not the whole control's (#382).
    /// </summary>
    [AvaloniaFact]
    public void TheWholeChoiceLabelIsOnTheTooltipWhenTheBoxClipsIt()
    {
        var host = OpenWith(LongestLabel);

        var combo = CompactRowFor(host, "Speech model")
            .GetVisualDescendants()
            .OfType<D47.App.Controls.Stepper>()
            .First();

        var value = combo.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "StepperValue");

        var tip = ToolTip.GetTip(value) as string;

        Assert.False(string.IsNullOrWhiteSpace(tip), "The stepper's value cell carries no tooltip.");
        Assert.Contains("to download", tip, StringComparison.Ordinal);

        host.Close();
    }

    private static SettingsHost OpenWith(string model)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        settings.Apply("listening.model", model, SettingsCaller.Panel);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    /// <summary>The rows the settings view builds, found by the class it marks them with.</summary>
    private static IEnumerable<Grid> CompactRows(SettingsHost host) =>
        host.View.GetVisualDescendants()
            .OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass));

    private static Grid CompactRowFor(SettingsHost host, string label) =>
        CompactRows(host).First(grid =>
            grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
}
