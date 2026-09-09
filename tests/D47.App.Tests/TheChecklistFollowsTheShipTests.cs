using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The engineer filter answers from where the ship is, so the page has to redraw when it moves — with
/// nothing touched between the two readings (#93).
/// </summary>
public class TheChecklistFollowsTheShipTests
{
    private const int LeiCheung = 300120;

    /// <summary>A system with no engineer in it, which is where every one of these starts.</summary>
    private const string Nowhere = "Shinrarta Dezhra";

    /// <summary>Lei Cheung's system.</summary>
    private const string Laksak = "Laksak";

    private static void Feed(GameStateStore store, params string[] lines)
    {
        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }
    }

    /// <summary>
    /// A Commander parked where no engineer is, in a ship carrying the two boosters the plan below is
    /// written against, at grade 1 with Lei Cheung.
    /// </summary>
    private static GameStateStore Parked()
    {
        var store = new GameStateStore();

        Feed(
            store,
            """{"timestamp":"2026-09-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            $$"""{"timestamp":"2026-09-01T08:00:01Z","event":"Location","StarSystem":"{{Nowhere}}","Docked":false}""",
            $$"""{"timestamp":"2026-09-01T08:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":1}]}""",
            """{"timestamp":"2026-09-01T08:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"TinyHardpoint4","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""");

        return store;
    }

    private static ChecklistService Checklists(GameStateStore store)
    {
        var paths = new AppPaths(TempFolders.Create("d47-checklist-follows-the-ship-tests"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => store.Active);

        checklists.Follow(store);

        // One roll Lei Cheung offers above the Commander's grade with them, and one they can only take part
        // of the way — the two bands the page draws differently.
        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                ChecklistScope.Ship(51),
                "anaconda",
                [
                    new BuildRequest("TinyHardpoint5", "Heavy Duty", 3),
                    new BuildRequest("TinyHardpoint4", "Heavy Duty", 5),
                ]),
            ["TinyHardpoint5", "TinyHardpoint4"]);

        return checklists;
    }

    /// <summary>The panel, on the checklist tab, left on screen so a jump can reach it.</summary>
    private static PanelView Showing(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static IReadOnlyList<string> Drawn(PanelView panel)
    {
        Dispatcher.UIThread.RunJobs();

        return [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];
    }

    /// <summary>The jump itself: one event, and nothing else touched.</summary>
    private static void JumpToLaksak(GameStateStore store) =>
        Feed(
            store,
            $$"""{"timestamp":"2026-09-01T09:00:00Z","event":"FSDJump","StarSystem":"{{Laksak}}","FuelLevel":28.3}""");

    [AvaloniaFact]
    public void TheEmptyMessageIsReplacedByTheEngineersWork()
    {
        var store = Parked();
        var checklists = Checklists(store);

        checklists.Choose(ChecklistService.HereKey);

        var panel = Showing(checklists);

        // Parked where nobody works: the filter has nothing to show, and names the scope that emptied it.
        Assert.Contains(
            Drawn(panel),
            text => text.Contains("Nothing on your list is in here.", StringComparison.Ordinal));

        JumpToLaksak(store);

        var arrived = Drawn(panel);

        Assert.DoesNotContain(
            arrived,
            text => text.Contains("Nothing on your list is in here.", StringComparison.Ordinal));

        Assert.Contains(arrived, text => text.Contains("Heavy Duty", StringComparison.Ordinal));
    }

    /// <summary>
    /// The control offered on work an engineer here can only start, which is as stale as the list was.
    /// </summary>
    [AvaloniaFact]
    public void ThePartialGradesCheckboxAppearsOnTheJump()
    {
        var store = Parked();
        var checklists = Checklists(store);

        checklists.Choose(ChecklistService.HereKey);

        var panel = Showing(checklists);

        static bool Offered(PanelView panel) => panel.GetVisualDescendants()
            .OfType<CheckBox>()
            .Any(box => box.Content as string == "Include Partial Grades");

        Assert.False(Offered(panel));

        JumpToLaksak(store);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Offered(panel));
    }

    /// <summary>The rank line names the engineer in the system the ship is in, not the one it left.</summary>
    [AvaloniaFact]
    public void TheRankLineNamesTheEngineerHere()
    {
        var store = Parked();
        var checklists = Checklists(store);

        checklists.Choose(ChecklistService.HereKey);

        var panel = Showing(checklists);

        Assert.DoesNotContain(Drawn(panel), text => text.Contains("Lei Cheung", StringComparison.Ordinal));

        JumpToLaksak(store);

        Assert.Contains(
            Drawn(panel),
            text => text.Contains(
                "Lei Cheung rolls this at grade 3, and you are grade 1 with them",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// The signal is the location changing, not the journal moving: the events after the jump that leave
    /// the ship where it is redraw nothing.
    /// </summary>
    [AvaloniaFact]
    public void OneRedrawPerJumpRatherThanOnePerEvent()
    {
        var store = Parked();
        var checklists = Checklists(store);

        var redraws = 0;

        checklists.HereChanged += () => redraws++;

        JumpToLaksak(store);

        Feed(
            store,
            $$"""{"timestamp":"2026-09-01T09:01:00Z","event":"SupercruiseExit","StarSystem":"{{Laksak}}","Body":"Laksak A 1"}""",
            $$"""{"timestamp":"2026-09-01T09:02:00Z","event":"Docked","StarSystem":"{{Laksak}}","StationName":"Trader's Rest","MarketID":128}""",
            """{"timestamp":"2026-09-01T09:03:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[]}""",
            """{"timestamp":"2026-09-01T09:04:00Z","event":"Undocked","StationName":"Trader's Rest"}""");

        Assert.Equal(1, redraws);
    }
}
