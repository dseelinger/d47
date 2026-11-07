using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Input;

namespace D47.Core.Tests;

/// <summary>Every tool list a turn can ship, built from the members the app calls.</summary>
internal static class ToolProfiles
{
    private static readonly ControlContext[] Contexts =
    [
        ControlContext.None,
        ControlContext.Docked,
        ControlContext.Landed,
        ControlContext.NormalSpace,
        ControlContext.Supercruise,
        ControlContext.Hyperspace,
        ControlContext.Srv,
        ControlContext.OnFoot,
        ControlContext.Fighter,
    ];

    public static IEnumerable<ToolProfile> All(CapabilityRegistry registry)
    {
        foreach (var context in Contexts)
        {
            yield return ToolSurface.ForMode(registry, context, actionsEnabled: true);
            yield return ToolSurface.ForMode(registry, context, actionsEnabled: false);
            yield return ToolSurface.Compact(registry, context, actionsEnabled: true);
            yield return ToolSurface.Compact(registry, context, actionsEnabled: false);
        }

        yield return ToolSurface.Searchable(registry);
    }
}
