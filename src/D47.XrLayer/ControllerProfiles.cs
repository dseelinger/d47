namespace D47.XrLayer;

/// <summary>The layer's actions. Each is created once with both hands as subaction paths.</summary>
internal enum LayerAction
{
    Aim,
    Grip,
    Trigger,
    Squeeze,
    Primary,
    Secondary,
    Menu,
    StickClick,
    Stick,
}

/// <summary>An interaction profile the layer suggests bindings for, and the input path each of its actions reads.</summary>
internal sealed record ControllerProfile(string Path, (LayerAction Action, string Binding)[] Bindings);

internal static class ControllerProfiles
{
    /// <summary>Each action's name, localized name and XrActionType, indexed by <see cref="LayerAction"/>.</summary>
    public static readonly (string Name, string Localized, int Type)[] Actions =
    [
        ("aim", "Aim", XrConstant.ActionTypePoseInput),
        ("grip", "Grip", XrConstant.ActionTypePoseInput),
        ("trigger", "Trigger", XrConstant.ActionTypeFloatInput),
        ("squeeze", "Squeeze", XrConstant.ActionTypeFloatInput),
        ("primary", "Primary", XrConstant.ActionTypeBooleanInput),
        ("secondary", "Secondary", XrConstant.ActionTypeBooleanInput),
        ("menu", "Menu", XrConstant.ActionTypeBooleanInput),
        ("stick_click", "Stick click", XrConstant.ActionTypeBooleanInput),
        ("stick", "Stick", XrConstant.ActionTypeVector2fInput),
    ];

    /// <summary>The boolean actions, each with the bit it sets in <see cref="XrHand.Buttons"/>.</summary>
    public static readonly (LayerAction Action, XrButtons Bit)[] Buttons =
    [
        (LayerAction.Primary, XrButtons.Primary),
        (LayerAction.Secondary, XrButtons.Secondary),
        (LayerAction.Menu, XrButtons.Menu),
        (LayerAction.StickClick, XrButtons.StickClick),
    ];

    public static readonly ControllerProfile[] All =
    [
        new("/interaction_profiles/oculus/touch_controller",
        [
            (LayerAction.Aim, "/user/hand/left/input/aim/pose"),
            (LayerAction.Aim, "/user/hand/right/input/aim/pose"),
            (LayerAction.Grip, "/user/hand/left/input/grip/pose"),
            (LayerAction.Grip, "/user/hand/right/input/grip/pose"),
            (LayerAction.Trigger, "/user/hand/left/input/trigger/value"),
            (LayerAction.Trigger, "/user/hand/right/input/trigger/value"),
            (LayerAction.Squeeze, "/user/hand/left/input/squeeze/value"),
            (LayerAction.Squeeze, "/user/hand/right/input/squeeze/value"),
            (LayerAction.Primary, "/user/hand/right/input/a/click"),
            (LayerAction.Primary, "/user/hand/left/input/x/click"),
            (LayerAction.Secondary, "/user/hand/right/input/b/click"),
            (LayerAction.Secondary, "/user/hand/left/input/y/click"),
            (LayerAction.Menu, "/user/hand/left/input/menu/click"),
            (LayerAction.StickClick, "/user/hand/left/input/thumbstick/click"),
            (LayerAction.StickClick, "/user/hand/right/input/thumbstick/click"),
            (LayerAction.Stick, "/user/hand/left/input/thumbstick"),
            (LayerAction.Stick, "/user/hand/right/input/thumbstick"),
        ]),
    ];
}
