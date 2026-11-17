namespace D47.XrLayer.Tests;

/// <summary>The action calls the layer makes, in order, as <see cref="FakeNext.ActionCalls"/> records them.</summary>
internal static class ControllerCalls
{
    public const ulong ElitesSet = 0xE117E;

    /// <summary>Two hand paths, the set, four actions, the profile and eight binding paths, then one suggestion.</summary>
    public static readonly string[] Prepare =
    [
        .. Repeat(nameof(FakeNext.StringToPath), 2),
        nameof(FakeNext.CreateActionSet),
        .. Repeat(nameof(FakeNext.CreateAction), 4),
        .. Repeat(nameof(FakeNext.StringToPath), 9),
        nameof(FakeNext.SuggestInteractionProfileBindings),
    ];

    /// <summary>An aim and a grip space for each hand.</summary>
    public static readonly string[] Spaces = Repeat(nameof(FakeNext.CreateActionSpace), 4);

    /// <summary>For each hand, left first: the aim and grip poses, then the trigger and grip values.</summary>
    public static readonly string[] Reads =
    [
        FakeNext.LocateHand, FakeNext.LocateHand, nameof(FakeNext.GetActionStateFloat), nameof(FakeNext.GetActionStateFloat),
        FakeNext.LocateHand, FakeNext.LocateHand, nameof(FakeNext.GetActionStateFloat), nameof(FakeNext.GetActionStateFloat),
    ];

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
    ];

    private static string[] Repeat(string call, int count) => Enumerable.Repeat(call, count).ToArray();
}
