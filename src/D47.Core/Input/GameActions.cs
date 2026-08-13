namespace D47.Core.Input;

/// <summary>
/// One of the Elite actions a d47 action resolves to, and the modes it is the right one in.
/// <para>
/// Several actions exist twice in Elite under different names — <c>ToggleCargoScoop</c> and
/// <c>ToggleCargoScoop_Buggy</c> are the same idea bound separately for the ship and the SRV,
/// and a Commander who says "scoop" means whichever one they are currently sitting in. One
/// d47 action, several Elite actions, chosen by mode.
/// </para>
/// </summary>
/// <param name="EliteAction">The action name exactly as the bindings file spells it.</param>
/// <param name="Contexts">The modes this variant is the correct one for.</param>
public sealed record ActionVariant(string EliteAction, ControlContext Contexts);

/// <summary>One thing d47 can ask the game to do.</summary>
public sealed record GameAction
{
    /// <summary>Stable snake_case id. It is what a tool argument carries.</summary>
    public required string Id { get; init; }

    /// <summary>How a Commander hears it named, mid-sentence: "the landing gear".</summary>
    public required string Label { get; init; }

    public required string Group { get; init; }

    public required IReadOnlyList<ActionVariant> Variants { get; init; }

    /// <summary>Every mode any variant covers.</summary>
    public ControlContext Contexts =>
        Variants.Aggregate(ControlContext.None, (all, variant) => all | variant.Contexts);

    /// <summary>The variant that applies in a mode, or null if none does.</summary>
    public ActionVariant? For(ControlContext context) =>
        Variants.FirstOrDefault(variant => (variant.Contexts & context) != 0);
}

/// <summary>
/// The actions d47 will ask Elite to perform (list.md Phase 10, items 6 to 9).
/// <para>
/// <b>Curated, not generated.</b> Elite declares 369 actions and most of them are axes, camera
/// controls and multicrew plumbing that nobody asks for by voice. A generated table would
/// advertise all of them to the model, which is both a context cost and an invitation to
/// invent. Every entry here is one the checklist names.
/// </para>
/// <para>
/// <b>Two things the checklist asks for are not here, because Elite has no binding for
/// them.</b> The <i>fuel scoop</i> has no control at all — scooping starts by itself when a
/// scoop-fitted ship is inside a star's scoop zone, and the game reports it through the
/// <c>ScoopingFuel</c> status flag rather than accepting a key. <i>Boarding and leaving the
/// SRV</i> likewise go through the role panel and have no bindable action. Both are therefore
/// answerable as state and not performable as commands, and saying so is the point of
/// <i>Know which actions the Commander can actually reach</i> — an action d47 cannot perform
/// should fail as a sentence, not as silence.
/// </para>
/// </summary>
public static class GameActions
{
    public const string Flight = "Flight";
    public const string Systems = "Ship systems";
    public const string Interface = "Panels and interface";
    public const string SrvGroup = "SRV";
    public const string Weapons = "Weapons";

