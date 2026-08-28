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
/// One action resolved against the Commander's own bindings and the mode they are in
/// (Phase 10, "Know which actions the Commander can actually reach" and "Offer only
/// the actions that work right now").
/// <para>
/// The point of carrying a <see cref="Reason"/> even when the answer is no: the audience is
/// HOTAS-heavy, so an action bound only to a stick is the common case rather than the odd one,
/// and it fails as silence unless something says why. A refusal that names the device is
/// actionable — the Commander can bind a key — where silence is a bug report.
/// </para>
/// </summary>
public sealed record ActionReach
{
    public required GameAction Action { get; init; }

    public required ActionAvailability Availability { get; init; }

    /// <summary>A sentence to say back. Empty when the action is on offer.</summary>
    public required string Reason { get; init; }

    /// <summary>The binding to press. Null unless <see cref="IsOffered"/>.</summary>
    public EliteBinding? Binding { get; init; }

    public bool IsOffered => Availability == ActionAvailability.Offered;
}

/// <summary>
/// Turns the parsed bindings and the live mode into the set of actions d47 will admit to.
/// <para>
/// This is the one place that decides what the model is told about. Two rules, and the second
/// is the one that is easy to lose: an action that is unreachable is not advertised
/// <em>and</em> is still answerable, so asking for it produces a reason rather than a tool
/// that does not exist.
/// </para>
/// </summary>
public static class ActionReachability
{
    /// <summary>
    /// Picks the slot to press. Any pressable slot beats an unpressable one regardless of
    /// which came first: a Commander whose stick holds Primary and whose keyboard holds
    /// Secondary is the ordinary HOTAS setup, and preferring Primary would refuse an action
    /// that is bound to a key right there in the same file.
    /// </summary>
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

        // Mode before bindings. Both can be wrong at once, and "that does nothing in
        // supercruise" is the more useful half of the answer — it tells the Commander to drop
        // out rather than to go and edit their controls.
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
            // Named rather than generic. "On your joystick" tells the Commander both why it
            // failed and exactly what to change; "unavailable" tells them to file a bug.
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

    /// <summary>
    /// Every action, resolved. Callers that advertise filter this to
    /// <see cref="ActionReach.IsOffered"/>; callers that answer a request read the reason.
    /// </summary>
    public static IReadOnlyList<ActionReach> ResolveAll(
        EliteBinds binds,
        ControlContext context,
        IEnumerable<GameAction>? actions = null) =>
        [.. (actions ?? GameActions.All).Select(action => Resolve(action, binds, context))];

    /// <summary>
    /// The ids currently on offer. This is what becomes the tool's closed vocabulary, so an
    /// action the Commander cannot reach is not a value the model can even name.
    /// </summary>
    public static IReadOnlyList<string> OfferedIds(EliteBinds binds, ControlContext context) =>
        [.. ResolveAll(binds, context).Where(reach => reach.IsOffered).Select(reach => reach.Action.Id)];

    private static ActionReach Deny(GameAction action, ActionAvailability availability, string reason) =>
        new() { Action = action, Availability = availability, Reason = reason };

    private static string Capitalise(string label) =>
        label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];
}
