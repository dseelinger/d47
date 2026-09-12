namespace D47.App.Windowing;

/// <summary>
/// What the flat overlay needs in order to carry the pages the headset's mini panel carries (asked for
/// 2026-08-24: "it should have the same tabs as the VR mini panel, including Checklist").
/// </summary>
public sealed record OverlayTabs
{
    /// <summary>What the Commander is working on (Phase 25).</summary>
    public D47.Core.Checklists.ChecklistService? Checklists { get; init; }

    /// <summary>
    /// The long arcs, which ride the checklist tab rather than sitting beside it (Phase 34) — so they
    /// reach this surface on exactly the same terms the list does.
    /// </summary>
    public D47.Core.Goals.GoalBook? Goals { get; init; }

    public Action? BackfillGoals { get; init; }

    /// <summary>Who to go and unlock next (Phase 28).</summary>
    public D47.Core.Engineers.EngineerPlanService? Unlocks { get; init; }

    public D47.Core.Ships.ShipPlanService? Ships { get; init; }

    public Func<D47.Core.Journal.CommanderGameState?>? GameState { get; init; }

    public D47.Core.Loadout.OnFootPlanService? OnFoot { get; init; }

    /// <summary>The Engineers tab's remembered checkbox filters, one memory shared with the other two surfaces (#132).</summary>
    public D47.App.Panel.EngineerDirectoryMemory? EngineersMemory { get; init; }

    /// <summary>
    /// The clocks, timers and alarms (Phase 24) — the page whose whole argument is a Commander who
    /// cannot glance at a wall clock, which is as true over a full-screen game as it is inside a
    /// headset.
    /// </summary>
    public D47.Core.Utilities.Timekeeper? Timekeeper { get; init; }

    public D47.Core.Utilities.AlarmStore? Alarms { get; init; }
}
