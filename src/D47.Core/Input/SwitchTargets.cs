using D47.Core.Journal;

namespace D47.Core.Input;

/// <summary>One action a switch target sets, and the state it is set to.</summary>
public sealed record TargetPart(string Action, DesiredState State);

/// <summary>A named state made of two or more action states, which one switch position can mean.</summary>
/// <param name="Id">The id a switch file stores.</param>
/// <param name="Name">How the picker names it.</param>
/// <param name="Parts">The states to set, in the order they are pressed.</param>
public sealed record SwitchTarget(string Id, string Name, IReadOnlyList<TargetPart> Parts)
{
    /// <summary>The modes in which any part of this target applies.</summary>
    public ControlContext Contexts =>
        Parts
            .Select(part => GameActions.Find(part.Action))
            .Where(action => action is not null)
            .Aggregate(ControlContext.None, (all, action) => all | action!.Contexts);
}

/// <summary>The closed list of switch targets.</summary>
public static class SwitchTargets
{
    public static IReadOnlyList<SwitchTarget> All { get; } =
    [
        new("lights_only", "Lights on, night vision off",
            [new("lights", DesiredState.On), new("night_vision", DesiredState.Off)]),
        new("night_vision_only", "Night vision on, lights off",
            [new("night_vision", DesiredState.On), new("lights", DesiredState.Off)]),
        new("lights_and_night_vision_off", "Lights and night vision off",
            [new("lights", DesiredState.Off), new("night_vision", DesiredState.Off)]),
        new("combat_hardpoints_out", "Combat mode, hardpoints out",
            [new("analysis_mode", DesiredState.Off), new("hardpoints", DesiredState.On)]),
        new("combat_hardpoints_in", "Combat mode, hardpoints stowed",
            [new("analysis_mode", DesiredState.Off), new("hardpoints", DesiredState.Off)]),
    ];

    public static SwitchTarget? Find(string? id) =>
        All.FirstOrDefault(target => string.Equals(target.Id, id, StringComparison.Ordinal));
}

/// <summary>A target met when Status.json's GuiFocus reads a value.</summary>
/// <param name="Id">The id a switch file stores.</param>
/// <param name="Name">How the picker names it.</param>
/// <param name="Focus">The focus the target means.</param>
/// <param name="OpenAction">The action that opens it from the cockpit, or null for the cockpit itself.</param>
public sealed record FocusTarget(string Id, string Name, GuiFocus Focus, string? OpenAction)
{
    /// <summary>The modes in which the target's open key applies; those of all four for the cockpit.</summary>
    public ControlContext Contexts =>
        (OpenAction is null ? FocusTargets.All.Where(target => target.OpenAction is not null) : [this])
            .Select(target => GameActions.Find(target.OpenAction!)?.Contexts ?? ControlContext.None)
            .Aggregate(ControlContext.None, (all, contexts) => all | contexts);
}

/// <summary>The closed list of focus targets.</summary>
public static class FocusTargets
{
    public static IReadOnlyList<FocusTarget> All { get; } =
    [
        new("cockpit", "Cockpit, nothing open", GuiFocus.None, null),
        new("galaxy_map_open", "Galaxy map", GuiFocus.GalaxyMap, "galaxy_map"),
        new("system_map_open", "System map", GuiFocus.SystemMap, "system_map"),
        new("left_panel_open", "Left panel", GuiFocus.ExternalPanel, "left_panel"),
        new("right_panel_open", "Right panel", GuiFocus.InternalPanel, "right_panel"),
    ];

    public static FocusTarget? Find(string? id) =>
        All.FirstOrDefault(target => string.Equals(target.Id, id, StringComparison.Ordinal));

    /// <summary>The target for a focus, or null for a focus no target names.</summary>
    public static FocusTarget? Of(GuiFocus focus) => All.FirstOrDefault(target => target.Focus == focus);

    /// <summary>The interface named mid-sentence: "the galaxy map".</summary>
    public static string Say(GuiFocus focus) => focus switch
    {
        GuiFocus.None => "the cockpit",
        GuiFocus.InternalPanel => "the right panel",
        GuiFocus.ExternalPanel => "the left panel",
        GuiFocus.CommsPanel => "the comms panel",
        GuiFocus.RolePanel => "the role panel",
        GuiFocus.StationServices => "the station services screen",
        GuiFocus.GalaxyMap => "the galaxy map",
        GuiFocus.SystemMap => "the system map",
        GuiFocus.Orrery => "the orrery",
        GuiFocus.FssMode => "the full spectrum scanner",
        GuiFocus.SaaMode => "the surface scanner",
        GuiFocus.Codex => "the codex",
        _ => "a full-screen view",
    };
}

/// <summary>A target met when the power distributor reads a split.</summary>
/// <param name="Id">The id a switch file stores.</param>
/// <param name="Name">How the picker names it.</param>
/// <param name="Pips">The split the target means.</param>
/// <param name="Presses">The power keys to press after a reset to balanced, in order.</param>
public sealed record PipTarget(string Id, string Name, PowerDistribution Pips, IReadOnlyList<string> Presses)
{
    /// <summary>The modes in which the distributor keys apply.</summary>
    public ControlContext Contexts =>
        GameActions.Find(PipTargets.Reset)?.Contexts ?? ControlContext.None;
}

/// <summary>The closed list of power distributor targets.</summary>
public static class PipTargets
{
    /// <summary>The action that returns the distributor to balanced, which every sequence starts from.</summary>
    public const string Reset = "balance_power";

    private const string S = "power_to_systems";
    private const string E = "power_to_engines";
    private const string W = "power_to_weapons";

    public static IReadOnlyList<PipTarget> All { get; } =
    [
        new("pips_balanced", "Balanced", new(4, 4, 4), []),
        new("pips_engines", "4 pips to engines", new(2, 8, 2), [E, E]),
        new("pips_weapons", "4 pips to weapons", new(2, 2, 8), [W, W]),
        new("pips_systems", "4 pips to systems", new(8, 2, 2), [S, S]),
        new("pips_systems_engines", "4 systems, 2 engines", new(8, 4, 0), [S, S, E, E, S]),
        new("pips_systems_weapons", "4 systems, 2 weapons", new(8, 0, 4), [S, S, W, W, S]),
        new("pips_engines_systems", "4 engines, 2 systems", new(4, 8, 0), [S, S, E, E, E]),
        new("pips_engines_weapons", "4 engines, 2 weapons", new(0, 8, 4), [E, E, W, W, E]),
        new("pips_weapons_systems", "4 weapons, 2 systems", new(4, 0, 8), [S, S, W, W, W]),
        new("pips_weapons_engines", "4 weapons, 2 engines", new(0, 4, 8), [E, E, W, W, W]),
    ];

    public static PipTarget? Find(string? id) =>
        All.FirstOrDefault(target => string.Equals(target.Id, id, StringComparison.Ordinal));
}
