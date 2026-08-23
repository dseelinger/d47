using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Engineers;
using D47.Core.Help;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Help drawn in the headset (asked for 2026-08-22).
/// <para>
/// The mark in the corner used to open a browser on the desktop, so the headset was handed none
/// and shows no button at all (change-requests.md 24). This is the other half of that: help that
/// is a page of the panel rather than a page of the web, so the surface with no browser is the
/// surface it was built for.
/// </para>
/// <para>
/// Everything here is driven through <see cref="VrPanelSurface"/> and pressed with a ray, because
/// a test that opens a <c>Window</c> proves the desktop and says nothing about the quad.
/// </para>
/// </summary>
public class HelpInTheHeadsetTests
{
    /// <summary>What a ray-sized target has to clear, in surface pixels (list.md Phase 39).</summary>
    private const double TouchFloor = 30;

    /// <summary>
    /// The floor for text meant to be read, in surface pixels. 1024 across a 1.1 m quad at 1.1 m
    /// is 19 pixels per degree, and ~20 arcminutes of cap height is the floor — so 13 px is the
    /// smallest a band may end up drawn at, whatever it says in the markup.
    /// </summary>
    private const double ReadingFloor = 13;

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A Commander in Sol, flying 30 ly a jump, unlocked with Liz Ryder alone.</summary>
    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-22T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-22T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-08-22T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[]}""",
                     """{"timestamp":"2026-08-22T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5}]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    /// <summary>
    /// The big panel with the Engineers tab furnished. Big explicitly: mini is the shipped
    /// default and has no tab strip and no header, so it is not the surface this is about.
    /// </summary>
    /// <param name="showingHelp">
    /// Opens help before the first frame. The surface keeps one PNG per session and keeps the
    /// first, so a capture of the help page has to be the first thing drawn — pressing the mark
    /// needs a layout to aim at, and by then the frame that would be kept is already the tab's.
    /// </param>
    /// <param name="dump">
    /// Where this surface's one PNG goes. Its own folder for the capture test: every test here
    /// writes <c>vr-PanelFull.png</c>, and the shared run directory means whichever finished last
    /// is the file on disk — which had the capture test reading somebody else's frame.
    /// </param>
    private static (VrPanelSurface Panel, PanelView View, string Dump) Headset(
        bool showingHelp = false,
        string? dump = null)
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var root = TempFolders.Create("d47-help-in-vr");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        builds.Save([
            new ShipBuild("F1", "ship-1", "python", 12, "Bad Idea",
                [new SlotPlan("FrameShiftDrive", "Increased FSD Range", 3)]),
        ]);

        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

        dump ??= TestSurface.CaptureDirectory;

        var panel = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            dumpTo: dump,
            ships: new ShipPlanService(builds, checklists, () => state),
            gameState: () => state,
            onFoot: new OnFootPlanService(kit, checklists, () => state),
            unlocks: new EngineerPlanService(builds, kit, checklists, () => state));

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        // Spoken, because that is the route a Commander wearing one actually has — there is no
        // tab to press until the surface has drawn one.
        PanelPhrases.Apply("show me the engineers", panel.Nav);

        if (showingHelp)
        {
            Assert.True(view.OpenHelp(), "help opened");
        }

        Serve(panel);

        return (panel, view, dump);
    }

    /// <summary>
    /// One frame, into a buffer nobody reads. The dispatcher is pumped first because the page
    /// rebuilds by posting, and twice through the raster because the first pass gives the tree an
    /// extent and the second lays out against it.
    /// </summary>
    private static void Serve(VrPanelSurface panel)
    {
        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Where a control is on the quad's face, in the 0..1 a ray answers in.</summary>
    private static (float U, float V) At(Control control, PanelView view, VrPanelSurface panel)
    {
        var corner = control.TranslatePoint(new Point(0, 0), view);
        Assert.NotNull(corner);

        var centre = corner.Value + new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var (width, height) = panel.Size;

        return ((float)(centre.X / width), (float)(centre.Y / height));
    }

    private static Button Mark(PanelView view) =>
        view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "HelpButton");

    /// <summary>
    /// <b>The headset has a help mark again.</b> It lost the one it had because the only thing
    /// behind it was a browser, and this is the tab whose help the panel can draw itself.
    /// </summary>
    [AvaloniaFact]
    public void TheEngineersTabCarriesAHelpMarkInTheHeadset()
    {
        var (panel, view, _) = Headset();

        Assert.Equal(PanelTab.Engineers, view.Tab);

        var mark = Mark(view);

        Assert.True(mark.IsVisible, "the headset's Engineers tab shows the help mark");
        Assert.True(mark.Bounds.Width >= TouchFloor, $"the mark is {mark.Bounds.Width} px across");
        Assert.True(mark.Bounds.Height >= TouchFloor, $"the mark is {mark.Bounds.Height} px down");

        panel.Dispose();
    }

    /// <summary>
    /// Pressed through a ray, it takes the panel — which is what makes it help <em>over</em> the
    /// page rather than a tab beside it. Every route that would navigate away is refused while it
    /// is up, and that refusal is the navigator's, not this page's.
    /// </summary>
    [AvaloniaFact]
    public void PressingItTakesThePanelWithoutLeavingTheTab()
    {
        var (panel, view, _) = Headset();

        var (u, v) = At(Mark(view), view, panel);

        Assert.True(panel.Press(u, v), "the mark takes the press");

        Serve(panel);

        Assert.True(view.Nav.Modal, "help holds the panel until it is dismissed");
        Assert.Equal("Help", view.Nav.Trail[^1].Word);

        // Still the Engineers tab underneath. Help is a level of the page, not a destination.
        Assert.Equal(PanelTab.Engineers, view.Tab);

        // And nothing can navigate away from it while it is up.
        Assert.False(view.Nav.Select(PanelTab.Transcript));
        Assert.Equal(PanelTab.Engineers, view.Tab);

        panel.Dispose();
    }

    /// <summary>
    /// The band is drawn, all four figures of it, and every one has real bounds. A figure that
    /// measured to nothing would still pass a test that only asked whether the control existed.
    /// </summary>
    [AvaloniaFact]
    public void TheBandDrawsItsFourFiguresOnTheQuad()
    {
        var (panel, view, _) = Headset();

        var (u, v) = At(Mark(view), view, panel);
        panel.Press(u, v);
        Serve(panel);

        var figures = view.GetVisualDescendants().OfType<HelpFigureView>().ToList();

        Assert.Equal(4, figures.Count);

        foreach (var figure in figures)
        {
            Assert.True(figure.Bounds.Width > 400, $"a figure is only {figure.Bounds.Width} px across");
            Assert.True(figure.Bounds.Height > 80, $"a figure is only {figure.Bounds.Height} px down");
        }

        // The lede and every heading reached the surface too.
        var said = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains(said, line => line.StartsWith("Who can improve your ship", StringComparison.Ordinal));
        Assert.Contains("Two lists.", said);
        Assert.Contains("The Route picks the one unlock that helps most.", said);

        panel.Dispose();
    }

    /// <summary>
    /// <b>The reason the figures are drawn rather than written.</b> A band is authored in a
    /// viewBox and scaled to whatever width the panel gives it, so the number in the markup is
    /// only the size on the quad if that scale is near one. This measures the scale actually
    /// applied and holds the smallest text in the band above the reading floor.
    /// <para>
    /// Without it a later change to the panel's chrome — a wider margin, a nav column — could
    /// quietly shrink every diagram in the headset below legibility, and nothing would fail.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void NothingInTheBandIsDrawnBelowTheReadingFloor()
    {
        var (panel, view, _) = Headset();

        var (u, v) = At(Mark(view), view, panel);
        panel.Press(u, v);
        Serve(panel);

        var article = HelpLibrary.For("engineers")!;
        var figures = view.GetVisualDescendants().OfType<HelpFigureView>().ToList();

        // Counted before the zip below, which pairs two sequences and would sail through with
        // nothing to say if the page had drawn none of them.
        Assert.Equal(4, figures.Count);

        var drawn = article.Sections
            .Select(section => section.Figure)
            .OfType<HelpFigure>()
            .Zip(figures, (figure, control) => (Figure: figure, Scale: control.Bounds.Width / figure.Width));

        foreach (var (figure, scale) in drawn)
        {
            Assert.True(scale > 0.5, $"a figure is drawn at {scale:0.00} of its authored size");

            var smallest = figure.Shapes.OfType<HelpLabel>().Min(label => label.Size) * scale;

            Assert.True(
                smallest >= ReadingFloor,
                $"the smallest text lands at {smallest:0.0} px, under the {ReadingFloor} px floor");
        }

        panel.Dispose();
    }

    /// <summary>
    /// Back puts the page back. One gesture, and the navigator already makes it the same one as
    /// the breadcrumb and the spoken word — there is nothing here for help to have got wrong.
    /// </summary>
    [AvaloniaFact]
    public void BackDismissesItAndTheTabIsWhereItWas()
    {
        var (panel, view, _) = Headset();

        var (u, v) = At(Mark(view), view, panel);
        panel.Press(u, v);
        Serve(panel);

        Assert.True(view.Nav.Modal);
        Assert.True(view.GoBack(), "there was something to go back from");

        Serve(panel);

        Assert.False(view.Nav.Modal, "the panel is handed back");
        Assert.Empty(view.GetVisualDescendants().OfType<HelpFigureView>());
        Assert.Equal(PanelTab.Engineers, view.Tab);

        // And the tab is usable again.
        Assert.True(view.Nav.Select(PanelTab.Transcript));

        panel.Dispose();
    }

    /// <summary>
    /// The frame the compositor would hand SteamVR, kept as a PNG.
    /// <para>
    /// It proves the band rasterises on the offscreen surface at all, which is the failure this
    /// path is prone to — a Win32 window that is never shown is not the headless platform, and
    /// every wrong theory about an invisible overlay has come from assuming those two agree.
    /// <b>It cannot judge contrast</b>: a headless render has none of the application's theme
    /// resources, so the palette in the file is the fallback rather than the Commander's
    /// (list.md Phase 39). Size and layout are real; colour is not.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheHelpFrameRasterises()
    {
        var folder = Path.Combine(TestSurface.CaptureDirectory, "help");
        Directory.CreateDirectory(folder);

        var (panel, view, dump) = Headset(showingHelp: true, dump: folder);

        Assert.True(view.Nav.Modal, "the frame that was kept is the help page");

        var written = Directory.GetFiles(dump, "vr-*.png");

        Assert.NotEmpty(written);
        Assert.All(written, file => Assert.True(new FileInfo(file).Length > 0, $"{file} is empty"));

        panel.Dispose();
    }

    /// <summary>
    /// <b>Help follows the level, and a level inherits.</b> Drilling into one engineer is still
    /// the Engineers subject, so the mark stays and opens the same band — which is what declaring
    /// help on the root buys, and what a per-tab table could not have expressed for a tab whose
    /// levels are about different things.
    /// </summary>
    [AvaloniaFact]
    public void DrillingIntoOneEngineerInheritsTheTabsHelp()
    {
        var (panel, view, _) = Headset();

        Assert.Equal("engineers", view.Nav.Help);

        // The crumb the directory pushes when a name is pressed.
        Assert.True(view.Nav.Drill(
            new NavCrumb(EngineersPages.WhoPrefix + "300080", "Liz Ryder")
            {
                Level = EngineersPages.WhoPrefix,
            }));

        Serve(panel);

        Assert.Equal("engineers", view.Nav.Help);
        Assert.True(Mark(view).IsVisible, "the mark survives the drill");

        var (u, v) = At(Mark(view), view, panel);
        Assert.True(panel.Press(u, v), "and still opens the band");

        Serve(panel);

        Assert.Equal("help:engineers", view.Nav.Trail[^1].Key);

        panel.Dispose();
    }

    /// <summary>
    /// The band's links, on the surface with no browser behind it — and the split that matters.
    /// <para>
    /// Checklists has a band of its own, so it is something this machine is already carrying and
    /// is drawn as a control: pressing it drills. Engineering and Ships have none yet, so they are
    /// addresses, and an address is written out rather than drawn as a control that would do
    /// nothing here.
    /// </para>
    /// <para>
    /// This test asserted all three were addresses until the Checklists band was written, and
    /// went red the moment it was. That is the intended behaviour arriving, not a regression: the
    /// day somebody writes the Engineering band, the line below it goes the same way.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheBandsLinksAreWrittenOutRatherThanDrawnAsDeadControls()
    {
        var (panel, view, _) = Headset(showingHelp: true);

        var shown = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains("Where to go next".ToUpperInvariant(), shown);

        // No band yet, so an address a Commander can read and type later.
        Assert.Contains(D47.App.DocsSite.Capability("engineering"), shown);
        Assert.Contains(D47.App.DocsSite.Capability("ships"), shown);

        // The long form of this very page, which the panel does not draw.
        Assert.Contains(D47.App.DocsSite.Capability("engineers"), shown);

        // Checklists is here, so it is a control rather than an address.
        Assert.DoesNotContain(D47.App.DocsSite.Capability("checklists"), shown);

        Assert.Contains(
            view.GetVisualDescendants().OfType<Button>(),
            button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == "Checklists"));

        panel.Dispose();
    }
}
