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

/// <summary>
/// A compact row puts the caption and the control side by side. The control column used to be
/// Auto, which means "as wide as the control asks for" — and a ComboBox asks for the width of
/// its selected item. Selecting a speech model, whose labels are whole sentences, took nearly
/// the entire row and left the help text wrapping one character per line.
/// </summary>
public class RowWidthTests
{
    /// <summary>The longest choice label on the surface, and the one that broke the row.</summary>
    private const string LongestLabel = "small.en";

    [AvaloniaFact]
    public void ALongChoiceLabelDoesNotSqueezeOutTheCaption()
    {
        var window = OpenWith(LongestLabel);

        var row = CompactRowFor(window, "Speech model");

        var caption = row.ColumnDefinitions[0].ActualWidth;
        var control = row.ColumnDefinitions[2].ActualWidth;

        // The words get the larger share. Before the fix this was 23 pixels against 543.
        Assert.True(
            caption > control,
            $"the caption keeps the larger share of the row, but got {caption:0} against {control:0}");

        window.Close();
    }

    /// <summary>
    /// The same bound applies to every compact row, so no future label can reintroduce this by
    /// being verbose. Rows are only checked once laid out; a collapsed section has no width.
    /// </summary>
    [AvaloniaFact]
    public void NoCompactRowLetsItsControlTakeTheLargerShare()
    {
        var window = OpenWith(LongestLabel);

        var laidOut = CompactRows(window).Where(row => row.Bounds.Width > 0).ToList();

        Assert.NotEmpty(laidOut);

        foreach (var row in laidOut)
        {
            Assert.True(
                row.ColumnDefinitions[0].ActualWidth >= row.ColumnDefinitions[2].ActualWidth,
                $"a row gave {row.ColumnDefinitions[2].ActualWidth:0} to its control and only "
                + $"{row.ColumnDefinitions[0].ActualWidth:0} to its caption");
        }

        window.Close();
    }

    /// <summary>
    /// The row that broke, captured with the offending label selected, so "does it look right
    /// now" has an artifact to answer with rather than needing the app driven by hand.
    /// </summary>
    [AvaloniaFact]
    public void TheSpeechModelRowRendersToACapture()
    {
        var window = OpenWith(LongestLabel);

        CompactRowFor(window, "Speech model").BringIntoView();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "settings-speech-model.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    private static SettingsWindow OpenWith(string model)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths);
        window.Show();

        settings.Apply("listening.model", model, SettingsCaller.Panel);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IEnumerable<Grid> CompactRows(SettingsWindow window) =>
        window.GetVisualDescendants().OfType<Grid>().Where(grid => grid.ColumnDefinitions.Count == 3);

    private static Grid CompactRowFor(SettingsWindow window, string label) =>
        CompactRows(window).First(grid =>
            grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
}
