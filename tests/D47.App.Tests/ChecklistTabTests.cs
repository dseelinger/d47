using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The one surface (Phase 17), now a tab of the panel rather than a window over it
/// (Phase 25, "The checklist leaves its window").
/// <para>
/// <b>The headline is not tidiness.</b> A <c>Window</c> cannot appear in the headset, so until
/// this moved a Commander in VR could not see their checklist at all — which is why these tests
/// build the panel rather than a dialog, and why the headset's copy gets the tab too.
/// </para>
/// <para>
/// The distinction the whole phase turns on is unchanged: some ticks are computed and some are
/// opinions, and a Commander must be able to tell which at a glance.
/// </para>
/// </summary>
public class ChecklistTabTests
{
    private static ChecklistService Checklists(string root) => Checklists(root, () => null);

    private static ChecklistService Checklists(
        string root, Func<D47.Core.Journal.CommanderGameState?> state)
    {
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            state);
    }

    /// <summary>
    /// The panel on the Checklist tab, shown, which is what makes its controls measurable.
    /// </summary>
    private static (Window Window, PanelView Panel) Open(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    /// <summary>
    /// The movers on screen, by the name a screen reader reads. The glyphs are drawn rather than
    /// typed, so there is no string on the button to look for.
    /// </summary>
    private static IReadOnlyList<Button> Movers(PanelView panel, string name) =>
        [.. panel.GetVisualDescendants().OfType<Button>()
            .Where(button => AutomationProperties.GetName(button) == name)];

    private static ChecklistItem Derived(bool done = false)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(12),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Grade 5 dirty drives",
            Intent = intent,
            State = done ? ChecklistState.Done : ChecklistState.Open,
        };
    }

    /// <summary>
    /// The tab is on both surfaces, unlike Settings — and that asymmetry is the item's whole
    /// point rather than an oversight.
    /// </summary>
    [AvaloniaFact]
    public void TheTabIsThereOnceTheHostGivesIt()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));
        var panel = new PanelView { DataContext = new PanelViewModel() };

        Assert.False(panel.FindControl<Control>("ChecklistTab")!.IsVisible);

        panel.EnableChecklist(checklists);

        Assert.True(panel.FindControl<Control>("ChecklistTab")!.IsVisible);
    }

    /// <summary>
    /// And the headset's own instantiation has it again, and still has no Loadout
    /// (Phase 39, "The Checklist tab is furnished on the VR panel again").
    /// <para>
    /// <b>This assertion has now been written three ways, and the middle one was not a mistake.</b>
    /// Phase 25 put the checklist in the headset because a <c>Window</c> cannot appear there at
    /// all, and Phase 26 put the fleet beside it; both were withdrawn on the Commander's
    /// instruction on 2026-08-19, which was them overruling two built phases rather than a
    /// discovery that either tab had never worked. Phase 39 asks for one of them back.
    /// </para>
    /// <para>
    /// So what is pinned here is the <em>asymmetry</em> rather than either half of it: the two
    /// calls are one line each, and a checklist that came back without anybody deciding Loadout
    /// should is exactly the drift this test exists to catch — in the direction it is now facing
    /// as much as in the one it used to.
    /// </para>
    /// <para>
    /// Handed the services both tabs would need, so the assertion is about the surface furnishing
    /// one and declining the other rather than about a test that never supplied them.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetCopyHasTheChecklistAndStillNotTheFleet()
    {
        var (settings, _, _) = TestSurface.Create();
        var root = TempFolders.Create("d47-checklist-tests");
        var checklists = Checklists(root);

        // The fleet service too, so the second assertion is the surface declining to furnish a
        // tab it could have rather than a test that never handed it the parts.
        var ships = new D47.Core.Ships.ShipPlanService(
            new D47.Core.Ships.ShipBuildStore(
                Path.Combine(root, "ships.json"),
                NullLogger<D47.Core.Ships.ShipBuildStore>.Instance),
            checklists,
            () => null);

        using var surface = new Headset.VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            checklists: checklists,
            ships: ships,
            gameState: () => null);

        var view = (PanelView)surface.GetType()
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(surface)!;

        Assert.True(view.FindControl<Control>("ChecklistTab")!.IsVisible);
        Assert.False(view.FindControl<Control>("LoadoutTab")!.IsVisible);
    }

    /// <summary>
    /// The window keeps both, which is the half of "one widget tree renders to both surfaces"
    /// that never moved: the difference between the surfaces is which host calls which
    /// <c>Enable</c>, and nothing in the view knows a headset from a window.
    /// </summary>
    [AvaloniaFact]
    public void TheWindowKeepsBoth()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));
        var (window, panel) = Open(checklists);

        Assert.True(panel.FindControl<Control>("ChecklistTab")!.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ADerivedItemHasNoCheckboxAtAll()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.List.Save(
        [
            checklists.Document with { Items = [.. checklists.Document.Items, Derived()] },
        ]);

        var (window, panel) = Open(checklists);

        var ticks = panel.GetVisualDescendants().OfType<CheckBox>().ToList();

        // One authored item and one derived one, and exactly one checkbox. Not a disabled tick:
        // a greyed-out control still asserts that ticking is the mechanism here, and it is not.
        Assert.Single(ticks);
        Assert.Equal("buy limpets", ticks[0].Content);

        window.Close();
    }

    [AvaloniaFact]
    public void FinishedItemsSitBelowTheLineWithTheirCountShowing()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.Complete(checklists.Document.Items[0].Id);
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        var (window, panel) = Open(checklists);

        var text = panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        // Kept, counted, and out of the way. Forty finished items must not bury the six still open.
        Assert.Contains("Done (1)", text);

        window.Close();
    }

    /// <summary>
    /// One list: scope rides each line, and the page is not carved into one list per ship. Since
    /// Phase 42 the reading groups the lines by project — with no rank set and no game state,
    /// projects stand in the order they first appear in the file — but a project is an ordering,
    /// never a heading: the page stays one list of lines.
    /// </summary>
    [AvaloniaFact]
    public void ScopeIsALabelOnTheLineRatherThanAHeadingOverAGroup()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Ship(12), "fit a fuel scoop");
        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");

        var (window, panel) = Open(checklists);

        var lines = panel.GetVisualDescendants().OfType<CheckBox>()
            .Select(tick => tick.Content as string ?? string.Empty)
            .ToList();

        // Grouped by project in first-appearance order (Phase 42), no headings between
        // them, and nothing lost.
        Assert.Equal(["buy limpets", "sell the cargo", "fit a fuel scoop"], lines);

        window.Close();
    }

    /// <summary>
    /// Selecting a line grows the movers, and moving reorders the file. Not a drag: a drag is the
    /// worst gesture available to a ray at a metre, and it has no spoken form at all.
    /// </summary>
    [AvaloniaFact]
    public void TheSelectedLineGrowsMoversAndMovingReordersTheList()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        // Adding selects (reported 2026-08-21), and this test is about a press selecting. Put back
        // to nothing so the press below is the act being tested rather than a second one undoing
        // what the fixture did; TheAddedLineIsTheSelectedOne covers the other half.
        checklists.Select(null);

        var (window, panel) = Open(checklists);

        // Nothing is selected, so no line is carrying movers. Several hundred rows each with a
        // permanent row of them is several hundred controls a ray can hit by accident.
        //
        // Found by the name rather than by a glyph, because the glyphs are drawn rather than
        // typed: an end mover is the step mover with a bar on it, which no pair of unrelated
        // codepoints gives and which no font can turn into tofu.
        Assert.Empty(Movers(panel, "Move up"));

        // The innermost border that holds the line, which is the card. Descendants come out
        // outermost first, so Last is the card rather than the pane or the scroller around it.
        var second = panel.GetVisualDescendants().OfType<Border>()
            .Last(border => border.GetVisualDescendants().OfType<CheckBox>()
                .Any(tick => tick.Content as string == "fit a fuel scoop"));

        second.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            second,
            new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            second,
            default,
            0,
            new Avalonia.Input.PointerPointProperties(
                Avalonia.Input.RawInputModifiers.LeftMouseButton,
                Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
            Avalonia.Input.KeyModifiers.None));

        Dispatcher.UIThread.RunJobs();

        Assert.Single(Movers(panel, "Move up")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["fit a fuel scoop", "buy limpets"],
            checklists.Document.Items.Select(item => item.Text));

        window.Close();
    }

    /// <summary>
    /// Suggestions are a page rather than an interruption: they wait in one place, reached by
    /// drilling, and the list itself is not interleaved with them.
    /// </summary>
    [AvaloniaFact]
    public void SuggestionsWaitOnTheirOwnPage()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        var (window, panel) = Open(checklists);

        var open = panel.GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.StartsWith("Suggestions", StringComparison.Ordinal) == true);

        Assert.Equal("Suggestions (1)", open.Content);

        // Not on the list itself. A proposal is not something the Commander is working on.
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Button>(),
            button => button.Content as string == "Accept");

        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // A level of the stack, so the breadcrumb says where the Commander is and offers the way
        // back — which is the thing a headset has no title bar for.
        Assert.Equal(["Checklist", "Suggestions"], panel.Nav.Trail.Select(crumb => crumb.Word));

        var buttons = panel.GetVisualDescendants().OfType<Button>()
            .Select(button => button.Content as string ?? string.Empty)
            .ToList();

        Assert.Contains("Accept", buttons);
        Assert.Contains("Decline", buttons);

        // Nothing has moved yet — the page is where the Commander decides, and the AI cannot.
        Assert.Empty(checklists.Document.Items);

        window.Close();
    }

    [AvaloniaFact]
    public void AcceptingFromThePageCommitsIt()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        var (window, panel) = Open(checklists);

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        Dispatcher.UIThread.RunJobs();

        var accept = panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Accept");

        accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("buy limpets", checklists.Document.Items.Single().Text);
        Assert.Empty(checklists.Proposals.Pending);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageFollowsAChangeMadeFromSomewhereElse()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        var (window, panel) = Open(checklists);

        // A voice command, or a text editor. The page is a view of the file rather than a second
        // copy of it, so it has to follow whoever wrote.
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            tick => tick.Content as string == "buy limpets");

        window.Close();
    }

    /// <summary>
    /// Four movers on the selected line, not two (reported 2026-08-21). The list runs to several
    /// hundred lines, so a line at 274 reaches the top in one press or in 273 — which makes "to
    /// the top" a different errand from "up" rather than a faster one.
    /// </summary>
    [AvaloniaFact]
    public void TheSelectedLineCarriesBothStepsAndBothEnds()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        // The last line added is the selected one, which is the reported behaviour and what makes
        // this test need no press of its own.
        var (window, panel) = Open(checklists);

        foreach (var name in new[] { "Move to the top", "Move up", "Move down", "Move to the bottom" })
        {
            Assert.Single(Movers(panel, name));
        }

        Assert.Single(Movers(panel, "Move to the top")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["fit a fuel scoop", "buy limpets", "sell the cargo"],
            checklists.Document.Items.Select(item => item.Text));

        window.Close();
    }

    /// <summary>
    /// The page at the size the headset renders it, for a human to look at.
    /// </summary>
    [AvaloniaFact]
    public void TheChecklistTabRendersToACapture()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Ship(12), "fit a fuel scoop");
        checklists.ProposeAdd(ChecklistScope.Universal, ["grind for grade 5 dirty drives"]);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1024, Height = 640 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "checklist-tab.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// Every mover glyph is actually painted. <b>A blank button is the tofu failure by another
    /// road</b> — a shape does not inherit a foreground the way a text block does, so the first
    /// version of this drew four empty grey rectangles and every assertion about them passed.
    /// </summary>
    [AvaloniaFact]
    public void NoMoverGlyphIsBlank()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        var (window, panel) = Open(checklists);

        var glyphs = panel.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => AutomationProperties.GetName(button)?.StartsWith("Move", StringComparison.Ordinal) == true)
            .Select(button => button.Content)
            .OfType<Avalonia.Controls.Shapes.Path>()
            .ToList();

        Assert.Equal(4, glyphs.Count);

        foreach (var glyph in glyphs)
        {
            Assert.NotNull(glyph.Fill);
            Assert.NotNull(glyph.Data);
            Assert.True(glyph.Data!.Bounds.Width > 0, "the glyph has no geometry to draw");
        }

        window.Close();
    }

    /// <summary>
    /// The four movers, drawn, for a human to look at. They are the one part of this that no
    /// assertion can check: an end glyph has to read as the step glyph with a bar on it.
    /// </summary>
    [AvaloniaFact]
    public void TheMoversRenderToACapture()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "refill manufactured materials");

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1024, Height = 400 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "checklist-movers.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// <b>The engineer filter, driven through the page rather than through the join under it</b>
    /// (reported 2026-08-23).
    /// <para>
    /// The reason this test exists at all: the same evening produced three separate measurements
    /// of <c>EngineersHere.For</c> in isolation, every one of them right, while the Commander's
    /// screen kept disagreeing. Core was being verified and the page was not, so nothing ever
    /// checked the thing being looked at. This drives the real <see cref="PanelView"/>, presses
    /// the real filter, and reads back the lines that are actually drawn.
    /// </para>
    /// <para>
    /// The case is the defect itself: Heavy Duty on a Shield Booster is Lei Cheung's to grade 3,
    /// and grades 4 and 5 are Mel Brandon's and Didi Vatermann's. A grade 5 line is not his work
    /// and must not be drawn under his name.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheEngineerFilterDrawsOnlyWorkThatEngineerCanActuallyDo()
    {
        var state = InLaksakWithAShieldBooster();
        var checklists = Checklists(TempFolders.Create("d47-engineer-filter"), () => state);

        checklists.List.Save(
        [
            checklists.Document with
            {
                Items =
                [
                    Roll("TinyHardpoint1", grade: 3),
                    Roll("TinyHardpoint2", grade: 5),
                ],
            },
        ]);

        var (window, panel) = Open(checklists);

        var drawn = Lines(panel);
        Assert.Contains(drawn, line => line.Contains("Grade 3", StringComparison.Ordinal));
        Assert.Contains(drawn, line => line.Contains("Grade 5", StringComparison.Ordinal));

        // Take the filter the way the Commander does: press the chooser, press the option.
        // Driven through the buttons rather than through a test-only seam, because a seam would
        // let the two diverge and the divergence is exactly what went unnoticed here.
        var word = checklists.FilterAxes()
            .Single(filter => filter.Key == ChecklistService.HereKey).Word;

        Press(panel, content => content.StartsWith("Showing", StringComparison.Ordinal));
        Press(panel, content => content == word);

        drawn = Lines(panel);

        Assert.Contains(drawn, line => line.Contains("Grade 3", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, line => line.Contains("Grade 5", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>Presses the first button whose text content matches, then lets the UI settle.</summary>
    private static void Press(PanelView panel, Func<string, bool> matching)
    {
        // Matched on the text a Commander can see, not on Content: a chooser's rows are Buttons
        // wrapping a StackPanel, so only the plain toolbar buttons carry a string.
        var button = panel.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b =>
                (b.Content is string text && matching(text))
                || b.GetVisualDescendants().OfType<TextBlock>()
                    .Any(block => block.Text is { Length: > 0 } shown && matching(shown)));

        Assert.NotNull(button);
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every line of text the checklist page is drawing right now.</summary>
    private static IReadOnlyList<string> Lines(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    /// <summary>
    /// One fact about an engineer is said once (<a
    /// href="https://github.com/dseelinger/d47/issues/33">#33</a>), reported 2026-08-24:
    /// <em>"This is being repeated once per module [...] That line should only appear for a new
    /// Engineer and only once."</em>
    /// <para>
    /// Two modules blocked behind the same rank. Both lines still say what is true of
    /// <b>them</b> — grade 3 cannot be rolled at rank 1 — because that is a fact about each
    /// module and a line has to stand on its own. The explanation about the engineer belongs to
    /// the first line that needs it and to no other.
    /// </para>
    /// <para>
    /// Watched to fail with the de-duplication taken out, where the sentence is drawn twice.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheRankExplanationIsDrawnOnceHoweverManyModulesWaitOnIt()
    {
        var state = InLaksakWithAShieldBooster(rank: 1);
        var checklists = Checklists(TempFolders.Create("d47-one-explanation"), () => state);

        checklists.List.Save(
        [
            checklists.Document with
            {
                Items = [Gated("TinyHardpoint1"), Gated("TinyHardpoint2")],
            },
        ]);

        var (window, panel) = Open(checklists);

        var drawn = Lines(panel);

        // Each line keeps its own verdict.
        Assert.Equal(
            2,
            drawn.Count(line => line.Contains("cannot be crafted at rank 1", StringComparison.Ordinal)));

        // The engineer is explained once.
        Assert.Equal(1, drawn.Count(line => line.Contains("compounds", StringComparison.Ordinal)));

        window.Close();
    }

    /// <summary>
    /// A gated roll: grade 3 wanted, the engineer named so the rank can be looked up at all, and
    /// the Commander at rank 1 — which is no route rather than a slow one.
    /// </summary>
    private static ChecklistItem Gated(string slot)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = "Heavy Duty",
            Grade = 3,
            Engineer = "Lei Cheung",
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(51),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = $"Grade 3 Heavy Duty on {slot}",
            Intent = intent,
        };
    }

    private static ChecklistItem Roll(string slot, int grade)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = "Heavy Duty",
            Grade = grade,
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(51),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = $"Grade {grade} Heavy Duty on {slot}",
            Intent = intent,
        };
    }

    /// <summary>In Lei Cheung's system, in a ship with a shield booster in both utility slots.</summary>
    /// <param name="rank">
    /// The Commander's standing with Lei Cheung. Five by default, which is every caller that does
    /// not care; a lower one is how a rank gate is made to fire.
    /// </param>
    private static D47.Core.Journal.CommanderGameState InLaksakWithAShieldBooster(int rank = 5)
    {
        var store = new D47.Core.Journal.GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-23T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-23T09:00:01Z","event":"Location","StarSystem":"Laksak","Docked":true,"StationName":"Trader's Rest"}""",
                     """{"timestamp":"2026-08-23T09:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":300120,"Progress":"Unlocked","Rank":""" + rank.ToString(CultureInfo.InvariantCulture) + """}]}""",
                     """{"timestamp":"2026-08-23T09:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"TinyHardpoint2","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(D47.Core.Journal.JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>
    /// <b>Include Partial Grades</b> (change-requests.md 35). The grade check above shut a real
    /// door: Lei Cheung genuinely can take a Heavy Duty booster from nothing to grade 3, at a
    /// workshop the Commander is standing in. Checked, the grade 5 line comes back — and says how
    /// far he takes it, because the two readings of this page are a sentence apart.
    /// </summary>
    [AvaloniaFact]
    public void PartialGradesAreOfferedBesideTheEngineerFilterAndSayHowFarTheyGo()
    {
        var state = InLaksakWithAShieldBooster();
        var checklists = Checklists(TempFolders.Create("d47-partial-grades"), () => state);

        checklists.List.Save(
        [
            checklists.Document with
            {
                Items = [Roll("TinyHardpoint1", grade: 3), Roll("TinyHardpoint2", grade: 5)],
            },
        ]);

        var (window, panel) = Open(checklists);

        var word = checklists.FilterAxes()
            .Single(filter => filter.Key == ChecklistService.HereKey).Word;

        Press(panel, content => content.StartsWith("Showing", StringComparison.Ordinal));
        Press(panel, content => content == word);

        // Unchecked stays exactly what shipped.
        Assert.DoesNotContain(Lines(panel), line => line.Contains("Grade 5", StringComparison.Ordinal));

        var box = panel.GetVisualDescendants().OfType<CheckBox>()
            .Single(check => check.Content?.ToString() == "Include Partial Grades");

        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var drawn = Lines(panel);

        Assert.Contains(drawn, line => line.Contains("Grade 5", StringComparison.Ordinal));
        Assert.Contains(drawn, line => line.Contains("Lei Cheung takes this to 3 of 5", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>
    /// The control is absent rather than hidden anywhere it means nothing — the phrase says
    /// nothing about a list filtered by ship, and the bar is already crowded.
    /// </summary>
    [AvaloniaFact]
    public void TheCheckboxIsNotThereWhenTheEngineerFilterIsNot()
    {
        var state = InLaksakWithAShieldBooster();
        var checklists = Checklists(TempFolders.Create("d47-partial-grades"), () => state);

        checklists.List.Save(
        [
            checklists.Document with { Items = [Roll("TinyHardpoint2", grade: 5)] },
        ]);

        var (window, panel) = Open(checklists);

        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            check => check.Content?.ToString() == "Include Partial Grades");

        window.Close();
    }
}
