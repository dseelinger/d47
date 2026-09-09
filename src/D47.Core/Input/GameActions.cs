using D47.Core.Journal;

namespace D47.Core.Input;

/// <summary>One of the Elite actions a d47 action resolves to, and the modes it is the right one in.</summary>
/// <param name="EliteAction">The action name exactly as the bindings file spells it.</param>
/// <param name="Contexts">The modes this variant is the correct one for.</param>
public sealed record ActionVariant(string EliteAction, ControlContext Contexts);

/// <summary>What the Commander asked for, when the action is a toggle.</summary>
public enum DesiredState
{
    /// <summary>Press it, whatever state it is in.</summary>
    Toggle,

    On,

    Off,
}

/// <summary>One thing d47 can ask the game to do.</summary>
public sealed record GameAction
{
    /// <summary>Stable snake_case id.</summary>
    public required string Id { get; init; }

    /// <summary>How a Commander hears it named, mid-sentence: "the landing gear".</summary>
    public required string Label { get; init; }

    public required string Group { get; init; }

    public required IReadOnlyList<ActionVariant> Variants { get; init; }

    /// <summary>The status flag that reports this toggle's current state, when Elite reports one.</summary>
    public StatusFlags? Reports { get; init; }

    /// <summary>
    /// True when <see cref="Reports"/> is set but reads backwards — Elite reports flight assist as
    /// <c>FlightAssistOff</c>, so the flag being set means the feature is off.
    /// </summary>
    public bool ReportsInverted { get; init; }

    /// <summary>
    /// Whole utterances that reach this action without a model, each with the state it asks for.
    /// </summary>
    public IReadOnlyList<(string Phrase, DesiredState State)> Phrases { get; init; } = [];

    /// <summary>Every mode any variant covers.</summary>
    public ControlContext Contexts =>
        Variants.Aggregate(ControlContext.None, (all, variant) => all | variant.Contexts);

    /// <summary>The variant that applies in a mode, or null if none does.</summary>
    public ActionVariant? For(ControlContext context) =>
        Variants.FirstOrDefault(variant => (variant.Contexts & context) != 0);

    /// <summary>Whether the action is already in the state that was asked for.</summary>
    public bool? AlreadyIn(DesiredState wanted, GameStatus status)
    {
        if (wanted == DesiredState.Toggle || Reports is not { } flag || !status.IsKnown)
        {
            return null;
        }

        var on = status.Has(flag) != ReportsInverted;

        return wanted == DesiredState.On ? on : !on;
    }
}

/// <summary>The actions d47 will ask Elite to perform (Phase 10, items 6 to 9).</summary>
public static class GameActions
{
    public const string Flight = "Flight";
    public const string Systems = "Ship systems";
    public const string Interface = "Panels and interface";
    public const string SrvGroup = "SRV";
    public const string Weapons = "Weapons";

    /// <summary>
    /// Actions that exist only as steps of something larger — on no tool, with no phrase, and never
    /// named by the Commander or the model.
    /// </summary>
    public const string Steps = "Compound command steps";

