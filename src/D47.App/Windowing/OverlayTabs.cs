namespace D47.App.Windowing;

/// <summary>
/// What the flat overlay needs in order to carry the pages the headset's mini panel carries
/// (asked for 2026-08-24: <em>"it should have the same tabs as the VR mini panel, including
/// Checklist"</em>).
/// <para>
/// <b>A record rather than nine more parameters.</b> <c>VrHost.Start</c> took the ninth route and
/// is now a call with eighteen arguments, most of them null in most tests; this is the same list
/// with names on it, which also means adding a tab is a property rather than a signature every
/// caller has to be found and updated for.
/// </para>
/// <para>
/// <b>Settings is deliberately absent and cannot be added here.</b> Two reasons that agree: the
/// strip is click-through, so a page of controls on it is a page nobody could touch, and it is the
/// one page mini cannot fit — 700 pixels of body and a nav that collapses below 900, against a
/// surface 512 wide. The second of those is <c>PanelView</c>'s rule rather than this host's, so
/// even a caller who wanted it would be declined.
/// </para>
/// <para>
/// Every property is optional and a page appears only when everything it needs is here, which is
/// how <c>VrPanelSurface</c> already works: a tab nobody furnished has no builder, registers no
/// root, and the navigator declines it — so a half-composed app draws fewer tabs rather than a
/// broken one.
/// </para>
/// </summary>
public sealed record OverlayTabs
{
    /// <summary>What the Commander is working on (Phase 25). The tab the request named.</summary>
    public D47.Core.Checklists.ChecklistService? Checklists { get; init; }

    /// <summary>
    /// The long arcs, which ride the checklist tab rather than sitting beside it (Phase 34) —
    /// so they reach this surface on exactly the same terms the list does.
    /// </summary>
    public D47.Core.Goals.GoalBook? Goals { get; init; }

    public Action? BackfillGoals { get; init; }

    /// <summary>
    /// Who to go and unlock next (Phase 28). Needs the fleet and the game state with it,
    /// because the ranking is arithmetic over where the Commander is and what they are flying.
    /// </summary>
    public D47.Core.Engineers.EngineerPlanService? Unlocks { get; init; }

    public D47.Core.Ships.ShipPlanService? Ships { get; init; }

    public Func<D47.Core.Journal.CommanderGameState?>? GameState { get; init; }

    public D47.Core.Loadout.OnFootPlanService? OnFoot { get; init; }

    /// <summary>
    /// The clocks, timers and alarms (Phase 24) — the page whose whole argument is a
    /// Commander who cannot glance at a wall clock, which is as true over a full-screen game as it
    /// is inside a headset.
    /// </summary>
    public D47.Core.Utilities.Timekeeper? Timekeeper { get; init; }

    public D47.Core.Utilities.AlarmStore? Alarms { get; init; }
}
