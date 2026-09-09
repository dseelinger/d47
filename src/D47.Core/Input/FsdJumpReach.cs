namespace D47.Core.Input;

/// <summary>How d47 reaches a hyperspace jump when the dedicated bind cannot be pressed (#344).</summary>
public static class FsdJumpReach
{
    /// <summary>The dedicated jump's action id.</summary>
    public const string Hyperspace = "hyperspace";

    /// <summary>The combined FSD key's action id — <c>HyperSuperCombination</c>.</summary>
    public const string Combined = "frame_shift_drive";

    /// <summary>Resolves a jump, falling back to the combined key.</summary>
    /// <param name="binds">The Commander's bindings.</param>
    /// <param name="context">The mode they are in.</param>
    /// <param name="systemTargeted">Whether there is a system to jump to.</param>
    public static ActionReach Resolve(EliteBinds binds, ControlContext context, bool systemTargeted)
    {
        var jump = GameActions.Find(Hyperspace)!;
        var direct = ActionReachability.Resolve(jump, binds, context);

        // Only a binding problem is worth a second look.
        if (!IsBindingProblem(direct))
        {
            return direct;
        }

        var combined = GameActions.Find(Combined)!;
        var fallback = ActionReachability.Resolve(combined, binds, context);

        if (!fallback.IsOffered)
        {
            return IsBindingProblem(fallback)
                ? Deny(
                    jump,
                    fallback.Availability,
                    $"The hyperspace jump {Why(direct, binds, context)} and the frame shift drive key "
                    + $"{Why(fallback, binds, context)}, so I have no way to jump. "
                    + "Bind either to a key and I can.")
                : direct;
        }

        if (!systemTargeted)
        {
            return Deny(
                jump,
                ActionAvailability.NotBound,
                "Nothing is targeted, so the frame shift key would drop us to supercruise instead. "
                + "Set a course and I can jump.");
        }

        // The combined key, offered as the jump that was asked for.
        return new ActionReach
        {
            Action = jump,
            Availability = ActionAvailability.Offered,
            Reason = string.Empty,
            Binding = fallback.Binding,
        };
    }

    /// <summary>
    /// Whether a refusal is about the bindings rather than about the binds file or the mode — the only
    /// kind a second action can answer.
    /// </summary>
    private static bool IsBindingProblem(ActionReach reach) =>
        reach.Availability is ActionAvailability.NotBound or ActionAvailability.OnAnotherDevice;

    /// <summary>The half-sentence for one unreachable action.</summary>
    private static string Why(ActionReach reach, EliteBinds binds, ControlContext context) =>
        reach.Availability == ActionAvailability.OnAnotherDevice &&
        reach.Action.For(context) is { } variant &&
        binds.For(variant.EliteAction) is { Count: > 0 } slots
            ? $"is on {EliteKeys.DeviceName(slots[0].Key)}"
            : "is not bound";

    private static ActionReach Deny(GameAction action, ActionAvailability availability, string reason) =>
        new() { Action = action, Availability = availability, Reason = reason };
}
