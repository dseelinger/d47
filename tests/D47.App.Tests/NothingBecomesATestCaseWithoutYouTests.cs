using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Diagnostics.Flight;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The half of the audio flight recorder that earns it
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>): turning a row into a
/// regression test, from the window a Commander does it in.
/// <para>
/// The property under test is the adoption gate, and it is the same one
/// <a href="https://github.com/dseelinger/d47/issues/162">#162</a> established for donated
/// corpora. A recording says what happened and cannot say what should have happened; the
/// expected value is the Commander's own word, and without it there is no test case to keep —
/// so the button refuses rather than filing a pair that asserts nothing.
/// </para>
/// </summary>
public class NothingBecomesATestCaseWithoutYouTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-flight-keep", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private FlightLog Log() => new(_folder, NullLogger.Instance);

    private static FlightCapture Heard(string text) =>
        new(FlightDirection.Heard, Noon, WavWriter.ToBytes(new float[16_000], 16_000), TimeSpan.FromSeconds(1))
        {
            Text = text,
        };

    private static FlightCapture Said(string text, string phonemes) =>
        new(FlightDirection.Spoken, Noon, WavWriter.ToBytes(new float[16_000], 16_000), TimeSpan.FromSeconds(1))
        {
            Text = text,
            Phonemes = phonemes,
            Provider = "Kokoro (on this machine)",
        };

    /// <summary>
    /// A themed window, for the reason the coverage list's tests are themed: without it every
    /// dynamic resource falls back and the surface under test is not the drawn one.
    /// </summary>
    private static FlightRecorderWindow Open(FlightLog log)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(TestSurface.Create().Settings);

        var window = new FlightRecorderWindow(log, () => Noon);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static T Named<T>(Visual surface, string name)
        where T : Visual =>
        surface.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    /// <summary>Picks the first row in the list, which is what puts a detail pane on screen.</summary>
    private static void Select(FlightRecorderWindow window)
    {
        window.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.Name == "FlightRow")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(FlightRecorderWindow window, string name)
    {
        Named<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void A_heard_row_becomes_a_mishear_case_with_the_words_you_type()
    {
        var log = Log();
        log.Add(Heard("set course for Colonel"));

        var window = Open(log);

        Select(window);
        Named<TextBox>(window, "FlightExpected").Text = "set course for Colonia";
        Press(window, "FlightKeep");

        var row = Assert.Single(log.Rows);

        Assert.NotNull(row.Kept);
        Assert.Equal(FlightKeepKind.Mishear, row.Kept.Kind);
        Assert.Equal("set course for Colonia", row.Kept.Expected);

        using var corpus = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(_folder, FlightLog.KeptFolderName, "mishears.json")));

        Assert.Single(corpus.RootElement.EnumerateArray());

        window.Close();
    }

    /// <summary>
    /// A said row is the other corpus, and the direction alone decides which — a Commander does
    /// not have to know that a mishear and a mispronunciation are filed differently.
    /// </summary>
    [AvaloniaFact]
    public void A_said_row_becomes_a_pronunciation_case()
    {
        var log = Log();
        log.Add(Said("Observatory.", "ɒbzɜːveɪ"));

        var window = Open(log);

        Select(window);
        Named<TextBox>(window, "FlightExpected").Text = "əbzˈɜːvətɹi";
        Press(window, "FlightKeep");

        var row = Assert.Single(log.Rows);

        Assert.Equal(FlightKeepKind.Pronunciation, row.Kept!.Kind);
        Assert.True(File.Exists(Path.Combine(_folder, FlightLog.KeptFolderName, "pronunciations.json")));

        window.Close();
    }

    /// <summary>
    /// Nothing typed, nothing kept — and the window says which half is missing rather than
    /// leaving a button that appears to have done nothing.
    /// </summary>
    [AvaloniaFact]
    public void An_empty_expectation_keeps_nothing_and_says_why()
    {
        var log = Log();
        log.Add(Heard("set course for Colonel"));

        var window = Open(log);

        Select(window);
        Press(window, "FlightKeep");

        Assert.Null(Assert.Single(log.Rows).Kept);
        Assert.False(Directory.Exists(Path.Combine(_folder, FlightLog.KeptFolderName)));

        Assert.Contains(
            "Type what you actually said first",
            Named<TextBlock>(window, "FlightKept").Text ?? string.Empty,
            StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The phoneme string is on the pane, in full. It is the column the feature exists for, and
    /// a diagnosis trimmed to the width of a list is not one.
    /// </summary>
    [AvaloniaFact]
    public void The_phonemes_are_shown_beside_the_line_that_produced_them()
    {
        var log = Log();
        log.Add(Said("Observatory.", "ɒbzɜːveɪ"));

        var window = Open(log);

        Select(window);

        var shown = string.Join(
            "\n",
            window.GetVisualDescendants()
                .OfType<SelectableTextBlock>()
                .Select(block => block.Text));

        Assert.Contains("ɒbzɜːveɪ", shown, StringComparison.Ordinal);

        window.Close();
    }
}
