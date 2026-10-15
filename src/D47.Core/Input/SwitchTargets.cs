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
