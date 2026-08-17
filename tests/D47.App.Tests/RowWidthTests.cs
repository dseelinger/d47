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
        var host = OpenWith(LongestLabel);

        var row = CompactRowFor(host, "Speech model");

        var caption = row.ColumnDefinitions[0].ActualWidth;
        var control = row.ColumnDefinitions[2].ActualWidth;

        // The words get the larger share. Before the fix this was 23 pixels against 543.
        Assert.True(
            caption > control,
            $"the caption keeps the larger share of the row, but got {caption:0} against {control:0}");

        host.Close();
    }

    /// <summary>
    /// The same bound applies to every compact row, so no future label can reintroduce this by
    /// being verbose. Rows are only checked once laid out; a collapsed section has no width.
    /// </summary>
    [AvaloniaFact]
    public void NoCompactRowLetsItsControlTakeTheLargerShare()
    {
        var host = OpenWith(LongestLabel);

        var laidOut = CompactRows(host).Where(row => row.Bounds.Width > 0).ToList();

        Assert.NotEmpty(laidOut);

        foreach (var row in laidOut)
        {
            Assert.True(
                row.ColumnDefinitions[0].ActualWidth >= row.ColumnDefinitions[2].ActualWidth,
                $"a row gave {row.ColumnDefinitions[2].ActualWidth:0} to its control and only "
                + $"{row.ColumnDefinitions[0].ActualWidth:0} to its caption");
        }

        host.Close();
    }

    /// <summary>
    /// The row that broke, captured with the offending label selected, so "does it look right
    /// now" has an artifact to answer with rather than needing the app driven by hand.
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
    /// The whole label is reachable even though the closed box cannot hold it.
    /// <para>
    /// Reported from a running build: the Commander read "Small (English only) — more accu" and
    /// concluded a model was installed. The end of that label is "— about 466 MB to download",
    /// which is the half that says it is not. The control column is two fifths of the row and
    /// cannot be widened enough to hold it without starving the caption, so the tooltip carries
    /// what the box clips.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheWholeChoiceLabelIsOnTheTooltipWhenTheBoxClipsIt()
    {
        var host = OpenWith(LongestLabel);

        var combo = CompactRowFor(host, "Speech model")
            .GetVisualDescendants()
            .OfType<ComboBox>()
            .First();

        var tip = ToolTip.GetTip(combo) as string;

        Assert.False(string.IsNullOrWhiteSpace(tip), "The combo box carries no tooltip.");
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

    /// <summary>
    /// The rows the settings view builds, found by the class it marks them with.
    /// <para>
    /// This used to be "every grid with three columns", which is not the same set. Avalonia
    /// builds a <see cref="TextBox"/> out of a three-column grid — inner-left content, the text,
    /// inner-right content — so putting a glyph inside a box made a control template look like a
    /// settings row with a 26-pixel control and no caption at all. The selector was reporting on
    /// grids this view never built.
    /// </para>
    /// </summary>
    private static IEnumerable<Grid> CompactRows(SettingsHost host) =>
        host.View.GetVisualDescendants()
            .OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass));

    private static Grid CompactRowFor(SettingsHost host, string label) =>
        CompactRows(host).First(grid =>
            grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
}
