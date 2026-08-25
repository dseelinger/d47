using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <b>"It should have the same tabs as the VR mini panel, including Checklist"</b> (asked for
/// 2026-08-24).
/// <para>
/// Stated as an executable claim rather than as two lists that have to be kept in step by hand:
/// the strip is furnished from the same services the headset is, and the only difference is
/// Settings — withheld because the strip is click-through, so a page of controls on it is a page
/// nobody could touch, and because it is the one page mini cannot fit.
/// </para>
/// </summary>
public class TheOverlayCarriesTheHeadsetsTabsTests
{
    /// <summary>
    /// The claim itself. If a later phase gives the headset a tab, this fails until the strip has
    /// it too or Settings' company is deliberately widened — which is the point of comparing the
    /// two surfaces rather than asserting a list.
    /// </summary>
    [AvaloniaFact]
    public void TheStripCarriesEveryTabTheHeadsetDoesExceptSettings()
    {
        var (headset, overlay) = Both();

        var missing = Tabs(headset.Nav)
            .Where(tab => tab != PanelTab.Settings)
            .Where(tab => !overlay.Nav.Has(tab))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"The headset carries these and the strip does not: {string.Join(", ", missing)}");

        // And it has no tab the headset has not got, so "the same" reads both ways.
        var extra = Tabs(overlay.Nav).Where(tab => !headset.Nav.Has(tab)).ToList();

        Assert.True(extra.Count == 0, $"The strip carries these and the headset does not: {string.Join(", ", extra)}");

        overlay.Close();
    }

    /// <summary>
    /// The tab the instruction named, reachable and drawn — not merely registered.
    /// </summary>
    [AvaloniaFact]
    public void MiniShowsTheChecklist()
    {
        var (_, overlay) = Both();

        Assert.True(overlay.Nav.Has(PanelTab.Checklist));

        overlay.Nav.Select(PanelTab.Checklist);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Checklist, overlay.Nav.Tab);

        overlay.Close();
    }

    /// <summary>
    /// <b>Settings is the exclusion, and it is the only one.</b> Two reasons that agree: the strip
    /// is click-through, and mini cannot fit a page whose nav collapses below 900 and whose body
    /// wants 700. So it is refused even where a caller went out of their way — the rule is
    /// <see cref="PanelView"/>'s rather than the host's.
    /// </summary>
    [AvaloniaFact]
    public void SettingsIsRefusedInMiniEvenWhenTheSurfaceHasIt()
    {
        var (headset, _) = Both();

        // The headset does furnish Settings, and in full it can be selected.
        Assert.True(headset.Nav.Has(PanelTab.Settings));

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Mini };

        panel.EnableSettings(() => new Avalonia.Controls.TextBlock { Text = "settings" });
        panel.EnableChecklist(Checklists());
        Dispatcher.UIThread.RunJobs();

        // Asserted on where the surface ends up rather than on what the navigator returned. The
        // navigator's answer is about whether the tab exists — it does — and mini's refusal is the
        // view putting it back, which is the behaviour a Commander sees.
        panel.Nav.Select(PanelTab.Settings);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Transcript, panel.Tab);

        // While the checklist, at the same size on the same surface, is taken.
        panel.Nav.Select(PanelTab.Checklist);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Checklist, panel.Tab);
    }

    /// <summary>
    /// A picture of the checklist at the strip's size, because <em>selectable</em> and
    /// <em>readable</em> are two claims and only one of them can be asserted.
    /// <para>
    /// This is the half of the Phase 51 ruling that did not go away: a page with a real minimum
    /// width drawn into 512 is a page nobody can use. The checklist has no such number, and this
    /// is how that is checked rather than assumed.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheChecklistAtTheStripsSizeRendersToACapture()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var checklists = Checklists();

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "sell the low temperature diamonds at Jameson");
        checklists.AddNote(ChecklistScope.Universal, "grade 5 dirty drives with Felicity Farseer");

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Mini };

        panel.EnableChecklist(checklists);
        panel.EnableModeToggle(_ => { });
        panel.Nav.Select(PanelTab.Checklist);

        var window = new Avalonia.Controls.Window
        {
            Content = panel,
            Width = PanelResolution.Mini.Width,
            Height = PanelResolution.Mini.Height,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Checklist, panel.Tab);

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "overlay-checklist.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    private static IEnumerable<PanelTab> Tabs(PanelNavigator nav) =>
        Enum.GetValues<PanelTab>().Where(nav.Has);

    /// <summary>
    /// One headset surface and one strip, wired from the same services the composition root wires
    /// them from — which is what makes the comparison mean anything.
    /// </summary>
    private static (VrPanelSurface Headset, OverlayPanel Overlay) Both()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var (settings, viewState, paths) = TestSurface.Create();

        var checklists = Checklists();
        var alarms = new AlarmStore(
            Path.Combine(paths.Data, "alarms.json"), NullLogger<AlarmStore>.Instance);
        var timekeeper = new Timekeeper(alarms);
        var adventures = AdventureFixture.Surface(paths);

        var headset = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            settingsPage: () => new Avalonia.Controls.TextBlock { Text = "settings" },
            checklists: checklists,
            timekeeper: timekeeper,
            alarmStore: alarms,
            adventures: adventures);

        var overlay = new OverlayPanel(
            new PanelViewModel(),
            settings,
            viewState,
            NullLogger<OverlayPanel>.Instance,
            avatars: null,
            adventures: adventures,
            tabs: new OverlayTabs
            {
                Checklists = checklists,
                Timekeeper = timekeeper,
                Alarms = alarms,
            });

        Dispatcher.UIThread.RunJobs();

        return (headset, overlay);
    }

    private static ChecklistService Checklists()
    {
        var paths = new AppPaths(TempFolders.Create("d47-overlay-tabs"));
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }
}
