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

    /// <summary>
    /// Every kind of line has one 44px GO, and it opens the exercise dialog rather than navigating: a
    /// pageless tool, a tool with a page, a settings row on its page and one that is absent.
    /// </summary>
    [AvaloniaFact]
    public void EveryLineHasOneGoAndItOpensTheExerciseDialog()
    {
        Theme();

        var report = new CoverageLedger().Report([Item("tool"), WithPage, Row("diagnostics.row"), Row("diagnostics.absent")]);
        var went = new List<CoverageItem>();
        var page = new CoveragePage(report, go: went.Add, onPage: key => key != "diagnostics.absent");
        var window = page.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = Gos(page);

        Assert.Equal(report.Total, buttons.Count);
        Assert.All(buttons, b => Assert.Equal(TypeScale.MinimumTarget, b.MinHeight));

        foreach (var go in buttons)
        {
            Press(go);

            var dialog = Assert.Single(window.Modals().OfType<ToolExerciseDialog>());

            Press(DialogButton(dialog, ToolExerciseDialog.CloseName)!);
        }

        Assert.Empty(went);
        Assert.Empty(window.Modals());

        window.Close();
    }

    /// <summary>
    /// GO THERE is offered for a tool with a page and a settings row on its page now, and not for a
    /// pageless tool or an absent conditional row; it is a 44px target and hands its item back.
    /// </summary>
    [AvaloniaFact]
    public void GoThereIsOfferedOnlyWhereTheLineHasAPlace()
    {
        Theme();

        var pageless = Item("tool");
        var row = Row("diagnostics.row");
        var absent = Row("diagnostics.absent");
        var report = new CoverageLedger().Report([pageless, WithPage, row, absent]);
        var went = new List<CoverageItem>();
        var page = new CoveragePage(report, go: went.Add, onPage: key => key != absent.Id);
        var window = page.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        bool Offers(CoverageItem item)
        {
            Press(GoOf(page, item));

            var dialog = Assert.Single(window.Modals().OfType<ToolExerciseDialog>());
            var there = DialogButton(dialog, ToolExerciseDialog.GoThereName);

            if (there is not null)
            {
                Assert.Equal(TypeScale.MinimumTarget, there.Height);
            }

            Press(DialogButton(dialog, ToolExerciseDialog.CloseName)!);

            return there is not null;
        }

        Assert.True(Offers(WithPage));
        Assert.True(Offers(row));
        Assert.False(Offers(pageless));
        Assert.False(Offers(absent));

        Press(GoOf(page, row));
        Press(DialogButton(Assert.Single(window.Modals().OfType<ToolExerciseDialog>()), ToolExerciseDialog.GoThereName)!);

        Assert.Empty(window.Modals());
        Assert.Equal([row], went);

        window.Close();
    }

    /// <summary>A settings row with no steps written shows its help text and the not-written line.</summary>
    [AvaloniaFact]
    public void ASettingsRowWithNoStepsShowsItsHelpAndTheUnwrittenLine()
    {
        Theme();

        var row = Row("diagnostics.row");
        var page = new CoveragePage(
            new CoverageLedger().Report([row]),
            exercise: item => new D47.App.Coverage.ToolExercise("A row", "Diagnostics", "What the row is for.", null));
        var window = page.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Press(GoOf(page, row));

        var dialog = Assert.Single(window.Modals().OfType<ToolExerciseDialog>());

        Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "What the row is for.");
        Assert.Equal(ToolExerciseDialog.Unwritten, dialog.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ExerciseUnwritten").Text);

        dialog.Close();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();
    }

    /// <summary>
    /// HIDE takes the line off the page, drops its section's count, and adds "(1 hidden)" to the
    /// summary, which otherwise stays the report's own.
    /// </summary>
    [AvaloniaFact]
    public void HideTakesTheLineOffThePageAndTheSummarySaysSo()
    {
        Theme();

        var report = Report();
        var summary = report.Summary;
        var page = new CoveragePage(report);
        var window = page.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(summary, Summary(page));

        Hide(window, page, Item("untouched"));

        Assert.DoesNotContain(page.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "PROBE - UNTOUCHED");
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => b.Name == "CoverageSection_never");
        Assert.Equal($"{summary} (1 hidden)", Summary(page));
        Assert.Equal(summary, report.Summary);
        Assert.Equal(1, report.Never);

        Hide(window, page, Item("worked"));

        Assert.Equal($"{summary} (2 hidden)", Summary(page));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => b.Name == "CoverageSection_exercised");

        window.Close();
    }

    /// <summary>
    /// Show hidden items puts a hidden line back in its section with UNHIDE on the row; UNHIDE makes it
    /// an ordinary line again.
    /// </summary>
    [AvaloniaFact]
    public void ShowHiddenItemsBringsAHiddenLineBackAndUnhideKeepsIt()
    {
        Theme();

        var page = new CoveragePage(Report());
        var window = page.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Hide(window, page, Item("untouched"));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => b.Name == "CoverageUnhide");

        var show = page.GetVisualDescendants().OfType<CheckBox>().Single(b => b.Name == "CoverageShowHidden");

        Assert.Equal(TypeScale.MinimumTarget, show.MinHeight);

        show.IsChecked = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("▾ NEVER TESTED (1)", Text(Heading(page, "never")));

        var unhide = Assert.Single(page.GetVisualDescendants().OfType<Button>(), b => b.Name == "CoverageUnhide");

        Assert.Equal(TypeScale.MinimumTarget, unhide.MinHeight);

        Press(unhide);

        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => b.Name == "CoverageUnhide");
        Assert.Equal(Report().Summary, Summary(page));

        show.IsChecked = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("▾ NEVER TESTED (1)", Text(Heading(page, "never")));

        window.Close();
    }

    /// <summary>A hidden line stays hidden on the next page built over the same store, and nothing else does.</summary>
    [AvaloniaFact]
    public void AHiddenLineStaysHiddenOnTheNextVisit()
    {
        Theme();
        var paths = new AppPaths(TempFolders.Create("d47-coverage-hidden"));

        var first = new CoveragePage(Report(), store: Store(paths));
        var window = first.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Hide(window, first, Item("untouched"));
        window.Close();

        var second = new CoveragePage(Report(), store: Store(paths));
        second.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(second.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "PROBE - UNTOUCHED");
        Assert.Contains(second.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "PROBE - WORKED");

        second.Close();
    }

    /// <summary>A line hidden at one fingerprint is shown again once its fingerprint differs, under Changed.</summary>
    [AvaloniaFact]
    public void AHiddenLineThatChangesComesBackByItself()
    {
        Theme();
        var paths = new AppPaths(TempFolders.Create("d47-coverage-hidden-changed"));
        var ledger = new CoverageLedger();
        var before = Item("moving", "before");

        ledger.Record(before, Monday, CoverageOutcome.Ok);

        var first = new CoveragePage(ledger.Report([before]), store: Store(paths));
        var window = first.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Hide(window, first, before);
        Assert.DoesNotContain(first.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "PROBE - MOVING");
        window.Close();

        var second = new CoveragePage(ledger.Report([Item("moving", "after")]), store: Store(paths));
        second.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains(second.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "PROBE - MOVING");
        Assert.Equal("▾ CHANGED SINCE YOU LAST TESTED THEM (1)", Text(Heading(second, "changed")));
        Assert.DoesNotContain("hidden", Summary(second), StringComparison.Ordinal);

        second.Close();
    }

    /// <summary>Go on a tool line with no page opens its exercise steps; with none written, its description and a hint.</summary>
    [AvaloniaFact]
    public void GoOnAPagelessToolOpensItsStepsOrItsDescription()
    {
        Theme();

        var written = new CoverageItem(CoverageKind.Tool, "has_steps", "probe", "Probe - has_steps", "aaaa");
        var unwritten = new CoverageItem(CoverageKind.Tool, "no_steps", "probe", "Probe - no_steps", "aaaa");
        var report = new CoverageLedger().Report([written, unwritten]);

        D47.App.Coverage.ToolExercise? Lookup(CoverageItem item) => item.Id == "has_steps"
            ? new D47.App.Coverage.ToolExercise(item.Id, "Probe", "Does a thing.", "Say hello and expect hello back.")
            : new D47.App.Coverage.ToolExercise(item.Id, "Probe", "Does another thing.", null);

        var page = new CoveragePage(report, go: _ => { }, exercise: Lookup);
        var window = page.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = page.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "CoverageGo").ToList();

        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, b => Assert.Equal(44, b.MinHeight));

        Press(buttons[0]);

        var first = Assert.Single(window.Modals().OfType<ToolExerciseDialog>());

        Assert.Equal("Say hello and expect hello back.", first.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ExerciseSteps").Text);
        Assert.DoesNotContain(first.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "ExerciseUnwritten");

        Press(first.GetVisualDescendants().OfType<Button>().Single(b => b.Name == ToolExerciseDialog.CloseName));

        Assert.DoesNotContain(window.Modals(), m => m is ToolExerciseDialog);

        Press(buttons[1]);

        var second = Assert.Single(window.Modals().OfType<ToolExerciseDialog>());

        Assert.Equal(ToolExerciseDialog.Unwritten, second.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ExerciseUnwritten").Text);
        Assert.Contains(second.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Does another thing.");

        second.Close();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();
    }

    /// <summary>GO THERE closes the dialog and the Features page and lands on the setting's row, marked.</summary>
    [AvaloniaFact]
    public void GoThereClosesTheDialogAndThePageAndLandsOnTheRow()
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

        Press(host.Panel.GetVisualDescendants().OfType<Button>().First(b => b.Name == "CoverageGo"));

        var dialog = Assert.Single(host.Window.Modals().OfType<ToolExerciseDialog>());

        Press(DialogButton(dialog, ToolExerciseDialog.GoThereName)!);
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(host.Window.Modals());
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

    private static readonly CoverageItem WithPage =
        new(CoverageKind.Tool, "search_stations", "galaxy", "Galaxy - search_stations", "aaaa");

    private static CoverageItem Row(string key) =>
        new(CoverageKind.Setting, key, "diagnostics", $"Probe - {key}", "aaaa");

    private static ViewStateStore Store(AppPaths paths) =>
        new(paths, new DiskFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static List<Avalonia.Controls.Button> Gos(Visual page) =>
        page.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Where(b => b.Name == "CoverageGo").ToList();

    /// <summary>The GO on the line that names the item.</summary>
    private static Avalonia.Controls.Button GoOf(Visual page, CoverageItem item) =>
        Gos(page).Single(go => go.GetVisualAncestors().OfType<DockPanel>().First().GetVisualDescendants()
            .OfType<TextBlock>().Any(t => t.Text == item.Name.ToUpperInvariant()));

    private static Avalonia.Controls.Button? DialogButton(ToolExerciseDialog dialog, string name) =>
        dialog.GetVisualDescendants().OfType<Avalonia.Controls.Button>().SingleOrDefault(b => b.Name == name);

    /// <summary>The Features page with a line hidden and Show hidden items on, and the exercise dialog of a row with a place.</summary>
    [AvaloniaFact]
    public void TheHiddenLineAndTheDialogRenderToCaptures()
    {
        Theme();

        var row = new CoverageItem(CoverageKind.Setting, "about.startMenu", "about", "About - Add to Start Menu", "aaaa");
        var page = new CoveragePage(
            new CoverageLedger().Report([row, Item("untouched"), WithPage]),
            go: _ => { },
            exercise: item => new D47.App.Coverage.ToolExercise(
                "Add to Start Menu",
                "About",
                "Puts a shortcut where Windows looks for one. Absent once there is one.",
                item.Id == row.Id ? "The row appears only when there is no Directive 47 shortcut in your Start Menu." : null));
        var window = page.Show();
        window.Width = 720;
        window.Height = 560;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Hide(window, page, Item("untouched"));
        page.GetVisualDescendants().OfType<CheckBox>().Single(b => b.Name == "CoverageShowHidden").IsChecked = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.SaveCapture("coverage-hidden-shown.png");

        Press(GoOf(page, row));
        window.CaptureRenderedFrame()!.SaveCapture("coverage-exercise-dialog.png");

        Assert.Single(window.Modals().OfType<ToolExerciseDialog>()).Close();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();
    }

    /// <summary>GO on the item's line, then HIDE in its dialog.</summary>
    private static void Hide(Window window, Visual page, CoverageItem item)
    {
        Press(GoOf(page, item));
        Press(DialogButton(Assert.Single(window.Modals().OfType<ToolExerciseDialog>()), ToolExerciseDialog.HideName)!);
        Assert.Empty(window.Modals());
    }

    private static string? Summary(Visual page) =>
        page.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "CoverageSummary").Text;

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
