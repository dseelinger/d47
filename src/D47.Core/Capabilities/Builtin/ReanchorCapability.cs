namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Putting the headset surfaces back (Phase 9, "Re-anchor the panels").
/// <para>
/// Elite's in-game recenter moves the cockpit without telling SteamVR, so a world-locked
/// surface drifts out of position with no event to hook. There is nothing to detect and
/// nothing to correct automatically; what there is, is a Commander who can see that their
/// panels are in the wrong place and wants them back.
/// </para>
/// <para>
/// <b>Its own capability rather than a second tool on the headset one, and the reason is the
/// keyword router.</b> The router reaches a capability's first argument-free tool, so a
/// capability owning both "how is the headset" and "put the panels back" can only be asked one
/// of them without a model in the path. This one has to be reachable with no model, because
/// local-only operation is a supported configuration and a drifted panel is not a thing that
/// should need a provider key to fix.
/// </para>
/// </summary>
public static class ReanchorCapability
{
    public const string Id = "reanchor";

    public static CapabilityDescriptor Create(VrCapability.HeadsetSurface headset) => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Re-anchor",
        Summary = "Snap the world-locked headset panels back in front of you, keeping their layout.",
        Examples = ["re-anchor", "put the panels back", "recenter the panels"],
        Keywords =
        [
            "re-anchor",
            "reanchor",
            "recenter the panels",
            "recentre the panels",
            "put the panels back",
            "put the panel back",
        ],

        // No settings rows and no panel card. Everything about it is either an action or the
        // hotkey that fires it, and the hotkey lives with the other hotkeys.
        Display = new CapabilityDisplay { PanelTitle = "Re-anchor", Order = 46, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = "reanchor_headset_surfaces",
                Description =
                    "Snap every world-locked headset panel back in front of the Commander, keeping their "
                    + "positions relative to each other.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(headset.Reanchor()))),
            },
        ],
    };

    private static string Describe(int moved) => moved switch
    {
        0 => "There are no world-locked surfaces to re-anchor.",
        1 => "Re-anchored one surface.",
        _ => $"Re-anchored {moved} surfaces, keeping their layout.",
    };
}
