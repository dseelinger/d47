using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Configuration;
using D47.Core.Coverage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The coverage list. The card carries the one-line summary; this carries the ninety-odd lines
/// behind it, which is why it is a window and not more of the Diagnostics card.
/// </summary>
public class CoverageWindowTests
{
    private static readonly DateTimeOffset Monday = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;

    public CoverageWindowTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Three brushes, all already in every theme. There is no green in the palette, so exercised
    /// is Accent rather than an invented colour needing a value in all five themes.
    /// </summary>
    [AvaloniaFact]
    public void EachStateCarriesItsOwnColour()
    {
        var window = Open(Report());

        Assert.Equal(Brush(window, ThemeManager.AccentKey), MarkBrush(window, "done"));
        Assert.Equal(Brush(window, ThemeManager.DangerKey), MarkBrush(window, "failed"));
        Assert.Equal(Brush(window, ThemeManager.TextMutedKey), MarkBrush(window, "never"));

        // Stale reads as work remaining, because the last run proved nothing about what is
        // there now. The word carries the difference, not a fourth colour.
        Assert.Equal(Brush(window, ThemeManager.TextMutedKey), MarkBrush(window, "changed"));

        window.Close();
    }

    /// <summary>
    /// Colour cannot be the only signal. One of the five themes builds its palette from the
    /// Commander's own HUD matrix, so no two brushes can be guaranteed to stay distinct — and
    /// even shipped, Elite's Accent (#FF7100) and Danger (#FF5555) are both warm and sixty rows
    /// of scrolling apart. A failure has to be findable without trusting the palette.
    /// </summary>
    [AvaloniaFact]
    public void AFailureIsDistinguishableWithoutRelyingOnColour()
    {
        var window = Open(Report());

        Assert.Equal(FontWeight.Bold, Mark(window, "failed").FontWeight);
        Assert.Equal(FontWeight.Normal, Mark(window, "done").FontWeight);

        window.Close();
    }

    /// <summary>
    /// Every line, not only the interesting ones. A list that quietly omits a state is a list
    /// that reports the app as smaller than it is.
    /// </summary>
    [AvaloniaFact]
    public void EveryItemInTheReportGetsALine()
    {
        var report = Report();
        var window = Open(report);

        var names = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .ToList();

        Assert.All(report.Lines, line => Assert.Contains(line.Item.Name, names));

        window.Close();
    }

    /// <summary>
    /// Knowing a tool has never been tried is half an answer; the other half is what it was
    /// supposed to do. Every line carries the capability id, so every line has somewhere to go.
    /// </summary>
    [AvaloniaFact]
    public void EveryLineLinksToItsCapabilitysHelpPage()
    {
        var report = Report();
        var window = Open(report);

        var links = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Name == "CoverageHelp")
            .Select(button => ToolTip.GetTip(button) as string)
            .ToList();

        Assert.Equal(report.Total, links.Count);

        Assert.All(
            report.Lines,
            line => Assert.Contains(DocsSite.Capability(line.Item.CapabilityId), links));

