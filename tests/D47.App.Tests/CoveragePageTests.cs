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
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Storage;
using D47.Core.Coverage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The coverage list.</summary>
[Trait("Category", "Integration")]
public class CoveragePageTests
{
    private static readonly DateTimeOffset Monday = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Three brushes, all already in every theme.</summary>
    [AvaloniaFact]
    public void EachStateCarriesItsOwnColour()
    {
        var window = Open(Report());

        Assert.Equal(Brush(window, ThemeManager.BlueKey), MarkBrush(window, "DONE"));
        Assert.Equal(Brush(window, ThemeManager.RedKey), MarkBrush(window, "FAILED"));
        Assert.Equal(Brush(window, ThemeManager.GreyKey), MarkBrush(window, "NEVER"));

        // Stale reads as work remaining, because the last run proved nothing about what is there now.
        Assert.Equal(Brush(window, ThemeManager.GreyKey), MarkBrush(window, "CHANGED"));

        window.Close();
    }

    /// <summary>Colour cannot be the only signal.</summary>
    [AvaloniaFact]
    public void AFailureIsDistinguishableWithoutRelyingOnColour()
    {
        var window = Open(Report());

        Assert.Equal(FontWeight.Bold, Mark(window, "FAILED").FontWeight);
        Assert.Equal(FontWeight.Normal, Mark(window, "DONE").FontWeight);

        window.Close();
    }

    /// <summary>Every line, not only the interesting ones.</summary>
    [AvaloniaFact]
    public void EveryItemInTheReportGetsALine()
    {
        var report = Report();
        var window = Open(report);

        var names = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .ToList();

        Assert.All(report.Lines, line => Assert.Contains(line.Item.Name.ToUpperInvariant(), names));

        window.Close();
    }

    /// <summary>
    /// Knowing a tool has never been tried is half an answer; the other half is what it was supposed to
    /// do.
    /// </summary>
    [AvaloniaFact]
    public void EveryLineLinksToItsCapabilitysHelpPage()
    {
        var report = Report();
        var window = Open(report);

        var links = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Name == "CoverageHelp")
            .Select(button => button.Tag as string)
            .ToList();

        Assert.Equal(report.Total, links.Count);

        Assert.All(
            report.Lines,
            line => Assert.Contains(DocsSite.Capability(line.Item.CapabilityId), links));