    /// <summary>Every action, in the order a Commander would meet them.</summary>
    public static readonly IReadOnlyList<GameAction> All =
    [
        // ---- Flight and navigation (item 6) -------------------------------------------------
        new()
        {
            Id = "landing_gear",
            Label = "the landing gear",
            Group = Flight,

            // Deliberately not supercruise.
            Variants = [new ActionVariant("LandingGearToggle", ControlContext.NormalSpace | ControlContext.Landed | ControlContext.Docked)],
            Reports = StatusFlags.LandingGearDown,
            Phrases =
            [
                ("gear down", DesiredState.On),
                ("lower the gear", DesiredState.On),
                ("put the gear down", DesiredState.On),
                ("gear up", DesiredState.Off),
                ("raise the gear", DesiredState.Off),
                ("retract the gear", DesiredState.Off),
                ("landing gear", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "lights",
            Label = "the lights",
            Group = Flight,
            Variants =
            [
                new ActionVariant("ShipSpotLightToggle", ControlContext.AnyShip),
                new ActionVariant("HeadlightsBuggyButton", ControlContext.Srv),
            ],
            Reports = StatusFlags.LightsOn,
            Phrases = [("lights on", DesiredState.On), ("lights off", DesiredState.Off), ("ship lights", DesiredState.Toggle)],
        },

        new()
        {
            Id = "cargo_scoop",
            Label = "the cargo scoop",
            Group = Flight,
            Variants =
            [
                new ActionVariant("ToggleCargoScoop", ControlContext.NormalSpace | ControlContext.Landed | ControlContext.Docked),
                new ActionVariant("ToggleCargoScoop_Buggy", ControlContext.Srv),
            ],
            Reports = StatusFlags.CargoScoopDeployed,
            Phrases =
            [
                ("open the cargo scoop", DesiredState.On),
                ("close the cargo scoop", DesiredState.Off),
                ("cargo scoop", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "hardpoints",
            Label = "the hardpoints",
            Group = Flight,
            Variants = [new ActionVariant("DeployHardpointToggle", ControlContext.NormalSpace)],
            Reports = StatusFlags.HardpointsDeployed,
            Phrases =
            [
                ("deploy hardpoints", DesiredState.On),
                ("hardpoints out", DesiredState.On),
                ("retract hardpoints", DesiredState.Off),
                ("hardpoints in", DesiredState.Off),
                ("toggle hardpoints", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "frame_shift_drive",
            Label = "the frame shift drive",
            Group = Flight,
            Variants = [new ActionVariant("HyperSuperCombination", ControlContext.Flying)],
            Phrases = [("frame shift drive", DesiredState.Toggle), ("engage the frame shift drive", DesiredState.Toggle)],
        },

        new()
        {
            Id = "supercruise",
            Label = "supercruise",
            Group = Flight,
            Variants = [new ActionVariant("Supercruise", ControlContext.NormalSpace)],

            // The bare word is safe here for the reason the router is whole-utterance: "engage supercruise"
            // is a different utterance from "supercruise" and matches its own phrase, so the short one
            // shadows nothing (Phase 52, item 1).
            Phrases =
            [
                ("supercruise", DesiredState.Toggle),
                ("engage supercruise", DesiredState.Toggle),
                ("take us to supercruise", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "hyperspace",
            Label = "the hyperspace jump",
            Group = Flight,
            Variants = [new ActionVariant("Hyperspace", ControlContext.Flying)],

            // "Engage" is the Commander's word for the jump (Phase 52, item 1), and it is a whole utterance
            // rather than a keyword.
            Phrases =
            [
                ("engage", DesiredState.Toggle),
                ("hyperspace jump", DesiredState.Toggle),
                ("jump to the next system", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "flight_assist",
            Label = "flight assist",
            Group = Flight,
            Variants = [new ActionVariant("ToggleFlightAssist", ControlContext.Flying)],

            // Elite reports the negative, so the flag being set means the feature is off.
            Reports = StatusFlags.FlightAssistOff,
            ReportsInverted = true,
            Phrases =
            [
                ("flight assist on", DesiredState.On),
                ("flight assist off", DesiredState.Off),
                ("toggle flight assist", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "throttle_zero",
            Label = "the throttle",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeedZero", ControlContext.Flying)],

            // Not "stop".
            Phrases = [("all stop", DesiredState.Toggle), ("throttle to zero", DesiredState.Toggle)],
        },

        new()
        {
            // The other end of the throttle, and the first step of "separate" (Phase 52, item 3).
            Id = "throttle_full",
            Label = "full throttle",
            Group = Steps,
            Variants = [new ActionVariant("SetSpeed100", ControlContext.Flying)],
        },

        new()
        {
            Id = "boost",
            Label = "the boost",
            Group = Flight,
            Variants = [new ActionVariant("UseBoostJuice", ControlContext.NormalSpace)],
            Phrases = [("engage boost", DesiredState.Toggle), ("boost us", DesiredState.Toggle)],
        },

        // ---- Ship systems (item 7) ----------------------------------------------------------
        new()
        {
            Id = "power_to_engines",
            Label = "power to engines",
            Group = Systems,
            Variants =
            [
                new ActionVariant("IncreaseEnginesPower", ControlContext.AnyShip),
                new ActionVariant("IncreaseEnginesPower_Buggy", ControlContext.Srv),
            ],
            Phrases = [("pips to engines", DesiredState.Toggle), ("power to engines", DesiredState.Toggle)],
        },

        new()
        {
            Id = "power_to_weapons",
            Label = "power to weapons",
            Group = Systems,
            Variants =
            [
                new ActionVariant("IncreaseWeaponsPower", ControlContext.AnyShip),
                new ActionVariant("IncreaseWeaponsPower_Buggy", ControlContext.Srv),
            ],
            Phrases = [("pips to weapons", DesiredState.Toggle), ("power to weapons", DesiredState.Toggle)],
        },

        new()
        {
            Id = "power_to_systems",
            Label = "power to systems",
            Group = Systems,
            Variants =
            [
                new ActionVariant("IncreaseSystemsPower", ControlContext.AnyShip),
                new ActionVariant("IncreaseSystemsPower_Buggy", ControlContext.Srv),
            ],
            Phrases = [("pips to systems", DesiredState.Toggle), ("power to systems", DesiredState.Toggle)],
        },

        new()
        {
            Id = "balance_power",
            Label = "the power distribution",
            Group = Systems,
            Variants =
            [
                new ActionVariant("ResetPowerDistribution", ControlContext.AnyShip),
                new ActionVariant("ResetPowerDistribution_Buggy", ControlContext.Srv),
            ],
            Phrases = [("balance the power", DesiredState.Toggle), ("balance power", DesiredState.Toggle)],
        },

        // Elite's own name for silent running, kept from a much older build.
        new()
        {
            Id = "silent_running",
            Label = "silent running",
            Group = Systems,
            Variants = [new ActionVariant("ToggleButtonUpInput", ControlContext.NormalSpace)],
            Reports = StatusFlags.SilentRunning,
            Phrases =
            [
                ("silent running on", DesiredState.On),
                ("silent running off", DesiredState.Off),
                ("silent running", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "heat_sink",
            Label = "a heat sink",
            Group = Systems,
            Variants = [new ActionVariant("DeployHeatSink", ControlContext.Flying)],
            Phrases = [("heat sink", DesiredState.Toggle), ("drop a heat sink", DesiredState.Toggle)],
        },

        new()
        {
            Id = "analysis_mode",
            Label = "the HUD mode",
            Group = Systems,
            Variants = [new ActionVariant("PlayerHUDModeToggle", ControlContext.AnyShip)],
            Reports = StatusFlags.AnalysisMode,
            Phrases =
            [
                ("analysis mode", DesiredState.On),
                ("combat mode", DesiredState.Off),
                ("switch hud mode", DesiredState.Toggle),
            ],
        },

        // ---- Panels, interface and fire groups (item 8) -------------------------------------
        Simple("left_panel", "the left panel", Interface, "FocusLeftPanel", "FocusLeftPanel_Buggy",
            ["left panel", "open the left panel"]),

        Simple("right_panel", "the right panel", Interface, "FocusRightPanel", "FocusRightPanel_Buggy",
            ["right panel", "open the right panel"]),

        Simple("comms_panel", "the comms panel", Interface, "FocusCommsPanel", "FocusCommsPanel_Buggy",
            ["comms panel", "open the comms panel"]),

        Simple("role_panel", "the role panel", Interface, "FocusRadarPanel", "FocusRadarPanel_Buggy",
            ["role panel", "open the role panel"]),

        Simple("next_panel", "the next panel", Interface, "CycleNextPanel", null, ["next panel"]),
        Simple("previous_panel", "the previous panel", Interface, "CyclePreviousPanel", null, ["previous panel"]),

        Simple("galaxy_map", "the galaxy map", Interface, "GalaxyMapOpen", "GalaxyMapOpen_Buggy",
            ["galaxy map", "open the galaxy map"]),

        Simple("system_map", "the system map", Interface, "SystemMapOpen", "SystemMapOpen_Buggy",
            ["system map", "open the system map"]),

        Ui("ui_up", "up"),
        Ui("ui_down", "down"),
        Ui("ui_left", "left"),
        Ui("ui_right", "right"),
        Ui("ui_select", "select"),
        Ui("ui_back", "back"),

        new()
        {
            Id = "next_fire_group",
            Label = "the next fire group",
            Group = Interface,
            Variants = [new ActionVariant("CycleFireGroupNext", ControlContext.Flying)],
            Phrases = [("next fire group", DesiredState.Toggle)],
        },

        new()
        {
            Id = "previous_fire_group",
            Label = "the previous fire group",
            Group = Interface,
            Variants = [new ActionVariant("CycleFireGroupPrevious", ControlContext.Flying)],
            Phrases = [("previous fire group", DesiredState.Toggle)],
        },

        // ---- SRV (item 9) -------------------------------------------------------------------
        new()
        {
            Id = "srv_turret",
            Label = "the SRV turret",
            Group = SrvGroup,
            Variants = [new ActionVariant("ToggleBuggyTurretButton", ControlContext.Srv)],
            Reports = StatusFlags.SrvTurretView,
            Phrases = [("turret on", DesiredState.On), ("turret off", DesiredState.Off), ("the turret", DesiredState.Toggle)],
        },

        new()
        {
            Id = "srv_handbrake",
            Label = "the handbrake",
            Group = SrvGroup,
            Variants = [new ActionVariant("AutoBreakBuggyButton", ControlContext.Srv)],
            Reports = StatusFlags.SrvHandbrake,
            Phrases =
            [
                ("handbrake on", DesiredState.On),
                ("handbrake off", DesiredState.Off),
                ("the handbrake", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "srv_drive_assist",
            Label = "drive assist",
            Group = SrvGroup,
            Variants = [new ActionVariant("ToggleDriveAssist", ControlContext.Srv)],
            Reports = StatusFlags.SrvDriveAssist,
            Phrases =
            [
                ("drive assist on", DesiredState.On),
                ("drive assist off", DesiredState.Off),
                ("drive assist", DesiredState.Toggle),
            ],
        },

        new()
        {
            Id = "srv_reverse",
            Label = "the SRV throttle direction",
            Group = SrvGroup,
            Variants = [new ActionVariant("BuggyToggleReverseThrottleInput", ControlContext.Srv)],
            Phrases = [("reverse the srv", DesiredState.Toggle)],
        },

        // The one action that spans the SRV and standing on the surface, and the only route between a
        // Commander and their ship that Elite exposes as a binding at all.
        new()
        {
            Id = "recall_ship",
            Label = "the ship recall",
            Group = SrvGroup,
            Variants = [new ActionVariant("RecallDismissShip", ControlContext.Srv | ControlContext.OnFoot)],
            Phrases =
            [
                ("recall my ship", DesiredState.Toggle),
                ("recall the ship", DesiredState.Toggle),
                ("dismiss my ship", DesiredState.Toggle),
                ("dismiss the ship", DesiredState.Toggle),
            ],
        },

        // ---- Weapons (the honk's route in, Phase 10 item 3) -------------------------- No phrases and no
        // tool.
        new()
        {
            Id = "primary_fire",
            Label = "the primary fire group",
            Group = Weapons,

            // Flying, not normal space.
            Variants = [new ActionVariant("PrimaryFire", ControlContext.Flying)],
        },

        new()
        {
            Id = "secondary_fire",
            Label = "the secondary fire group",
            Group = Weapons,
            Variants = [new ActionVariant("SecondaryFire", ControlContext.NormalSpace)],
        },
    ];

    private static readonly Dictionary<string, GameAction> ById =
        All.ToDictionary(action => action.Id, StringComparer.Ordinal);

    public static GameAction? Find(string id) => ById.GetValueOrDefault(id);

    public static IReadOnlyList<string> Ids => [.. All.Select(action => action.Id)];

    /// <summary>An action with a ship form and, usually, an SRV twin.</summary>
    private static GameAction Simple(
        string id,
        string label,
        string group,
        string shipAction,
        string? srvAction,
        IReadOnlyList<string> phrases) => new()
    {
        Id = id,
        Label = label,
        Group = group,
        Variants = srvAction is null
            ? [new ActionVariant(shipAction, ControlContext.AnyShip)]
            : [new ActionVariant(shipAction, ControlContext.AnyShip), new ActionVariant(srvAction, ControlContext.Srv)],
        Phrases = [.. phrases.Select(phrase => (phrase, DesiredState.Toggle))],
    };

    /// <summary>One of the six panel-navigation keys.</summary>
    private static GameAction Ui(string id, string word) => new()
    {
        Id = id,
        Label = word,
        Group = Interface,
        Variants = [new ActionVariant($"UI_{char.ToUpperInvariant(word[0])}{word[1..]}", ControlContext.AnyShip | ControlContext.Srv)],
        Phrases = [(word, DesiredState.Toggle)],
    };
}
