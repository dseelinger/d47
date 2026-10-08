using D47.Core.Actions;
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

    /// <summary>How long the key is held; null taps it.</summary>
    public TimeSpan? HoldFor { get; init; }

    /// <summary>The full-screen interface that must have focus; the action is refused with <see cref="FocusName"/> not open otherwise.</summary>
    public GuiFocus? RequiresFocus { get; init; }

    /// <summary>The full-screen interface that must not have focus; the action is refused as already open otherwise.</summary>
    public GuiFocus? ExcludesFocus { get; init; }

    /// <summary>How the interface named by <see cref="RequiresFocus"/> or <see cref="ExcludesFocus"/> is spoken: "the FSS".</summary>
    public string? FocusName { get; init; }

    /// <summary>The status flag that reports this toggle's current state, when Elite reports one.</summary>
    public StatusFlags? Reports { get; init; }

    /// <summary>
    /// True when <see cref="Reports"/> is set but reads backwards — Elite reports flight assist as
    /// <c>FlightAssistOff</c>, so the flag being set means the feature is off.
    /// </summary>
    public bool ReportsInverted { get; init; }

    /// <summary>The modes in which <see cref="Reports"/> can be trusted; null means every mode.</summary>
    public ControlContext? ReportsIn { get; init; }

    /// <summary>How <see cref="Phrases"/> are built from <see cref="Names"/>.</summary>
    public PhraseShape Shape { get; init; } = PhraseShape.OneShot;

    /// <summary>What the Commander calls it. The first is the one an acknowledgement says.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>For a one-shot, the words said before a name: "drop a" in "drop a heat sink".</summary>
    public IReadOnlyList<string> Verbs { get; init; } = [];

    /// <summary>For a switch, the words before a name that turn it on: "deploy" in "deploy the gear".</summary>
    public IReadOnlyList<string> OnVerbs { get; init; } = [];

    public IReadOnlyList<string> OffVerbs { get; init; } = [];

    /// <summary>Whole utterances that set a state without naming the action: "drop out".</summary>
    public IReadOnlyList<(string Phrase, DesiredState State)> ExtraPhrases { get; init; } = [];

    /// <summary>For a switch, the word after a name that turns it on: "down" in "gear down".</summary>
    public string? OnParticle { get; init; }

    public string? OffParticle { get; init; }

    /// <summary>For two named modes, the mode that is on and the mode that is off.</summary>
    public (string On, string Off)? Modes { get; init; }

    /// <summary>
    /// Whole utterances that reach this action without a model, each with the state it asks for. The
    /// first for each state is the one an acknowledgement says.
    /// </summary>
    public IReadOnlyList<(string Phrase, DesiredState State)> Phrases => ActionGrammar.Phrases(this);

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

        if (ReportsIn is { } trusted && (ControlContexts.Of(status) & trusted) == 0)
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
    public const string Combat = "Combat";
    public const string OnFoot = "On foot";
    public const string Weapons = "Weapons";

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
            Shape = PhraseShape.Switch,
            Names = ["gear", "landing gear"],
            OnVerbs = ["deploy", "lower"],
            OnParticle = "down",
            OffVerbs = ["retract", "raise"],
            OffParticle = "up",
        },

        new()
        {
            Id = "lights",
            Label = "the lights",
            Group = Flight,
            Variants =
            [
                new ActionVariant("ShipSpotLightToggle", ControlContext.AnyShip | ControlContext.Fighter),
                new ActionVariant("HeadlightsBuggyButton", ControlContext.Srv),
                new ActionVariant("HumanoidToggleFlashlightButton", ControlContext.OnFoot),
            ],
            Reports = StatusFlags.LightsOn,

            // Status.json has no flag for the suit light, so on foot "on" and "off" press the toggle.
            ReportsIn = ControlContext.AnyShip | ControlContext.Srv,
            Shape = PhraseShape.Switch,
            Names = ["lights", "ship lights", "headlights"],
        },

        new()
        {
            Id = "night_vision",
            Label = "night vision",
            Group = Flight,
            Variants =
            [
                new ActionVariant("NightVisionToggle", ControlContext.AnyShip | ControlContext.Srv),
                new ActionVariant("HumanoidToggleNightVisionButton", ControlContext.OnFoot),
            ],
            Reports = StatusFlags.NightVision,

            // Whether Elite sets the flag on foot is unverified, so on foot "on" and "off" press the toggle.
            ReportsIn = ControlContext.AnyShip | ControlContext.Srv,
            Shape = PhraseShape.Switch,
            Names = ["night vision"],
            OnVerbs = ["enable"],
            OffVerbs = ["disable"],
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
            Shape = PhraseShape.Switch,
            Names = ["cargo scoop", "cargo hatch", "scoop"],
            OnVerbs = ["open", "deploy"],
            OnParticle = "out",
            OffVerbs = ["close", "retract"],
            OffParticle = "away",
        },

        new()
        {
            Id = "hardpoints",
            Label = "the hardpoints",
            Group = Flight,
            Variants = [new ActionVariant("DeployHardpointToggle", ControlContext.NormalSpace)],
            Reports = StatusFlags.HardpointsDeployed,
            Shape = PhraseShape.Switch,
            Names = ["hardpoints"],
            OnVerbs = ["deploy"],
            OnParticle = "out",
            OffVerbs = ["retract", "stow"],
            OffParticle = "in",
        },

        new()
        {
            Id = "frame_shift_drive",
            Label = "the frame shift drive",
            Group = Flight,
            Variants = [new ActionVariant("HyperSuperCombination", ControlContext.Flying)],
            Names = ["frame shift drive"],
            Verbs = ["engage"],
        },

        new()
        {
            Id = "supercruise",
            Label = "supercruise",
            Group = Flight,
            Variants = [new ActionVariant("Supercruise", ControlContext.Flying)],
            Reports = StatusFlags.Supercruise,
            Shape = PhraseShape.Switch,
            Names = ["supercruise", "cruise", "warp"],
            OnVerbs = ["engage", "take us to"],
            OffVerbs = ["drop out of", "exit"],
            ExtraPhrases = [("drop out", DesiredState.Off)],
        },

        new()
        {
            Id = "hyperspace",
            Label = "the hyperspace jump",
            Group = Flight,
            Variants = [new ActionVariant("Hyperspace", ControlContext.Flying)],
            Names = ["hyperspace", "jump", "hyperspace jump", "engage", "engage hyperspace", "jump to the next system"],
        },

        new()
        {
            Id = "flight_assist",
            Label = "flight assist",
            Group = Flight,
            Variants = [new ActionVariant("ToggleFlightAssist", ControlContext.Flying | ControlContext.Fighter)],

            // Elite reports the negative, so the flag being set means the feature is off.
            Reports = StatusFlags.FlightAssistOff,
            ReportsInverted = true,

            // Whether Status.json sets the flag in a fighter is unverified, so there "on" and "off" press the toggle.
            ReportsIn = ControlContext.AnyShip | ControlContext.Srv,
            Shape = PhraseShape.Switch,
            Names = ["flight assist"],
            OnVerbs = ["enable"],
            OffVerbs = ["disable"],
        },

        new()
        {
            Id = "throttle_zero",
            Label = "the throttle",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeedZero", ControlContext.Flying | ControlContext.Fighter)],

            // Not "stop".
            Names = ["all stop", "throttle to zero"],
        },

        new()
        {
            Id = "throttle_25",
            Label = "quarter throttle",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeed25", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["throttle to twenty-five", "twenty-five per cent"],
        },

        new()
        {
            Id = "throttle_50",
            Label = "half throttle",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeed50", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["throttle to fifty", "fifty per cent"],
        },

        new()
        {
            // SetSpeed75 is 75% thrust. Not military power in the aviation sense (full thrust, no
            // afterburner) — do not "correct" this to 100%.
            Id = "throttle_75",
            Label = "military thrust",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeed75", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["military thrust", "military power", "throttle to seventy-five", "seventy-five per cent"],
        },

        new()
        {
            Id = "throttle_full",
            Label = "full throttle",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeed100", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["full throttle", "full speed", "throttle to a hundred"],
        },

        new()
        {
            Id = "throttle_reverse_25",
            Label = "quarter reverse",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeedMinus25", ControlContext.NormalSpace)],
            Names = ["throttle to minus twenty-five"],
        },

        new()
        {
            Id = "throttle_reverse_50",
            Label = "half reverse",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeedMinus50", ControlContext.NormalSpace)],
            Names = ["throttle to minus fifty", "half reverse"],
        },

        new()
        {
            Id = "throttle_reverse_75",
            Label = "three-quarter reverse",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeedMinus75", ControlContext.NormalSpace)],
            Names = ["throttle to minus seventy-five"],
        },

        new()
        {
            Id = "throttle_reverse_full",
            Label = "full reverse",
            Group = Flight,
            Variants = [new ActionVariant("SetSpeedMinus100", ControlContext.NormalSpace)],
            Names = ["full reverse", "throttle to minus a hundred"],
        },

        new()
        {
            Id = "reverse_thrust",
            Label = "reverse thrust",
            Group = Flight,
            Variants = [new ActionVariant("ToggleReverseThrottleInput", ControlContext.NormalSpace)],
            Names = ["reverse thrust", "toggle reverse thrust"],
        },

        new()
        {
            Id = "boost",
            Label = "the boost",
            Group = Flight,
            Variants = [new ActionVariant("UseBoostJuice", ControlContext.NormalSpace | ControlContext.Fighter)],
            Names = ["boost", "boost us"],
            Verbs = ["engage"],
        },

        new()
        {
            Id = "target_next_route_system",
            Label = "the next system in the route",
            Group = Flight,
            Variants = [new ActionVariant("TargetNextRouteSystem", ControlContext.Flying)],
            Names = ["next system", "next system in route"],
            Verbs = ["target"],
        },

        // ---- Ship systems (item 7) ----------------------------------------------------------
        new()
        {
            Id = "power_to_engines",
            Label = "power to engines",
            Group = Systems,
            Variants =
            [
                new ActionVariant("IncreaseEnginesPower", ControlContext.AnyShip | ControlContext.Fighter),
                new ActionVariant("IncreaseEnginesPower_Buggy", ControlContext.Srv),
            ],
            Names = ["pips to engines", "power to engines"],
        },

        new()
        {
            Id = "power_to_weapons",
            Label = "power to weapons",
            Group = Systems,
            Variants =
            [
                new ActionVariant("IncreaseWeaponsPower", ControlContext.AnyShip | ControlContext.Fighter),
                new ActionVariant("IncreaseWeaponsPower_Buggy", ControlContext.Srv),
            ],
            Names = ["pips to weapons", "power to weapons"],
        },

        new()
        {
            Id = "power_to_systems",
            Label = "power to systems",
            Group = Systems,
            Variants =
            [
                new ActionVariant("IncreaseSystemsPower", ControlContext.AnyShip | ControlContext.Fighter),
                new ActionVariant("IncreaseSystemsPower_Buggy", ControlContext.Srv),
            ],
            Names = ["pips to systems", "power to systems"],
        },

        new()
        {
            Id = "balance_power",
            Label = "the power distribution",
            Group = Systems,
            Variants =
            [
                new ActionVariant("ResetPowerDistribution", ControlContext.AnyShip | ControlContext.Fighter),
                new ActionVariant("ResetPowerDistribution_Buggy", ControlContext.Srv),
            ],
            Names = ["balance power"],
        },

        // Elite's own name for silent running, kept from a much older build.
        new()
        {
            Id = "silent_running",
            Label = "silent running",
            Group = Systems,
            Variants = [new ActionVariant("ToggleButtonUpInput", ControlContext.NormalSpace)],
            Reports = StatusFlags.SilentRunning,
            Shape = PhraseShape.Switch,
            Names = ["silent running"],
            OnVerbs = ["enable", "engage"],
            OffVerbs = ["disable"],
        },

        new()
        {
            Id = "heat_sink",
            Label = "a heat sink",
            Group = Systems,
            Variants = [new ActionVariant("DeployHeatSink", ControlContext.Flying)],
            Names = ["heat sink"],
            Verbs = ["drop a"],
        },

        new()
        {
            Id = "chaff",
            Label = "chaff",
            Group = Systems,
            Variants = [new ActionVariant("FireChaffLauncher", ControlContext.NormalSpace)],
            Names = ["chaff"],
            Verbs = ["deploy", "fire"],
        },

        new()
        {
            Id = "shield_cell",
            Label = "a shield cell",
            Group = Systems,
            Variants = [new ActionVariant("UseShieldCell", ControlContext.NormalSpace)],
            Names = ["shield cell"],
            Verbs = ["use a", "fire a"],
        },

        new()
        {
            Id = "ecm",
            Label = "the ECM",
            Group = Systems,
            Variants = [new ActionVariant("ChargeECM", ControlContext.NormalSpace)],

            // ECM fires on release once charged.
            HoldFor = TimeSpan.FromSeconds(4),
            Names = ["ecm"],
            Verbs = ["fire the", "charge the"],
        },

        new()
        {
            Id = "analysis_mode",
            Label = "the HUD mode",
            Group = Systems,
            Variants =
            [
                new ActionVariant("PlayerHUDModeToggle", ControlContext.AnyShip),
                new ActionVariant("PlayerHUDModeToggle_Buggy", ControlContext.Srv),
            ],
            Reports = StatusFlags.AnalysisMode,

            // Whether Elite sets the flag in the SRV is unverified, so there "on" and "off" press the toggle.
            ReportsIn = ControlContext.AnyShip,
            Shape = PhraseShape.Modes,
            Names = ["hud mode"],
            Modes = ("analysis mode", "combat mode"),
        },

        // ---- Panels, interface and fire groups (item 8) -------------------------------------
        Simple("left_panel", "the left panel", Interface, "FocusLeftPanel", "FocusLeftPanel_Buggy", "left panel", "open"),
        Simple("right_panel", "the right panel", Interface, "FocusRightPanel", "FocusRightPanel_Buggy", "right panel", "open"),
        Simple("comms_panel", "the comms panel", Interface, "FocusCommsPanel", "FocusCommsPanel_Buggy", "comms panel", "open", "FocusCommsPanel_Humanoid"),
        Simple("role_panel", "the role panel", Interface, "FocusRadarPanel", "FocusRadarPanel_Buggy", "role panel", "open"),
        Simple("next_panel", "the next panel", Interface, "CycleNextPanel", null, "next panel"),
        Simple("previous_panel", "the previous panel", Interface, "CyclePreviousPanel", null, "previous panel"),
        Simple("galaxy_map", "the galaxy map", Interface, "GalaxyMapOpen", "GalaxyMapOpen_Buggy", "galaxy map", "open", "GalaxyMapOpen_Humanoid"),
        Simple("system_map", "the system map", Interface, "SystemMapOpen", "SystemMapOpen_Buggy", "system map", "open", "SystemMapOpen_Humanoid"),

        new()
        {
            Id = "fss_open",
            Label = "the FSS",
            Group = Interface,
            Variants = [new ActionVariant("ExplorationFSSEnter", ControlContext.Flying)],
            ExcludesFocus = GuiFocus.FssMode,
            FocusName = "the FSS",
            Names = ["fss", "full spectrum scanner"],
            Verbs = ["open the", "open"],
        },

        new()
        {
            Id = "fss_close",
            Label = "the FSS",
            Group = Interface,
            Variants = [new ActionVariant("ExplorationFSSQuit", ControlContext.Flying)],
            RequiresFocus = GuiFocus.FssMode,
            FocusName = "the FSS",
            Names = ["leave the fss", "close the fss", "exit the fss"],
        },

        new()
        {
            Id = "fss_discovery_scan",
            Label = "the discovery scan",
            Group = Interface,
            Variants = [new ActionVariant("ExplorationFSSDiscoveryScan", ControlContext.Flying)],
            HoldFor = HonkOnArrival.Charge,
            RequiresFocus = GuiFocus.FssMode,
            FocusName = "the FSS",
            Names = ["discovery scan", "honk"],
        },

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
            Variants =
            [
                new ActionVariant("CycleFireGroupNext", ControlContext.Flying | ControlContext.Fighter),
                new ActionVariant("BuggyCycleFireGroupNext", ControlContext.Srv),
            ],
            Names = ["next fire group"],
        },

        new()
        {
            Id = "previous_fire_group",
            Label = "the previous fire group",
            Group = Interface,
            Variants =
            [
                new ActionVariant("CycleFireGroupPrevious", ControlContext.Flying | ControlContext.Fighter),
                new ActionVariant("BuggyCycleFireGroupPrevious", ControlContext.Srv),
            ],
            Names = ["previous fire group"],
        },

        new()
        {
            Id = "next_tab",
            Label = "the next tab",
            Group = Interface,
            Variants =
            [
                new ActionVariant("CycleNextPage", ControlContext.AnyShip | ControlContext.Srv),
            ],
            Names = ["next tab", "next page"],
        },

        new()
        {
            Id = "previous_tab",
            Label = "the previous tab",
            Group = Interface,
            Variants =
            [
                new ActionVariant("CyclePreviousPage", ControlContext.AnyShip | ControlContext.Srv),
            ],
            Names = ["previous tab", "previous page"],
        },

        new()
        {
            Id = "ui_focus",
            Label = "the panel focus",
            Group = Interface,
            Variants =
            [
                new ActionVariant("UIFocus", ControlContext.AnyShip),
                new ActionVariant("UIFocus_Buggy", ControlContext.Srv),
            ],
            Names = ["ui focus", "focus the panels"],
        },

        new()
        {
            Id = "quick_comms",
            Label = "quick comms",
            Group = Interface,
            Variants =
            [
                new ActionVariant("QuickCommsPanel", ControlContext.AnyShip),
                new ActionVariant("QuickCommsPanel_Buggy", ControlContext.Srv),
                new ActionVariant("QuickCommsPanel_Humanoid", ControlContext.OnFoot),
            ],
            Names = ["quick comms"],
            Verbs = ["open"],
        },

        new()
        {
            Id = "orbit_lines",
            Label = "the orbit lines",
            Group = Interface,
            Variants =
            [
                new ActionVariant("OrbitLinesToggle", ControlContext.Flying),
            ],
            Names = ["orbit lines", "toggle orbit lines"],
        },

        new()
        {
            Id = "galaxy_map_home",
            Label = "galaxy map home",
            Group = Interface,
            Variants =
            [
                new ActionVariant("GalaxyMapHome", ControlContext.AnyShip | ControlContext.Srv | ControlContext.OnFoot),
            ],
            RequiresFocus = GuiFocus.GalaxyMap,
            FocusName = "the galaxy map",
            Names = ["galaxy map home", "centre on my system"],
        },

        new()
        {
            Id = "sensor_range_up",
            Label = "the sensor range",
            Group = Interface,
            Variants =
            [
                new ActionVariant("RadarIncreaseRange", ControlContext.Flying),
            ],
            Names = ["increase sensor range", "sensor range up"],
        },

        new()
        {
            Id = "sensor_range_down",
            Label = "the sensor range",
            Group = Interface,
            Variants =
            [
                new ActionVariant("RadarDecreaseRange", ControlContext.Flying),
            ],
            Names = ["decrease sensor range", "sensor range down"],
        },

        // ---- SRV (item 9) -------------------------------------------------------------------
        new()
        {
            Id = "srv_turret",
            Label = "the SRV turret",
            Group = SrvGroup,
            Variants = [new ActionVariant("ToggleBuggyTurretButton", ControlContext.Srv)],
            Reports = StatusFlags.SrvTurretView,
            Shape = PhraseShape.Switch,
            Names = ["turret", "turret view"],
        },

        new()
        {
            Id = "srv_handbrake",
            Label = "the handbrake",
            Group = SrvGroup,
            Variants = [new ActionVariant("AutoBreakBuggyButton", ControlContext.Srv)],
            Reports = StatusFlags.SrvHandbrake,
            Shape = PhraseShape.Switch,
            Names = ["handbrake"],
        },

        new()
        {
            Id = "srv_drive_assist",
            Label = "drive assist",
            Group = SrvGroup,
            Variants = [new ActionVariant("ToggleDriveAssist", ControlContext.Srv)],
            Reports = StatusFlags.SrvDriveAssist,
            Shape = PhraseShape.Switch,
            Names = ["drive assist"],
            OnVerbs = ["enable"],
            OffVerbs = ["disable"],
        },

        new()
        {
            Id = "srv_reverse",
            Label = "the SRV throttle direction",
            Group = SrvGroup,
            Variants = [new ActionVariant("BuggyToggleReverseThrottleInput", ControlContext.Srv)],
            Names = ["reverse the srv"],
        },

        // The one action that spans the SRV and standing on the surface, and the only route between a
        // Commander and their ship that Elite exposes as a binding at all.
        new()
        {
            Id = "recall_ship",
            Label = "the ship recall",
            Group = SrvGroup,
            Variants = [new ActionVariant("RecallDismissShip", ControlContext.Srv | ControlContext.OnFoot)],
            Names = ["recall my ship", "recall the ship", "dismiss my ship", "dismiss the ship"],
        },

        // ---- Combat: targeting and fighter orders -------------------------------------------
        new()
        {
            Id = "select_target",
            Label = "the target ahead",
            Group = Combat,
            Variants = [new ActionVariant("SelectTarget", ControlContext.Flying | ControlContext.Fighter), new ActionVariant("SelectTarget_Buggy", ControlContext.Srv)],
            Names = ["select target", "target ahead"],
        },

        new()
        {
            Id = "next_target",
            Label = "the next target",
            Group = Combat,
            Variants = [new ActionVariant("CycleNextTarget", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["next target"],
        },

        new()
        {
            Id = "previous_target",
            Label = "the previous target",
            Group = Combat,
            Variants = [new ActionVariant("CyclePreviousTarget", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["previous target"],
        },

        new()
        {
            Id = "next_hostile",
            Label = "the next hostile",
            Group = Combat,
            Variants = [new ActionVariant("CycleNextHostileTarget", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["next hostile"],
        },

        new()
        {
            Id = "previous_hostile",
            Label = "the previous hostile",
            Group = Combat,
            Variants = [new ActionVariant("CyclePreviousHostileTarget", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["previous hostile"],
        },

        new()
        {
            Id = "highest_threat",
            Label = "the highest threat",
            Group = Combat,
            Variants = [new ActionVariant("SelectHighestThreat", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["highest threat"],
            Verbs = ["target the"],
        },

        new()
        {
            Id = "next_subsystem",
            Label = "the next subsystem",
            Group = Combat,
            Variants = [new ActionVariant("CycleNextSubsystem", ControlContext.NormalSpace)],
            Names = ["next subsystem"],
        },

        new()
        {
            Id = "previous_subsystem",
            Label = "the previous subsystem",
            Group = Combat,
            Variants = [new ActionVariant("CyclePreviousSubsystem", ControlContext.NormalSpace)],
            Names = ["previous subsystem"],
        },

        new()
        {
            Id = "target_wingman_1",
            Label = "wingman one",
            Group = Combat,
            Variants = [new ActionVariant("TargetWingman0", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["wingman one"],
            Verbs = ["target"],
        },

        new()
        {
            Id = "target_wingman_2",
            Label = "wingman two",
            Group = Combat,
            Variants = [new ActionVariant("TargetWingman1", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["wingman two"],
            Verbs = ["target"],
        },

        new()
        {
            Id = "target_wingman_3",
            Label = "wingman three",
            Group = Combat,
            Variants = [new ActionVariant("TargetWingman2", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["wingman three"],
            Verbs = ["target"],
        },

        new()
        {
            Id = "wingman_target",
            Label = "the wingman's target",
            Group = Combat,
            Variants = [new ActionVariant("SelectTargetsTarget", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["wingman's target"],
            Verbs = ["target"],
        },

        new()
        {
            Id = "wingman_nav_lock",
            Label = "the wingman nav lock",
            Group = Combat,
            Variants = [new ActionVariant("WingNavLock", ControlContext.Flying | ControlContext.Fighter)],
            Names = ["wingman nav lock", "nav lock"],
        },

        new()
        {
            Id = "fighter_orders",
            Label = "the fighter orders",
            Group = Combat,
            Variants = [new ActionVariant("OpenOrders", ControlContext.NormalSpace)],
            Names = ["fighter orders"],
        },

        new()
        {
            Id = "fighter_dock",
            Label = "the fighter recall",
            Group = Combat,
            Variants = [new ActionVariant("OrderRequestDock", ControlContext.NormalSpace)],
            Names = ["recall the fighter", "fighter dock"],
        },

        new()
        {
            Id = "fighter_defend",
            Label = "fighter defend",
            Group = Combat,
            Variants = [new ActionVariant("OrderDefensiveBehaviour", ControlContext.NormalSpace)],
            Names = ["fighter defend", "fighter defensive"],
        },

        new()
        {
            Id = "fighter_engage",
            Label = "fighter engage",
            Group = Combat,
            Variants = [new ActionVariant("OrderAggressiveBehaviour", ControlContext.NormalSpace)],
            Names = ["fighter engage at will", "fighter attack"],
        },

        new()
        {
            Id = "fighter_focus",
            Label = "fighter focus",
            Group = Combat,
            Variants = [new ActionVariant("OrderFocusTarget", ControlContext.NormalSpace)],
            Names = ["fighter attack my target", "fighter focus my target"],
        },

        new()
        {
            Id = "fighter_hold_fire",
            Label = "fighter hold fire",
            Group = Combat,
            Variants = [new ActionVariant("OrderHoldFire", ControlContext.NormalSpace)],
            Names = ["fighter hold fire"],
        },

        new()
        {
            Id = "fighter_hold_position",
            Label = "fighter hold position",
            Group = Combat,
            Variants = [new ActionVariant("OrderHoldPosition", ControlContext.NormalSpace)],
            Names = ["fighter hold position"],
        },

        new()
        {
            Id = "fighter_follow",
            Label = "fighter follow",
            Group = Combat,
            Variants = [new ActionVariant("OrderFollow", ControlContext.NormalSpace)],
            Names = ["fighter follow me", "fighter form up"],
        },

        // ---- On foot: suit, tools and weapons ----------------------------------------------
        new()
        {
            Id = "suit_shields",
            Label = "the suit shields",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidToggleShieldsButton", ControlContext.OnFoot)],

            // Status.json has no suit shield state, so "on" and "off" press the toggle.
            Names = ["shields"],
            ExtraPhrases = [("toggle shields", DesiredState.Toggle), ("shields on", DesiredState.On), ("shields off", DesiredState.Off)],
        },

        new()
        {
            Id = "medkit",
            Label = "a medkit",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidHealthPack", ControlContext.OnFoot)],
            Names = ["medkit", "heal"],
            Verbs = ["use a"],
        },

        new()
        {
            Id = "energy_cell",
            Label = "an energy cell",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidBattery", ControlContext.OnFoot)],
            Names = ["energy cell"],
            Verbs = ["use an"],
        },

        new()
        {
            Id = "energylink",
            Label = "the energylink",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSwitchToRechargeTool", ControlContext.OnFoot)],
            Names = ["energylink", "energy link"],
        },

        new()
        {
            Id = "profile_analyser",
            Label = "the profile analyser",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSwitchToCompAnalyser", ControlContext.OnFoot)],
            Names = ["profile analyser", "profile analyzer"],
        },

        new()
        {
            Id = "genetic_sampler",
            Label = "the suit tool",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSwitchToSuitTool", ControlContext.OnFoot)],
            Names = ["suit tool", "genetic sampler", "arc cutter"],
        },

        new()
        {
            Id = "primary_weapon",
            Label = "the primary weapon",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSelectPrimaryWeaponButton", ControlContext.OnFoot)],
            Names = ["primary weapon"],
        },

        new()
        {
            Id = "secondary_weapon",
            Label = "the secondary weapon",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSelectSecondaryWeaponButton", ControlContext.OnFoot)],
            Names = ["secondary weapon"],
        },

        new()
        {
            Id = "utility_weapon",
            Label = "the utility weapon",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSelectUtilityWeaponButton", ControlContext.OnFoot)],
            Names = ["sidearm", "utility weapon"],
        },

        new()
        {
            Id = "holster",
            Label = "the holster",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidHideWeaponButton", ControlContext.OnFoot)],
            Names = ["holster", "holster weapon"],
        },

        new()
        {
            Id = "frag_grenade",
            Label = "the frag grenade",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSelectFragGrenade", ControlContext.OnFoot)],
            Names = ["frag grenade"],
        },

        new()
        {
            Id = "emp_grenade",
            Label = "the EMP grenade",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSelectEMPGrenade", ControlContext.OnFoot)],
            Names = ["emp grenade"],
        },

        new()
        {
            Id = "shield_grenade",
            Label = "the shield projector",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidSelectShieldGrenade", ControlContext.OnFoot)],
            Names = ["shield projector"],
        },

        new()
        {
            Id = "mission_help",
            Label = "the mission help panel",
            Group = OnFoot,
            Variants = [new ActionVariant("HumanoidToggleMissionHelpPanelButton", ControlContext.OnFoot)],
            Names = ["mission help"],
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

    /// <summary>A one-shot with a ship form and, optionally, an SRV and an on-foot twin.</summary>
    private static GameAction Simple(
        string id,
        string label,
        string group,
        string shipAction,
        string? srvAction,
        string name,
        string? verb = null,
        string? onFootAction = null) => new()
    {
        Id = id,
        Label = label,
        Group = group,
        Variants =
        [
            new ActionVariant(shipAction, ControlContext.AnyShip),
            .. srvAction is null ? [] : (ActionVariant[])[new ActionVariant(srvAction, ControlContext.Srv)],
            .. onFootAction is null ? [] : (ActionVariant[])[new ActionVariant(onFootAction, ControlContext.OnFoot)],
        ],
        Names = [name],
        Verbs = verb is null ? [] : [verb],
    };

    /// <summary>One of the six panel-navigation keys.</summary>
    private static GameAction Ui(string id, string word) => new()
    {
        Id = id,
        Label = word,
        Group = Interface,
        Variants = [new ActionVariant($"UI_{char.ToUpperInvariant(word[0])}{word[1..]}", ControlContext.AnyShip | ControlContext.Srv)],
        Shape = PhraseShape.Key,
        Names = [word],
    };
}