        window.Close();
    }

    /// <summary>
    /// Something that ran and errored is a stronger call to action than something never tried, so it
    /// leads — and it appears once, not again under its status further down.
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

        Assert.Equal("▾ CAME BACK WITH AN ERROR (1)", headings[0]);
        Assert.Equal("▾ NEVER TESTED (1)", headings[1]);

        var names = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Count(block => block.Text == "PROBE - BROKE");

        Assert.Equal(1, names);

        window.Close();
    }

    /// <summary>The way in exists only when this process was asked to record.</summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheDiagnosticsRowOffersTheListOnlyWhenRecording(bool recording)
    {
        // The summary is what makes the row exist; the report is what the button opens.
        var (settings, viewState, paths) = TestSurface.Create(
            recording ? () => Report().Summary : null);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, recording ? Report : null);

        // Diagnostics' own area (#220).
        host.View.Reveal(D47.Core.Capabilities.Builtin.DiagnosticsCapability.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var offered = host.View.GetVisualDescendants()
            .OfType<Button>()
            .Any(button => button.Name == "OpenCoverage");

        Assert.Equal(recording, offered);

        host.Close();
    }

    /// <summary>A settings line has a 44px Go button that hands its item back; a tool line has none.</summary>
    [AvaloniaFact]
    public void ASettingLineHasAGoButtonAndAToolLineHasNone()
    {
        Theme();

        var tool = Item("tool");
        var setting = new CoverageItem(CoverageKind.Setting, "diagnostics.row", "diagnostics", "Probe - row", "aaaa");
        var report = new CoverageLedger().Report([tool, setting]);
        var went = new List<CoverageItem>();
        var window = new CoveragePage(report, go: went.Add);

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var go = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => button.Name == "CoverageGo");

        Assert.Equal(44, go.MinHeight);

        go.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([setting], went);

        window.Close();
    }

    /// <summary>A tool line whose tool has a page gets a Go button; one without a page gets none.</summary>
    [AvaloniaFact]
    public void AToolLineWithAPageHasAGoButton()
    {
        Theme();

        var withPage = new CoverageItem(CoverageKind.Tool, "search_stations", "galaxy", "Galaxy - search_stations", "aaaa");
        var report = new CoverageLedger().Report([Item("tool"), withPage]);
        var went = new List<CoverageItem>();
        var window = new CoveragePage(report, go: went.Add);

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var go = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => button.Name == "CoverageGo");

        go.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([withPage], went);

        window.Close();
    }

    /// <summary>Go closes the Features page and lands on the setting's row, marked.</summary>
    [AvaloniaFact]
    public void GoClosesThePageAndLandsOnTheRow()
    {
        var key = D47.Core.Capabilities.Builtin.SpeechCapability.ChatterboxVoiceKey;
        var item = new CoverageItem(CoverageKind.Setting, key, "speech", "Speech - voice", "aaaa");
        var (settings, viewState, paths) = TestSurface.Create(() => "x");

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(D47.Core.Capabilities.Builtin.SpeechCapability.ProviderKey, D47.Core.Audio.TtsProviderCatalog.ChatterboxId, SettingsCaller.Panel);

        var host = SettingsHost.Open(settings, viewState, paths, () => new CoverageLedger().Report([item]));

        host.View.Reveal(D47.Core.Capabilities.Builtin.DiagnosticsCapability.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var click = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);

        host.View.GetVisualDescendants().OfType<Button>().First(b => b.Name == "OpenCoverage").RaiseEvent(click);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var go = host.Panel.GetVisualDescendants().OfType<Button>().First(b => b.Name == "CoverageGo");
        go.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(host.Panel.GetVisualDescendants(), c => c.Name == "CoverageGo");
        Assert.NotNull(host.View.LandedOn);

        host.Close();
    }

    /// <summary>Writes a PNG per theme for a human to look at.</summary>
    [AvaloniaFact]
    public void TheListRendersToACaptureAtRealSize()
    {
        var settings = Theme();

        foreach (var theme in D47.Core.Interface.ThemeCatalog.All)
        {
            if (theme.Id == D47.Core.Interface.ThemeCatalog.ElitePaletteId)
            {
                // Depends on the Commander's own HUD matrix file; the plain Elite palette is captured anyway.
                continue;
            }

            settings.Apply("ui.theme", theme.Id, SettingsCaller.Panel);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var window = Shown(RealisticReport());

            var frame = TopLevel.GetTopLevel(window)!.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.SaveCapture($"coverage-{theme.Id}.png");

            // The far end, where the exercised lines are.
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.ScrollToEnd();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            TopLevel.GetTopLevel(window)!.CaptureRenderedFrame()!.SaveCapture($"coverage-{theme.Id}-bottom.png");

            window.Close();
        }
    }

    /// <summary>
    /// And the row that opens it, in the card it lives on — the other half of this change, and the half
    /// no assertion about a button's existence tells you the look of.
    /// </summary>
    [AvaloniaFact]
    public void TheDiagnosticsRowRendersToACapture()
    {
        var (settings, viewState, paths) = TestSurface.Create(() => Report().Summary);

        // Cards remember whether they were left open, and Diagnostics defaults to closed — a capture of a
        // collapsed card would show the header and none of the row.
        viewState.Save(viewState.Load().With("diagnostics", expanded: true));

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, Report);

        var scroller = host.View.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.ScrollToEnd();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        host.Window.CaptureRenderedFrame()!.SaveCapture("coverage-row.png");

        host.Close();
    }

    /// <summary>Pressing a heading hides its rows and nothing else.</summary>
    [AvaloniaFact]
    public void PressingASectionHeadingHidesAndShowsItsRows()
    {
        var window = Open(Report());
        var heading = Heading(window, "never");

        Assert.Equal("▾ NEVER TESTED (1)", Text(heading));
        Assert.True(Mark(window, "NEVER").IsEffectivelyVisible);

        Press(heading);
        Assert.Equal("▸ NEVER TESTED (1)", Text(heading));
        Assert.False(Mark(window, "NEVER").IsEffectivelyVisible);
        Assert.True(Mark(window, "DONE").IsEffectivelyVisible);
        Assert.Equal("Never tested, collapsed", Avalonia.Automation.AutomationProperties.GetName(heading));

        Press(heading);
        Assert.True(Mark(window, "NEVER").IsEffectivelyVisible);

        window.Close();
    }

    /// <summary>The state is read back by the next page built over the same store.</summary>
    [AvaloniaFact]
    public void ACollapsedSectionStaysCollapsedOnTheNextVisit()
    {
        Theme();
        var paths = new AppPaths(TempFolders.Create("d47-coverage-sections"));
        var store = new ViewStateStore(paths, new DiskFileSystem(), NullLogger<ViewStateStore>.Instance);

        var first = new CoveragePage(Report(), store: store);
        first.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Press(Heading(first, "exercised"));
        first.Close();

        var second = new CoveragePage(Report(), store: new ViewStateStore(paths, new DiskFileSystem(), NullLogger<ViewStateStore>.Instance));
        second.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(Mark(second, "DONE").IsEffectivelyVisible);
        Assert.True(Mark(second, "NEVER").IsEffectivelyVisible);

        second.Close();
    }

    /// <summary>A redraw after Clear keeps each section as it was left.</summary>
    [AvaloniaFact]
    public void ClearingARowKeepsTheCollapsedSections()
    {
        Theme();
        var report = Report();
        var window = new CoveragePage(report, _ => { }, () => report);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Press(Heading(window, "exercised"));
        Press(window.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == "CoverageClear"));

        Assert.False(Mark(window, "DONE").IsEffectivelyVisible);

        window.Close();
    }

    private static void Press(Avalonia.Controls.Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A capture with the Tested section collapsed.</summary>
    [AvaloniaFact]
    public void CaptureWithASectionCollapsed()
    {
        Theme();
        var page = new CoveragePage(RealisticReport());
        var window = new Window { Content = page, Width = 720, Height = 720 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Press(Heading(page, "never"));
        window.CaptureRenderedFrame()!.SaveCapture("coverage-section-collapsed.png");
        window.Close();
    }

    private static Avalonia.Controls.Button Heading(Visual window, string key) =>
        window.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(b => b.Name == $"CoverageSection_{key}");

    private static string Text(Avalonia.Controls.Button heading) => ((TextBlock)heading.Content!).Text!;

    /// <summary>A themed window.</summary>
    private static CoveragePage Open(CoverageReport report)
    {
        Theme();
        return Shown(report);
    }

    private static CoveragePage Shown(CoverageReport report)
    {
        var window = new CoveragePage(report);
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
    /// The shape the window is actually for: most of it never exercised, a handful done, one or two
    /// that broke.
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

            // Most of it untouched, a handful done, one or two broken — the shape the window is actually for.
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
