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
/// The engineer filter, on the page, with a roll the Commander has not earned the grade for
/// (<a href="https://github.com/dseelinger/d47/issues/205">#205</a>).
/// <para>
/// Reported as an 8A power plant planned for <i>Armoured G5</i> and <i>Thermal Spread</i>, of
/// which only Thermal Spread appeared. The blueprint was in the out-of-rank band, which the page
/// did not render and no control readmitted — so the one line the Commander had come to that
/// workshop for was the one line they could not see, and the effect beside it read as a stray
/// errand.
/// </para>
/// <para>
/// <b>Tested on the surface, because the report was about the surface.</b> Both halves were on
/// the document the whole time and every Core probe agreed they were; what was wrong was what got
/// drawn.
/// </para>
/// </summary>
public class TheEngineerFilterShowsWorkWaitingOnRankTests
{
    private const int LeiCheung = 300120;

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-09-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-09-01T08:00:01Z","event":"Location","StarSystem":"Laksak","Docked":true,"StationName":"Trader's Rest"}""",

                     // Unlocked, and at grade 1 — which is the whole setup: Lei Cheung takes Heavy
                     // Duty on a shield booster to 3, and grade 3 cannot be rolled at rank 1.
                     $$"""{"timestamp":"2026-09-01T08:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":1}]}""",

                     """{"timestamp":"2026-09-01T08:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistService Checklists(CommanderGameState state)
    {
        var paths = new AppPaths(TempFolders.Create("d47-engineer-filter-rank-tests"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        // A blueprint and its effect on one slot, the way a promoted build puts them there. The
        // plan names no engineer, which is the ordinary case and the reason the page has to say
        // the grade itself: the evaluator reaches a rank through the engineer a plan names.
        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                ChecklistScope.Ship(51),
                "anaconda",
                [new BuildRequest("TinyHardpoint5", "Heavy Duty", 3, Experimental: "Force Block")]),
            ["TinyHardpoint5"]);

        return checklists;
    }

    private static IReadOnlyList<string> Drawn(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];
    }

    [AvaloniaFact]
    public void TheBlueprintIsOnThePageBesideItsEffect()
    {
        var checklists = Checklists(State());

        checklists.Choose(ChecklistService.HereKey);

        var drawn = Drawn(checklists);

        // The half that was reported missing.
        Assert.Contains(drawn, text => text.Contains("Heavy Duty", StringComparison.Ordinal));

        // And the half that was there on its own, which is what made the page read as wrong
        // rather than as empty.
        Assert.Contains(drawn, text => text.Contains("Force Block", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AndTheLineSaysWhichGradeAndWhatTheCommandersIs()
    {
        var checklists = Checklists(State());

        checklists.Choose(ChecklistService.HereKey);

        var drawn = Drawn(checklists);

        // Visible and explained rather than visible and misleading. Nothing else on the line can
        // say this: the plan named no engineer, so the verdict treats it as ordinary open work.
        Assert.Contains(
            drawn,
            text => text.Contains(
                "Lei Cheung rolls this at grade 3, and you are grade 1 with them",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// The rest of the list is unchanged: the filter still narrows to this engineer's work, and a
    /// line that is nobody's to roll is still absent.
    /// </summary>
    [AvaloniaFact]
    public void ALineNoEngineerHereCanRollIsStillFilteredOut()
    {
        var checklists = Checklists(State());

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.Choose(ChecklistService.HereKey);

        var drawn = Drawn(checklists);

        Assert.DoesNotContain(drawn, text => text.Contains("buy limpets", StringComparison.Ordinal));
    }
}
