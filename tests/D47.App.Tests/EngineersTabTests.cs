using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class EngineersTabTests
{
    private sealed record Surface(
        Window Window,
        PanelView Panel,
        ShipPlanService Ships,
        ChecklistService Checklists);

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
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5}]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static Surface Open(bool planned = true, D47.Core.Capabilities.Builtin.IClipboard? clipboard = null)
    {
        var root = TempFolders.Create("d47-engineers-tab-tests");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(
            Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        if (planned)
        {
            builds.Save([
                new ShipBuild("F1", "ship-1", "python", 12, "Bad Idea",
                    [new SlotPlan("FrameShiftDrive", "Increased FSD Range", 3)]),
            ]);
        }

        var kit = new OnFootBuildStore(
            Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);

        var viewState = new ViewStateStore(
            new D47.Core.AppPaths(root), NullLogger<ViewStateStore>.Instance);
        var memory = new EngineerDirectoryMemory(viewState);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (clipboard is not null)
        {
            panel.EnableCopy(clipboard);
        }

        panel.EnableEngineers(unlocks, ships, () => state, onFoot, memory);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Engineers;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, checklists);
    }

    private static IReadOnlyList<string> Text(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Press(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text is { } said && said.Contains(label, StringComparison.Ordinal)));

    /// <summary>
    /// Two roots of one tab, on the same reading as Loadout's three: the Directory and the Route are
    /// two answers to which engineer, not two destinations.
    /// </summary>
    [AvaloniaFact]
    public void TheTabHasTwoModes()
    {
        var surface = Open();

        Assert.Equal(
            ["Directory", "Route"],
            surface.Panel.Nav.Roots(PanelTab.Engineers).Select(root => root.Word));

        surface.Window.Close();
    }

    /// <summary>
    /// The directory leads with what can be acted on today, and its summary counts the whole
    /// directory in the game's three states.
    /// </summary>
    [AvaloniaFact]
    public void TheDirectoryLeadsWithWhatIsReachable()
    {
        var surface = Open();
        var shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.Contains("1 of 38 unlocked", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("in progress", StringComparison.Ordinal)
                                       && line.Contains("not started", StringComparison.Ordinal));

        Assert.Contains("Ready for Unlock", shown);
        Assert.Contains("Unlocked", shown);
        Assert.Contains("Needs a Referral", shown);

        // The heading that can be acted on comes before the one that cannot.
        Assert.True(
            shown.ToList().IndexOf("Ready for Unlock")
            < shown.ToList().IndexOf("Needs a Referral"));

        // And the say-line, on this level as on every other.
        Assert.Contains(shown, line => line.StartsWith("Say:", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Distances are on the page, which is the reason for the coordinates shipping in the table:
    /// this is arithmetic against where the Commander is, not a lookup.
    /// </summary>
    [AvaloniaFact]
    public void EveryRowSaysHowFarAndHowManyJumps()
    {
        var surface = Open();

        Assert.Contains(
            Text(surface.Panel),
            line => line.Contains("ly, about", StringComparison.Ordinal)
                    && line.Contains("jump", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Drilling a row opens the one engineer behind it, with the way in spelled out stop by stop rather
    /// than summarised — a summary is what a Commander cannot act on.
    /// </summary>
    [AvaloniaFact]
    public void DrillingOpensOneEngineer()
    {
        var surface = Open();

        Press(surface.Panel, "Felicity Farseer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["Directory", "Felicity Farseer"],
            surface.Panel.Nav.Trail.Select(crumb => crumb.Word));

        var shown = Text(surface.Panel);

        Assert.Contains("Farseer Inc in Deciat", shown);
        Assert.Contains("The way in", shown);
 // One speciality per line, not one running clause.
        Assert.Contains(shown, line => line.Contains("•  Frame Shift Drive (G5)", StringComparison.Ordinal));
        Assert.DoesNotContain(shown, line => line.Contains("Frame Shift Drive to 5", StringComparison.Ordinal));

        // Frontier's own sentence, printed rather than summarised away.
        Assert.Contains(shown, line => line.Contains("exploration rank Scout", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>The engineer's system carries a copy glyph, wired to the surface's clipboard (#157).</summary>
    [AvaloniaFact]
    public void TheEngineersSystemCarriesACopyGlyph()
    {
        var clipboard = new D47.Core.Capabilities.Builtin.RecordingClipboard();
        var surface = Open(clipboard: clipboard);

        Press(surface.Panel, "Felicity Farseer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var glyph = surface.Panel.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Copy Deciat");

        glyph.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Deciat"], clipboard.Written);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheRouteShowsItsWork()
    {
        var surface = Open();

        Assert.True(surface.Panel.Nav.SelectRoot(EngineersPages.RouteRoot));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.Contains("Measured from Sol", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("planned thing covered", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("hand over:", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("first:", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("covers:", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Promotion offers the whole chain, and offers it — the checklist gains nothing until the
    /// Commander accepts, which is the same path every other plan takes.
    /// </summary>
    [AvaloniaFact]
    public void PromotingOffersTheChain()
    {
        var surface = Open();

        surface.Panel.Nav.SelectRoot(EngineersPages.RouteRoot);
        Dispatcher.UIThread.RunJobs();

        Press(surface.Panel, "Put this route on my checklist")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        var proposal = Assert.Single(surface.Checklists.Proposals.Pending);

        Assert.Equal(ProposalKind.Plan, proposal.Kind);
        Assert.All(proposal.Items, item =>
            Assert.Equal(ChecklistIntentKind.EngineerAccess, item.Intent!.Kind));

        // Nothing on the list itself until it is accepted.
        Assert.Empty(surface.Checklists.Document.Items);

        surface.Window.Close();
    }

    /// <summary>
    /// With nothing planned the page still answers, and says so rather than showing an empty ranking —
    /// who is nearest is the right answer to a question with no plans behind it.
    /// </summary>
    [AvaloniaFact]
    public void WithNothingPlannedItStillAnswers()
    {
        var surface = Open(planned: false);
        var shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.Contains("1 of 38 unlocked", StringComparison.Ordinal));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ARankedNameOpensThatEngineer()
    {
        var surface = Open();

        Assert.True(surface.Panel.Nav.SelectRoot(EngineersPages.RouteRoot));
        Dispatcher.UIThread.RunJobs();

        var first = Text(surface.Panel)
            .First(line => EngineerDirectory.All.Any(engineer => engineer.Name == line));

        Press(surface.Panel, first).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(first, surface.Panel.Nav.Trail[^1].Word);
        Assert.StartsWith(EngineersPages.WhoPrefix, surface.Panel.Nav.Trail[^1].Key, StringComparison.Ordinal);

        // And it is the engineer's own page rather than a level that merely carries their name.
        Assert.Contains(Text(surface.Panel), line => line.Contains("Where you stand", StringComparison.Ordinal));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AStopOnTheWayInOpensThatEngineerToo()
    {
        var surface = Open();

        // Somebody behind a referral, so their chain has a stop on it that is not themselves.
        var behind = EngineerDirectory.All.First(engineer => engineer.Name == "Broo Tarquin");

        surface.Panel.Nav.Drill(EngineersPages.Crumb(behind));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Text(surface.Panel), line => line.Contains("The way in", StringComparison.Ordinal));

        // Inside the engineer's own page, not the directory beside it: a wide panel keeps the list on screen,
        // and its rows have opened an engineer since Phase 28 — so a test that searched the whole panel would
        // pass without this change ever having been made.
        var page = surface.Panel.GetVisualDescendants().OfType<EngineerPage>().Single();

        var pressable = page.GetVisualDescendants().OfType<Button>()
            .Select(button => (Button: button, Label: button.GetVisualDescendants()
                .OfType<TextBlock>().FirstOrDefault()?.Text))
            .Where(found => found.Label is { } label
                            && label != behind.Name
                            && EngineerDirectory.All.Any(engineer => engineer.Name == label))
            .ToList();

        var stop = Assert.Single(pressable);

        stop.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(stop.Label, surface.Panel.Nav.Trail[^1].Word);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void OnlyOneEngineerIsOpenAtATime()
    {
        var surface = Open();

        // Pressed rather than navigated, because the row in the directory pane beside the open engineer is
        // the control the report is about.
        Press(surface.Panel, "Liz Ryder").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Directory", "Liz Ryder"], surface.Panel.Nav.Trail.Select(c => c.Word));

        Press(surface.Panel, "Felicity Farseer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Directory", "Felicity Farseer"], surface.Panel.Nav.Trail.Select(c => c.Word));
        Assert.Single(surface.Panel.GetVisualDescendants().OfType<EngineerPage>());

        // And the same by way of the directory, which is what the second report tried.
        surface.Panel.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        Press(surface.Panel, "Liz Ryder").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Directory", "Liz Ryder"], surface.Panel.Nav.Trail.Select(c => c.Word));
        Assert.Single(surface.Panel.GetVisualDescendants().OfType<EngineerPage>());

        surface.Window.Close();
    }

    private static CheckBox Check(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Content?.ToString() == label);

    /// <summary>
    /// The eight engineers out at Colonia can be taken off the list, and back, without touching who is
    /// on foot (#132).
    /// </summary>
    [AvaloniaFact]
    public void TheColoniaEngineersCanBeHidden()
    {
        var surface = Open();

        // StartsWith, because a row carries the count of plans waiting on them after the name.
        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Mel Brandon", StringComparison.Ordinal));
        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Felicity Farseer", StringComparison.Ordinal));

        Check(surface.Panel, "Hide the Colonia eight").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Text(surface.Panel), line => line.StartsWith("Mel Brandon", StringComparison.Ordinal));
        Assert.DoesNotContain(Text(surface.Panel), line => line.StartsWith("Petra Olmanova", StringComparison.Ordinal));
        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Felicity Farseer", StringComparison.Ordinal));

        // And back, because a filter that cannot be undone is a setting nobody meant to change.
        Check(surface.Panel, "Hide the Colonia eight").IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Mel Brandon", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>The on-foot engineers can be taken off the list independently of the Colonia tick (#132).</summary>
    [AvaloniaFact]
    public void TheOnFootEngineersCanBeHidden()
    {
        var surface = Open();

        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Jude Navarro", StringComparison.Ordinal));
        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Domino Green", StringComparison.Ordinal));

        Check(surface.Panel, "Hide on-foot engineers").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Text(surface.Panel), line => line.StartsWith("Jude Navarro", StringComparison.Ordinal));
        Assert.DoesNotContain(Text(surface.Panel), line => line.StartsWith("Domino Green", StringComparison.Ordinal));
        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Felicity Farseer", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>Both ticked together leaves 21 rows, Baltanos counted off once rather than twice (#132).</summary>
    [AvaloniaFact]
    public void BothTicksTogetherLeaveTwentyOne()
    {
        var surface = Open();

        Check(surface.Panel, "Hide the Colonia eight").IsChecked = true;
        Check(surface.Panel, "Hide on-foot engineers").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        // Baltanos is on both lists — gone once, not counted twice anywhere, and the summary above the
        // ticks still counts the whole directory.
        Assert.DoesNotContain(Text(surface.Panel), line => line.StartsWith("Baltanos", StringComparison.Ordinal));
        Assert.Contains(Text(surface.Panel), line => line.Contains("1 of 38 unlocked", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// The Route page has no checkboxes of its own — it carries nobody a Directory tick has hidden
    /// (#132).
    /// </summary>
    [AvaloniaFact]
    public void ARouteHiddenOnTheDirectoryDoesNotRank()
    {
        var surface = Open();

        Check(surface.Panel, "Hide on-foot engineers").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        surface.Panel.Nav.SelectRoot(EngineersPages.RouteRoot);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Text(surface.Panel), line => line == "Jude Navarro");

        surface.Window.Close();
    }

    /// <summary>Both ticks are remembered, so reopening the tab restores them as they were left (#132).</summary>
    [AvaloniaFact]
    public void BothTicksAreRememberedAcrossAReopen()
    {
        var root = TempFolders.Create("d47-engineers-tick-memory-tests");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);
        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);
        var paths = new D47.Core.AppPaths(root);

        var firstPanel = new PanelView { DataContext = new PanelViewModel() };

        firstPanel.EnableEngineers(
            unlocks, ships, () => state, onFoot,
            new EngineerDirectoryMemory(new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance)));

        var firstWindow = new Window { Content = firstPanel, Width = 900, Height = 700 };
        firstWindow.Show();
        firstPanel.Tab = PanelTab.Engineers;
        Dispatcher.UIThread.RunJobs();

        Check(firstPanel, "Hide the Colonia eight").IsChecked = true;
        Check(firstPanel, "Hide on-foot engineers").IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        firstWindow.Close();

        var secondPanel = new PanelView { DataContext = new PanelViewModel() };

        secondPanel.EnableEngineers(
            unlocks, ships, () => state, onFoot,
            new EngineerDirectoryMemory(new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance)));

        var secondWindow = new Window { Content = secondPanel, Width = 900, Height = 700 };
        secondWindow.Show();
        secondPanel.Tab = PanelTab.Engineers;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Check(secondPanel, "Hide the Colonia eight").IsChecked);
        Assert.True(Check(secondPanel, "Hide on-foot engineers").IsChecked);

        secondWindow.Close();
    }

 /// <summary>
    /// An unlock criterion carries a drawn box, named for a screen reader rather than a character, and
    /// the three states are told apart by that name (#126).
    /// </summary>
    [AvaloniaFact]
    public void AMetCriterionIsMarkedAndAnUnreadableOneIsNot()
    {
        var surface = Open();

        surface.Panel.Nav.Drill(
            EngineersPages.Crumb(EngineerDirectory.All.First(e => e.Name == "Liz Ryder")));

        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains("Unlock Prerequisites", shown);
        Assert.Contains(Boxes(surface.Panel), says => says == "met");

        // Marco Qwent, whose referral through Elvira Martuuk is not met and whose invitation is not readable.
        surface.Panel.Nav.Drill(
            EngineersPages.Crumb(EngineerDirectory.All.First(e => e.Name == "Marco Qwent")));

        Dispatcher.UIThread.RunJobs();

        shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.StartsWith("Grade 3 with Elvira Martuuk", StringComparison.Ordinal));

        var boxes = Boxes(surface.Panel);

        Assert.Contains(boxes, says => says == "not met");
        Assert.Contains(boxes, says => says == "not yet known");

        surface.Window.Close();
    }

    /// <summary>
    /// Marco Qwent gates Chloe Sedesi, and the plans want her — so his row and his own page both say
    /// the referral opens her, until his referral is met (#138).
    /// </summary>
    [AvaloniaFact]
    public void AGatingEngineerNamesTheDependantThePlansWant()
    {
        var root = TempFolders.Create("d47-engineers-gate-tests");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(
            Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        builds.Save([
            new ShipBuild("F1", "ship-1", "python", 12, "Bad Idea",
                [new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, Engineer: "Chloe Sedesi")]),
        ]);

        var kit = new OnFootBuildStore(
            Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableEngineers(unlocks, ships, () => state, onFoot);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Engineers;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            Text(panel),
            line => line.Contains("Marco Qwent", StringComparison.Ordinal)
                    && line.Contains("grade 3 opens Chloe Sedesi", StringComparison.Ordinal));

        panel.Nav.Drill(EngineersPages.Crumb(EngineerDirectory.All.First(e => e.Name == "Marco Qwent")));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Text(panel), line => line.Contains("grade 3 opens Chloe Sedesi", StringComparison.Ordinal));

        window.Close();
    }

    private static IReadOnlyList<string?> Boxes(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<Avalonia.Controls.Shapes.Path>()
            .Select(Avalonia.Automation.AutomationProperties.GetName)];
}
