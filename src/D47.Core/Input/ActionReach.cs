namespace D47.Core.Input;

/// <summary>Why an action is or is not on offer this turn.</summary>
public enum ActionAvailability
{
    /// <summary>Bound to something d47 can press, and the current mode is one it works in.</summary>
    Offered,

    /// <summary>d47 has not been able to read the Commander's bindings at all.</summary>
    BindsUnknown,

    /// <summary>The mode is wrong: the key exists and would do nothing right now.</summary>
    WrongMode,

    /// <summary>Elite has this action, and the Commander has left it unbound.</summary>
    NotBound,

    /// <summary>Bound, but to a stick, throttle, pedal or hand controller.</summary>
    OnAnotherDevice,
}

/// <summary>
/// One action resolved against the Commander's own bindings and the mode they are in (Phase 10, "Know
/// which actions the Commander can actually reach" and "Offer only the actions that work right now").
/// </summary>
public sealed record ActionReach
{
    public required GameAction Action { get; init; }

    public required ActionAvailability Availability { get; init; }

    /// <summary>A sentence to say back.</summary>
    public required string Reason { get; init; }

    /// <summary>The binding to press.</summary>
    public EliteBinding? Binding { get; init; }

    public bool IsOffered => Availability == ActionAvailability.Offered;
}

/// <summary>Turns the parsed bindings and the live mode into the set of actions d47 will admit to.</summary>
public static class ActionReachability
{
    /// <summary>Picks the slot to press.</summary>
    public static EliteBinding? Pressable(IEnumerable<EliteBinding> slots) =>
        slots.FirstOrDefault(slot => EliteKeys.Resolve(slot.Key).IsPressable);

    public static ActionReach Resolve(GameAction action, EliteBinds binds, ControlContext context)
    {
        if (!binds.IsKnown)
        {
            return Deny(
                action,
                ActionAvailability.BindsUnknown,
                "I cannot read your control bindings, so I do not know which keys you use.");
        }

        // Mode before bindings.
        if (action.For(context) is not { } variant)
        {
            var where = context == ControlContext.None
                ? "I cannot tell what you are flying"
                : $"you are {ControlContexts.Describe(context)}";

            return Deny(
                action,
                ActionAvailability.WrongMode,
                $"{Capitalise(action.Label)} does nothing while {where}.");
        }

        var slots = binds.For(variant.EliteAction);

        if (slots.Count == 0)
        {
            return Deny(
                action,
                ActionAvailability.NotBound,
                $"You have no binding for {action.Label}, so there is no key for me to press.");
        }

        if (Pressable(slots) is not { } binding)
        {
            // Named rather than generic. "On your joystick" tells the Commander both why it failed and
            // exactly what to change; "unavailable" tells them to file a bug.
            var device = EliteKeys.DeviceName(slots[0].Key);

            return Deny(
                action,
                ActionAvailability.OnAnotherDevice,
                $"{Capitalise(action.Label)} is on {device}, which I have no way to press. "
                + "Bind it to a key or a mouse button and I can.");
        }

        return new ActionReach
        {
            Action = action,
            Availability = ActionAvailability.Offered,
            Reason = string.Empty,
            Binding = binding,
        };
    }

    /// <summary>Every action, resolved.</summary>
    public static IReadOnlyList<ActionReach> ResolveAll(
        EliteBinds binds,
        ControlContext context,
        IEnumerable<GameAction>? actions = null) =>
        [.. (actions ?? GameActions.All).Select(action => Resolve(action, binds, context))];

    /// <summary>The ids currently on offer.</summary>
    public static IReadOnlyList<string> OfferedIds(EliteBinds binds, ControlContext context) =>
        [.. ResolveAll(binds, context).Where(reach => reach.IsOffered).Select(reach => reach.Action.Id)];

    private static ActionReach Deny(GameAction action, ActionAvailability availability, string reason) =>
        new() { Action = action, Availability = availability, Reason = reason };

    private static string Capitalise(string label) =>
        label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];
}
