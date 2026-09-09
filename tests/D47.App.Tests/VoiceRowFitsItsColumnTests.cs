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

/// <summary>The control stays inside the column the row gave it.</summary>
public class VoiceRowFitsItsColumnTests
{
    /// <summary>What layout rounding is allowed to add.</summary>
    private const double Rounding = 1.0;

    /// <summary>
    /// An account's list, in the shape the real one has: a descriptor on every name, a gender and an
    /// accent behind it, and one entry considerably longer than the rest.
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
        var host = Open();
        var row = RowFor(host, "Voice");
        var column = row.ColumnDefinitions[2].ActualWidth;

        // The button, not the panel holding it.
        var button = row.GetVisualDescendants().OfType<Button>().First();

        Assert.True(
            button.Bounds.Width <= column + Rounding,
            $"the control is {button.Bounds.Width:0} wide in a column of {column:0}");

        host.Close();
    }

    /// <summary>
    /// The same bound for every compact row, so no future list of choices can reintroduce this by being
    /// long.
    /// </summary>
    [AvaloniaFact]
    public void NoCompactRowLetsItsControlRunPastItsColumn()
    {
        var host = Open();

        var laidOut = host.View.GetVisualDescendants().OfType<Grid>()
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

        host.Close();
    }

    /// <summary>
    /// The label the box cannot hold is still reachable, which is the rule the speech model row already
    /// established: what the box clips, the tooltip carries.
    /// </summary>
    [AvaloniaFact]
    public void TheWholeVoiceNameIsOnTheTooltip()
    {
        var host = Open();

        // Not the reset glyph or the info glyph beside the label — those are different buttons about
        // different things (#61, and the 2026-09-01 callout).
        var button = RowFor(host, "Voice").GetVisualDescendants().OfType<Button>()
            .First(control => !D47.App.Settings.SettingsView.IsRowChrome(control));

        Assert.Contains("Bill", ToolTip.GetTip(button) as string ?? string.Empty, StringComparison.Ordinal);

        host.Close();
    }

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create(voices: Voices());

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        settings.Apply(SpeechCapability.VoiceKey, "bill", SettingsCaller.Panel);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    private static string Caption(Grid row) =>
        row.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? "unnamed";

    private static Grid RowFor(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(IsCompactRow)
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    /// <summary>A compact settings row: words, a fixed gap, control.</summary>
    private static bool IsCompactRow(Grid grid) =>
        grid.ColumnDefinitions.Count == 3
        && grid.ColumnDefinitions[0].Width.IsStar
        && grid.ColumnDefinitions[1].Width.IsAbsolute
        && grid.ColumnDefinitions[2].Width.IsStar;
}
