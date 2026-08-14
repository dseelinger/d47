using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The control stays inside the column the row gave it (list.md Phase 4).
/// <para>
/// <see cref="RowWidthTests"/> holds the column to two fifths of the row. It does not hold the
/// control to the column, and a ComboBox asks for the width of its widest item — so an
/// ElevenLabs account with several hundred voices, each named "Bill - Wise, Mature, Balanced",
/// put a box on the screen that ran past the edge of the panel it was in.
/// </para>
/// </summary>
public class VoiceRowFitsItsColumnTests
{
    /// <summary>
    /// What layout rounding is allowed to add. A combo box comes out a pixel over its column and
    /// always has; the bug this file is about was eighty-seven of them, and a bound that argues
    /// about the first one would be a test about Avalonia's rounding rather than about the row.
    /// </summary>
    private const double Rounding = 1.0;

    /// <summary>
    /// An account's list, in the shape the real one has: a descriptor on every name, a gender
    /// and an accent behind it, and one entry considerably longer than the rest.
    /// </summary>
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("bill", "Bill - Wise, Mature, Balanced", "american", "male"),
        new("george", "George - Warm, Captivating Storyteller", "british", "male"),
        new(
            "long",
            "Christopher - Deliberate, Authoritative, Documentary Narration And Long Form Reading",
            "transatlantic",
            "male"),
    ];

    [AvaloniaFact]
    public void TheVoiceRowsControlDoesNotRunPastItsColumn()
    {
        var window = Open();
        var row = RowFor(window, "Voice");
        var column = row.ColumnDefinitions[2].ActualWidth;

        // The button, not the panel holding it. The panel was always the width of the column;
        // it was the button inside that ran out past both — 345 pixels in a column of 258, and
        // further with every longer voice name on the account.
        var button = row.GetVisualDescendants().OfType<Button>().First();

        Assert.True(
            button.Bounds.Width <= column + Rounding,
            $"the control is {button.Bounds.Width:0} wide in a column of {column:0}");

        window.Close();
    }

    /// <summary>
    /// The same bound for every compact row, so no future list of choices can reintroduce this
    /// by being long. Rows are only measured once laid out; a collapsed section has no width.
    /// </summary>
    [AvaloniaFact]
    public void NoCompactRowLetsItsControlRunPastItsColumn()
    {
        var window = Open();

        var laidOut = window.GetVisualDescendants().OfType<Grid>()
            .Where(grid => IsCompactRow(grid) && grid.Bounds.Width > 0)
            .ToList();

        Assert.NotEmpty(laidOut);

        foreach (var row in laidOut)
        {
            var column = row.ColumnDefinitions[2].ActualWidth;

            foreach (var control in row.GetVisualDescendants().OfType<Control>()
                .Where(child => child is Button or ComboBox or TextBox or NumericUpDown))
            {
                Assert.True(
                    control.Bounds.Width <= column + Rounding,
                    $"the {Caption(row)} row's control is {control.Bounds.Width:0} wide "
                    + $"in a column of {column:0}");
            }
        }

        window.Close();
    }

    /// <summary>
    /// The label the box cannot hold is still reachable, which is the rule the speech model row
    /// already established: what the box clips, the tooltip carries.
    /// </summary>
    [AvaloniaFact]
    public void TheWholeVoiceNameIsOnTheTooltip()
    {
        var window = Open();

        var button = RowFor(window, "Voice").GetVisualDescendants().OfType<Button>().First();

        Assert.Contains("Bill", ToolTip.GetTip(button) as string ?? string.Empty, StringComparison.Ordinal);

        window.Close();
    }

    private static SettingsWindow Open()
    {
        var (settings, viewState, paths) = TestSurface.Create(voices: Voices());

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths);
        window.Show();

        settings.Apply(SpeechCapability.VoiceKey, "bill", SettingsCaller.Panel);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static string Caption(Grid row) =>
        row.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? "unnamed";

    private static Grid RowFor(SettingsWindow window, string label) =>
        window.GetVisualDescendants().OfType<Grid>()
            .Where(IsCompactRow)
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    /// <summary>
    /// A compact settings row: words, a fixed gap, control. Three columns alone is not enough of
    /// a description — the window has other three-column grids, and one of them holds a 16-pixel
    /// column of its own with something 474 pixels wide sitting in it quite legitimately.
    /// </summary>
    private static bool IsCompactRow(Grid grid) =>
        grid.ColumnDefinitions.Count == 3
        && grid.ColumnDefinitions[0].Width.IsStar
        && grid.ColumnDefinitions[1].Width.IsAbsolute
        && grid.ColumnDefinitions[2].Width.IsStar;
}
