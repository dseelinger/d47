namespace D47.XrLayer.Tests;

/// <summary>The action calls the layer makes, in order, as <see cref="FakeNext.ActionCalls"/> records them.</summary>
internal static class ControllerCalls
{
    public const ulong ElitesSet = 0xE117E;

    /// <summary>Two hand paths, the set, nine actions, the profile and seventeen binding paths, then one suggestion.</summary>
    public static readonly string[] Prepare =
    [
        .. Repeat(nameof(FakeNext.StringToPath), 2),
        nameof(FakeNext.CreateActionSet),
        .. Repeat(nameof(FakeNext.CreateAction), 9),
        .. Repeat(nameof(FakeNext.StringToPath), 18),
        nameof(FakeNext.SuggestInteractionProfileBindings),
    ];

    /// <summary>An aim and a grip space for each hand.</summary>
    public static readonly string[] Spaces = Repeat(nameof(FakeNext.CreateActionSpace), 4);

    /// <summary>For each hand, left first: the aim and grip poses, the four buttons, the stick, then the trigger and grip values.</summary>
    public static readonly string[] Reads = [.. Hand(), .. Hand()];

    public static readonly string[] Bindings =
    [
        "/user/hand/left/input/aim/pose",
        "/user/hand/right/input/aim/pose",
        "/user/hand/left/input/grip/pose",
        "/user/hand/right/input/grip/pose",
        "/user/hand/left/input/trigger/value",
        "/user/hand/right/input/trigger/value",
        "/user/hand/left/input/squeeze/value",
        "/user/hand/right/input/squeeze/value",
        "/user/hand/right/input/a/click",
        "/user/hand/left/input/x/click",
        "/user/hand/right/input/b/click",
        "/user/hand/left/input/y/click",
        "/user/hand/left/input/menu/click",
        "/user/hand/left/input/thumbstick/click",
        "/user/hand/right/input/thumbstick/click",
        "/user/hand/left/input/thumbstick",
        "/user/hand/right/input/thumbstick",
    ];

    private static string[] Hand() =>
    [
        FakeNext.LocateHand, FakeNext.LocateHand,
        .. Repeat(nameof(FakeNext.GetActionStateBoolean), 4),
        nameof(FakeNext.GetActionStateVector2f),
        nameof(FakeNext.GetActionStateFloat), nameof(FakeNext.GetActionStateFloat),
    ];

    private static string[] Repeat(string call, int count) => Enumerable.Repeat(call, count).ToArray();
}
