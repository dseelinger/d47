using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Loadout;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Suits page's own question, put on foot the same way the fleet's is (#299): a plan Promote put
/// on the checklist is asked about here rather than only on the Checklist tab.
/// </summary>
public class ASuitsPageNoticeAnswersAPendingChecklistProposalTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static (OnFootMode Mode, ChecklistService Checklists) SuitWithPendingProposal()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-onfoot-notice-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                 })
        {
            store.Apply(Event(line));
        }

        var live = store.Active!;

        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(paths.Data, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            () => live);

        var build = kit.BuildFor(D47.Core.Knowledge.OnFootKind.Suit, 7, "Maverick Suit");

        kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));
        kit.Promote(build.Id);

        return (new OnFootMode(kit, checklists, () => live), checklists);
    }

    [Fact]
    public void APromotedOnFootPlanLeavesANoticeOnTheSuitsPage()
    {
        var (mode, _) = SuitWithPendingProposal();

        Assert.NotNull(mode.Notice());
    }

    [Fact]
    public void AcceptingTheNoticeClearsIt()
    {
        var (mode, _) = SuitWithPendingProposal();

        var notice = mode.Notice();
        Assert.NotNull(notice);

        notice!.Yes();

        Assert.Null(mode.Notice());
    }

    [Fact]
    public void DecliningTheNoticeClearsIt()
    {
        var (mode, _) = SuitWithPendingProposal();

        var notice = mode.Notice();
        Assert.NotNull(notice);

        notice!.No();

        Assert.Null(mode.Notice());
    }
}
