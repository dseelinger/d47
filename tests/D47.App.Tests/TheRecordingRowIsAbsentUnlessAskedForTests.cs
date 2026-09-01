using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Diagnostics.Recording;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The audio recorder's one settings row, through the surface a Commander actually sees
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>).
/// <para>
/// Asserted here rather than against the descriptor, because "entirely absent from the surface
/// unless enabled" is a claim about the drawn page. It is also the shape that has already let
/// two faults through: a null host delegate makes a row absent, and a row nothing draws is a row
/// no test can see.
/// </para>
/// </summary>
public class TheRecordingRowIsAbsentUnlessAskedForTests : IDisposable
{
    private const string WipeButton = "Press_privacy_audioFlight";

    private const string ReviewButton = "OpenAudioRecorder";

    private static readonly DateTimeOffset Noon = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-flight-row", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static IReadOnlyList<string> ButtonNames(Visual surface) =>
        [.. surface.GetVisualDescendants().OfType<Button>().Select(button => button.Name ?? string.Empty)];

    [AvaloniaFact]
    public void An_ordinary_run_has_no_row_at_all()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.DoesNotContain(WipeButton, ButtonNames(host.View));
        Assert.DoesNotContain(ReviewButton, ButtonNames(host.View));

        host.Close();
    }

    /// <summary>
    /// Recording, the row is there with both affordances — and the summary is the record's own,
    /// so the row states what is actually held rather than a fixed sentence.
    /// </summary>
    [AvaloniaFact]
    public void Recording_draws_the_summary_the_review_and_the_wipe()
    {
        var log = new RecordingLog(_folder, NullLogger.Instance);

        log.Add(new RecordingCapture(
            RecordingDirection.Heard,
            Noon,
            WavWriter.ToBytes(new float[16_000], 16_000),
            TimeSpan.FromSeconds(1))
        {
            Text = "set course for Colonia",
        });

        var (settings, viewState, paths) = TestSurface.Create(recording: log);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, recording: (log, () => Noon));

        Assert.Contains(ReviewButton, ButtonNames(host.View));
        Assert.Contains(WipeButton, ButtonNames(host.View));
        Assert.Contains("1 utterances", Disclosures(host.View), StringComparison.Ordinal);

        host.Close();
    }

    /// <summary>
    /// The wipe is the one act on this row that cannot be taken back, and it happens from the
    /// panel rather than from anything the model can reach — the row is an Info row, which
    /// <c>SettingsService.Apply</c> refuses outright.
    /// </summary>
    [AvaloniaFact]
    public void Pressing_the_wipe_deletes_the_recording_and_the_row_says_so()
    {
        var log = new RecordingLog(_folder, NullLogger.Instance);

        log.Add(new RecordingCapture(
            RecordingDirection.Heard,
            Noon,
            WavWriter.ToBytes(new float[16_000], 16_000),
            TimeSpan.FromSeconds(1))
        {
            Text = "set course for Colonia",
        });

        var (settings, viewState, paths) = TestSurface.Create(recording: log);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, recording: (log, () => Noon));

        var wipe = host.View.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Name == WipeButton);

        wipe.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(log.Rows);
        Assert.False(Directory.Exists(_folder));
        Assert.Contains("Nothing recorded", Disclosures(host.View), StringComparison.Ordinal);

        host.Close();
    }

    private static string Disclosures(Visual surface) =>
        string.Join(
            "\n",
            surface.GetVisualDescendants().OfType<SelectableTextBlock>().Select(block => block.Text));
}