    /// <summary>
    /// Every action, in the order a Commander would meet them. Registration order is display
    /// order and it is stable, because the advertised profile is a projection of this list.
    /// </summary>
    public static readonly IReadOnlyList<GameAction> All =
    [
        // ---- Flight and navigation (item 6) -------------------------------------------------
        Action("landing_gear", "the landing gear", Flight,
            // Deliberately not supercruise. The key does nothing there, and an acknowledged
            // command that had no effect is worse than a refusal that explains itself.
            new ActionVariant("LandingGearToggle", ControlContext.NormalSpace | ControlContext.Landed | ControlContext.Docked)),

        Action("lights", "the lights", Flight,
            new ActionVariant("ShipSpotLightToggle", ControlContext.AnyShip),
            new ActionVariant("HeadlightsBuggyButton", ControlContext.Srv)),

        Action("cargo_scoop", "the cargo scoop", Flight,
            new ActionVariant("ToggleCargoScoop", ControlContext.NormalSpace | ControlContext.Landed | ControlContext.Docked),
            new ActionVariant("ToggleCargoScoop_Buggy", ControlContext.Srv)),

        Action("hardpoints", "the hardpoints", Flight,
            new ActionVariant("DeployHardpointToggle", ControlContext.NormalSpace)),

        Action("frame_shift_drive", "the frame shift drive", Flight,
            new ActionVariant("HyperSuperCombination", ControlContext.Flying)),

        Action("supercruise", "supercruise", Flight,
            new ActionVariant("Supercruise", ControlContext.NormalSpace)),

        Action("hyperspace", "the hyperspace jump", Flight,
            new ActionVariant("Hyperspace", ControlContext.Flying)),

        Action("flight_assist", "flight assist", Flight,
            new ActionVariant("ToggleFlightAssist", ControlContext.Flying)),

        Action("throttle_zero", "the throttle", Flight,
            new ActionVariant("SetSpeedZero", ControlContext.Flying)),

        Action("boost", "the boost", Flight,
            new ActionVariant("UseBoostJuice", ControlContext.NormalSpace)),

        // ---- Ship systems (item 7) ----------------------------------------------------------
        Action("power_to_engines", "power to engines", Systems,
            new ActionVariant("IncreaseEnginesPower", ControlContext.AnyShip),
            new ActionVariant("IncreaseEnginesPower_Buggy", ControlContext.Srv)),

        Action("power_to_weapons", "power to weapons", Systems,
            new ActionVariant("IncreaseWeaponsPower", ControlContext.AnyShip),
            new ActionVariant("IncreaseWeaponsPower_Buggy", ControlContext.Srv)),

        Action("power_to_systems", "power to systems", Systems,
            new ActionVariant("IncreaseSystemsPower", ControlContext.AnyShip),
            new ActionVariant("IncreaseSystemsPower_Buggy", ControlContext.Srv)),

        Action("balance_power", "the power distribution", Systems,
            new ActionVariant("ResetPowerDistribution", ControlContext.AnyShip),
            new ActionVariant("ResetPowerDistribution_Buggy", ControlContext.Srv)),

        // Elite's own name for silent running, kept from a much older build. Spelled the way
        // the bindings file spells it, because that is the only spelling that resolves.
        Action("silent_running", "silent running", Systems,
            new ActionVariant("ToggleButtonUpInput", ControlContext.NormalSpace)),

        Action("heat_sink", "a heat sink", Systems,
            new ActionVariant("DeployHeatSink", ControlContext.Flying)),

        Action("analysis_mode", "the HUD mode", Systems,
            new ActionVariant("PlayerHUDModeToggle", ControlContext.AnyShip)),

        // ---- Panels, interface and fire groups (item 8) -------------------------------------
        Action("left_panel", "the left panel", Interface,
            new ActionVariant("FocusLeftPanel", ControlContext.AnyShip),
            new ActionVariant("FocusLeftPanel_Buggy", ControlContext.Srv)),

        Action("right_panel", "the right panel", Interface,
            new ActionVariant("FocusRightPanel", ControlContext.AnyShip),
            new ActionVariant("FocusRightPanel_Buggy", ControlContext.Srv)),

        Action("comms_panel", "the comms panel", Interface,
            new ActionVariant("FocusCommsPanel", ControlContext.AnyShip),
            new ActionVariant("FocusCommsPanel_Buggy", ControlContext.Srv)),

        Action("role_panel", "the role panel", Interface,
            new ActionVariant("FocusRadarPanel", ControlContext.AnyShip),
            new ActionVariant("FocusRadarPanel_Buggy", ControlContext.Srv)),

        Action("next_panel", "the next panel", Interface,
            new ActionVariant("CycleNextPanel", ControlContext.AnyShip)),

        Action("previous_panel", "the previous panel", Interface,
            new ActionVariant("CyclePreviousPanel", ControlContext.AnyShip)),

        Action("galaxy_map", "the galaxy map", Interface,
            new ActionVariant("GalaxyMapOpen", ControlContext.AnyShip),
            new ActionVariant("GalaxyMapOpen_Buggy", ControlContext.Srv)),

        Action("system_map", "the system map", Interface,
            new ActionVariant("SystemMapOpen", ControlContext.AnyShip),
            new ActionVariant("SystemMapOpen_Buggy", ControlContext.Srv)),

        Action("ui_up", "up", Interface, new ActionVariant("UI_Up", ControlContext.AnyShip | ControlContext.Srv)),
        Action("ui_down", "down", Interface, new ActionVariant("UI_Down", ControlContext.AnyShip | ControlContext.Srv)),
        Action("ui_left", "left", Interface, new ActionVariant("UI_Left", ControlContext.AnyShip | ControlContext.Srv)),
        Action("ui_right", "right", Interface, new ActionVariant("UI_Right", ControlContext.AnyShip | ControlContext.Srv)),
        Action("ui_select", "select", Interface, new ActionVariant("UI_Select", ControlContext.AnyShip | ControlContext.Srv)),
        Action("ui_back", "back", Interface, new ActionVariant("UI_Back", ControlContext.AnyShip | ControlContext.Srv)),

        Action("next_fire_group", "the next fire group", Interface,
            new ActionVariant("CycleFireGroupNext", ControlContext.Flying)),

        Action("previous_fire_group", "the previous fire group", Interface,
            new ActionVariant("CycleFireGroupPrevious", ControlContext.Flying)),

        // ---- SRV (item 9) -------------------------------------------------------------------
        Action("srv_turret", "the SRV turret", SrvGroup,
            new ActionVariant("ToggleBuggyTurretButton", ControlContext.Srv)),

        Action("srv_handbrake", "the handbrake", SrvGroup,
            new ActionVariant("AutoBreakBuggyButton", ControlContext.Srv)),

        Action("srv_drive_assist", "drive assist", SrvGroup,
            new ActionVariant("ToggleDriveAssist", ControlContext.Srv)),

        Action("srv_reverse", "the SRV throttle direction", SrvGroup,
            new ActionVariant("BuggyToggleReverseThrottleInput", ControlContext.Srv)),

        // The one action that spans the SRV and standing on the surface, and the only route
        // between a Commander and their ship that Elite exposes as a binding at all.
        Action("recall_ship", "the ship recall", SrvGroup,
            new ActionVariant("RecallDismissShip", ControlContext.Srv | ControlContext.OnFoot)),

        // ---- Weapons (the honk's route in, list.md Phase 10 item 3) --------------------------
        Action("primary_fire", "the primary fire group", Weapons,
            new ActionVariant("PrimaryFire", ControlContext.NormalSpace)),

        Action("secondary_fire", "the secondary fire group", Weapons,
            new ActionVariant("SecondaryFire", ControlContext.NormalSpace)),
    ];

    private static readonly Dictionary<string, GameAction> ById =
        All.ToDictionary(action => action.Id, StringComparer.Ordinal);

    public static GameAction? Find(string id) => ById.GetValueOrDefault(id);

    public static IReadOnlyList<string> Ids => [.. All.Select(action => action.Id)];

    private static GameAction Action(string id, string label, string group, params ActionVariant[] variants) =>
        new() { Id = id, Label = label, Group = group, Variants = variants };
}
