using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The left list says which engineer the right pane is drawing (#110), and the detail pane's prose
/// can be selected and copied (#122).
/// <para>
/// Both are asserted through the drawn page rather than through the helper that builds it: the
/// question in each case is what a Commander is looking at, and a probe of the builder would pass
/// for a page that never reached the screen.
/// </para>
/// </summary>
public class TheOpenEngineerIsOutlinedTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

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

    private static (Window Window, PanelView Panel) Open()
    {
        var root = TempFolders.Create("d47-outlined-row-tests");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(
            Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

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

        return (window, panel);
    }

    private static IReadOnlyList<Button> Rows(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>()];

    private static IReadOnlyList<Button> Outlined(PanelView panel) =>
        [.. Rows(panel).Where(button => button.Classes.Contains("showing"))];

    private static string Label(Button button) =>
        string.Join(
            " ",
            button.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty));

    /// <summary>
    /// Nothing is drilled into, so nothing is outlined. Asserted first because an outline that is
    /// always on would pass every test below it.
    /// </summary>
    [AvaloniaFact]
    public void NoEngineerOpenMeansNoOutline()
    {
        var (window, panel) = Open();

        Assert.Empty(Outlined(panel));

        window.Close();
    }

    /// <summary>
    /// The engineer the right pane is drawing, and exactly that one. Drilled through the navigator
    /// rather than by pressing, which is also the case the report cares about second: the pane can
    /// be opened by voice and the list does not move.
    /// </summary>
    [AvaloniaFact]
    public void TheOpenEngineerIsTheOutlinedRow()
    {
        var (window, panel) = Open();

        var farseer = D47.Core.Knowledge.EngineerDirectory.ByName("Felicity Farseer")!;
        panel.Nav.Drill(EngineersPages.Crumb(farseer));
        Dispatcher.UIThread.RunJobs();

        var outlined = Outlined(panel);

        Assert.Single(outlined);
        Assert.Contains("Felicity Farseer", Label(outlined[0]), StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The outline follows the pane back out again. A mark that is set on the way in and never
    /// cleared is the failure this shape exists to avoid, and it is invisible in a test that only
    /// ever drills.
    /// </summary>
    [AvaloniaFact]
    public void GoingBackClearsTheOutline()
    {
        var (window, panel) = Open();

        panel.Nav.Drill(EngineersPages.Crumb(D47.Core.Knowledge.EngineerDirectory.ByName("Felicity Farseer")!));
        Dispatcher.UIThread.RunJobs();
        Assert.Single(Outlined(panel));

        panel.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Outlined(panel));

        window.Close();
    }

    /// <summary>
    /// <b>The drill still works.</b> Making the detail prose selectable puts a control that handles
    /// drag inside the pane, and the index rows are what a Commander presses — so the press that
    /// opens an engineer is asserted rather than assumed (#122).
    /// </summary>
    [AvaloniaFact]
    public void PressingARowStillOpensThatEngineer()
    {
        var (window, panel) = Open();

        var row = Rows(panel).First(button =>
            Label(button).Contains("Felicity Farseer", StringComparison.Ordinal));

        row.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Felicity Farseer", panel.Nav.Trail[^1].Word);

        window.Close();
    }

    /// <summary>
    /// The detail pane's prose is selectable, which is the whole of "I want to be able to copy text
    /// from the Engineer Details pane" — drag-selection and Ctrl+C are the control's own.
    /// </summary>
    [AvaloniaFact]
    public void TheDetailPaneIsSelectable()
    {
        var (window, panel) = Open();

        panel.Nav.Drill(EngineersPages.Crumb(D47.Core.Knowledge.EngineerDirectory.ByName("Felicity Farseer")!));
        Dispatcher.UIThread.RunJobs();

        var selectable = panel.GetVisualDescendants().OfType<SelectableTextBlock>().ToList();

        Assert.NotEmpty(selectable);
        Assert.Contains(
            selectable,
            block => (block.Text ?? string.Empty).Contains("Deciat", StringComparison.OrdinalIgnoreCase));

        window.Close();
    }

    /// <summary>
    /// <b>And the pressable rows are still plain.</b> The cut is the detail helpers and not
    /// <c>Row</c>'s label: a selectable label inside a button can swallow the drag that was meant
    /// to be a press. If somebody later makes <c>Row</c> selectable too, this is what says so.
    /// </summary>
    [AvaloniaFact]
    public void TheIndexRowsAreNotSelectable()
    {
        var (window, panel) = Open();

        var inRows = Rows(panel)
            .SelectMany(button => button.GetVisualDescendants().OfType<SelectableTextBlock>())
            .ToList();

        Assert.Empty(inRows);

        window.Close();
    }
}