        window.Close();
    }

    /// <summary>
    /// Something that ran and errored is a stronger call to action than something never tried,
    /// so it leads — and it appears once, not again under its status further down.
    /// </summary>
    [AvaloniaFact]
    public void TheListLeadsWithFailuresAndShowsEachLineOnce()
    {
        var window = Open(Report());

        var headings = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.EndsWith(')') && text.Contains(" ("))
            .ToList();

        Assert.Equal("Came back with an error (1)", headings[0]);
        Assert.Equal("Never exercised (1)", headings[1]);

        var names = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Count(block => block.Text == "Probe - broke");

        Assert.Equal(1, names);

        window.Close();
    }

    /// <summary>
    /// The way in exists only when this process was asked to record. On a normal run the row is
    /// not registered at all, so there is nothing to open and no button — absent rather than
    /// present and doing nothing.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheDiagnosticsRowOffersTheListOnlyWhenRecording(bool recording)
    {
        // The summary is what makes the row exist; the report is what the button opens. A run
        // that records has both, a normal run has neither.
        var (settings, viewState, paths) = TestSurface.Create(
            recording ? () => Report().Summary : null);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths, recording ? Report : null);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var offered = window.GetVisualDescendants()
            .OfType<Button>()
            .Any(button => button.Name == "OpenCoverage");

        Assert.Equal(recording, offered);

        window.Close();
    }

    /// <summary>
    /// Writes a PNG per theme for a human to look at. Ninety-four rows is the point of the
    /// window, so the capture uses ninety-four — a list that reads well at four and falls apart
    /// at ninety is a list nobody found out about until the day they needed it.
    /// </summary>
    [AvaloniaFact]
    public void TheListRendersToACaptureAtRealSize()
    {
        var output = TestSurface.CaptureDirectory;
        _output.WriteLine($"Captures: {output}");

        var settings = Theme();

        foreach (var theme in D47.Core.Interface.ThemeCatalog.All)
        {
            if (theme.Id == D47.Core.Interface.ThemeCatalog.ElitePaletteId)
            {
                // Depends on the Commander's own HUD matrix file; the plain Elite palette is
                // captured anyway.
                continue;
            }

            settings.Apply("ui.theme", theme.Id, SettingsCaller.Panel);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var window = Shown(RealisticReport());

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(output, $"coverage-{theme.Id}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

            // The far end, where the exercised lines are. The first screenful is all failures
            // and never-tried by design, so a capture that stops there never shows the one
            // colour the accent brush is carrying.
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.ScrollToEnd();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            window.CaptureRenderedFrame()!.Save(
                Path.Combine(output, $"coverage-{theme.Id}-bottom.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

            window.Close();
        }
    }

    /// <summary>
    /// And the row that opens it, in the card it lives on — the other half of this change, and
    /// the half no assertion about a button's existence tells you the look of.
    /// </summary>
    [AvaloniaFact]
    public void TheDiagnosticsRowRendersToACapture()
    {
        var output = TestSurface.CaptureDirectory;
        _output.WriteLine($"Captures: {output}");

        var (settings, viewState, paths) = TestSurface.Create(() => Report().Summary);

        // Cards remember whether they were left open, and Diagnostics defaults to closed — a
        // capture of a collapsed card would show the header and none of the row.
        viewState.Save(viewState.Load().With("diagnostics", expanded: true));

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths, Report);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.ScrollToEnd();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(output, "coverage-row.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// A themed window. The theme has to be applied for the colour assertions to mean anything:
    /// without it every DynamicResource falls back to the default brush and a test comparing
    /// two unresolved lookups passes while telling you nothing.
    /// </summary>
    private static CoverageWindow Open(CoverageReport report)
    {
        Theme();
        return Shown(report);
    }

    private static CoverageWindow Shown(CoverageReport report)
    {
        var window = new CoverageWindow(report);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static SettingsService Theme()
    {
        var settings = TestSurface.Create().Settings;

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return settings;
    }

    /// <summary>One line in each of the four states.</summary>
    private static CoverageReport Report()
    {
        var ledger = new CoverageLedger();

        ledger.Record(Item("worked"), Monday, CoverageOutcome.Ok);
        ledger.Record(Item("broke"), Monday, CoverageOutcome.Failed);
        ledger.Record(Item("moved", "before"), Monday, CoverageOutcome.Ok);

        return ledger.Report(
            [Item("worked"), Item("broke"), Item("moved", "after"), Item("untouched")]);
    }

    /// <summary>
    /// The shape the window is actually for: most of it never exercised, a handful done, one
    /// or two that broke.
    /// </summary>
    private static CoverageReport RealisticReport()
    {
        string[] capabilities = ["diagnostics", "listening", "speech", "conversation"];

        var items = new List<CoverageItem>();
        var ledger = new CoverageLedger();

        for (var i = 0; i < 94; i++)
        {
            var isRow = i % 3 == 0;
            var capability = capabilities[i % capabilities.Length];

            var item = new CoverageItem(
                isRow ? CoverageKind.Setting : CoverageKind.Tool,
                $"{(isRow ? "row" : "tool")}_{i:00}",
                capability,
                $"{char.ToUpperInvariant(capability[0])}{capability[1..]} - something_or_other_{i:00}",
                "aaaa");

            items.Add(item);

            // Most of it untouched, a handful done, one or two broken — the shape the window
            // is actually for.
            if (i % 7 < 2)
            {
                ledger.Record(item, Monday, i % 21 == 0 ? CoverageOutcome.Failed : CoverageOutcome.Ok);
            }
        }

        return ledger.Report(items);
    }

    private static CoverageItem Item(string id, string fingerprint = "aaaa") =>
        new(CoverageKind.Tool, id, "diagnostics", $"Probe - {id}", fingerprint);

    private static IBrush? Brush(Visual window, string key) => window.FindResource(key) as IBrush;

    private static IBrush? MarkBrush(Visual window, string mark) => Mark(window, mark).Foreground;

    private static TextBlock Mark(Visual window, string mark) =>
        window.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == mark);
}
