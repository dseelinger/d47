using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// <see cref="EngineerPlanService.Promote"/> revises only its own <see
/// cref="ChecklistSource.EngineeringPlan"/> lines for the engineers on the chain — a prerequisite item
/// the Commander added carries <see cref="ChecklistSource.EngineerPrerequisite"/> instead, so a route
/// re-promoted afterwards does not drop it (#257).
/// </summary>
public class PromotingARouteDoesNotRemovePrerequisitesTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void APrerequisiteAddedByHandSurvivesAPromotedRoute()
    {
        using var install = new TempInstall();
        var root = install.Root;
        var store = new GameStateStore();

        store.Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        store.Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0]}"""));

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => store.Active);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => store.Active);

        var felicity = EngineerDirectory.ByName("Felicity Farseer")!;

        checklists.AddPrerequisites(felicity);

        var added = checklists.Document.Items
            .Where(item => item.Source == ChecklistSource.EngineerPrerequisite)
            .ToList();

        Assert.Equal(2, added.Count);

        unlocks.Promote("Felicity Farseer");
        checklists.Accept();

        var stillThere = checklists.Document.Items
            .Where(item => item.Source == ChecklistSource.EngineerPrerequisite)
            .ToList();

        Assert.Equal(2, stillThere.Count);
        Assert.All(stillThere, item => Assert.True(item.IsLive));
    }
}
