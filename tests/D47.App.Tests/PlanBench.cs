using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>The checklists, build stores and plan services over in-memory files under one folder.</summary>
internal sealed record PlanBench(
    ChecklistService Checklists,
    ShipBuildStore Builds,
    OnFootBuildStore Kit,
    ShipPlanService Ships,
    OnFootPlanService OnFoot,
    EngineerPlanService Unlocks)
{
    public static PlanBench Over(string folder, Func<CommanderGameState?> state)
    {
        var checklists = TestSurface.Checklists(folder, state);
        var builds = new ShipBuildStore(Path.Combine(folder, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(folder, "on-foot.json"), new MemoryFileSystem(), NullLogger<OnFootBuildStore>.Instance);

        return new PlanBench(
            checklists,
            builds,
            kit,
            new ShipPlanService(builds, checklists, state),
            new OnFootPlanService(kit, checklists, state),
            new EngineerPlanService(builds, kit, checklists, state));
    }
}
