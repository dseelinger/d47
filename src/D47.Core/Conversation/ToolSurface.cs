using D47.Core.Capabilities;
using D47.Core.Input;

namespace D47.Core.Conversation;

/// <summary>One named tool list, in a fixed order.</summary>
public sealed record ToolProfile(string Id, IReadOnlyList<ToolAdvertisement> Tools);

/// <summary>The registry's tools as a turn advertises them: one mode's list, or every tool for tool search.</summary>
public static class ToolSurface
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

    /// <summary>The tools for one mode and key-press setting, none deferred.</summary>
    public static ToolProfile ForMode(CapabilityRegistry registry, ControlContext context, bool actionsEnabled) =>
        new(
            Name(context, actionsEnabled),
            Advertise(registry, capabilityId => Includes(capabilityId, context, actionsEnabled), deferring: false));

    /// <summary>
    /// Every non-protected tool whatever the mode or key-press setting, each deferred unless it is
    /// <see cref="ToolDefinition.AlwaysLoaded"/>. The same on every turn.
    /// </summary>
    public static ToolProfile Searchable(CapabilityRegistry registry) =>
        new("searchable", Advertise(registry, _ => true, deferring: true));

    /// <summary>Every tool list that can ever ship: each mode's with key presses on and off, and the searchable one.</summary>
    public static IEnumerable<ToolProfile> All(CapabilityRegistry registry)
    {
        foreach (var context in Contexts)
        {
            yield return ForMode(registry, context, actionsEnabled: true);
            yield return ForMode(registry, context, actionsEnabled: false);
        }

        yield return Searchable(registry);
    }

    private static List<ToolAdvertisement> Advertise(
        CapabilityRegistry registry,
        Func<string, bool> includes,
        bool deferring)
    {
        var tools = new List<ToolAdvertisement>();

        // Registration order, which is stable, so the same list serialises identically every time it ships.
        foreach (var capability in registry.All)
        {
            if (!includes(capability.Descriptor.Id))
            {
                continue;
            }

            foreach (var tool in capability.Descriptor.Tools)
            {
                // A protected tool is never advertised.
                if (tool.Protected)
                {
                    continue;
                }

                tools.Add(new ToolAdvertisement(
                    tool.Name,
                    tool.Description,
                    capability.ToolSchemas[tool.Name],
                    Deferred: deferring && !tool.AlwaysLoaded));
            }
        }

        return tools;
    }

    private static bool Includes(string capabilityId, ControlContext context, bool actionsEnabled)
    {
        if (capabilityId is not ("flight-controls" or "ship-systems" or "panels" or "srv" or "macros"))
        {
            return true;
        }

        if (!actionsEnabled)
        {
            return false;
        }

        // Macros span every group, so they ship wherever anything does.
        if (capabilityId == "macros")
        {
            return context != ControlContext.None && context != ControlContext.Hyperspace;
        }

        var group = capabilityId switch
        {
            "flight-controls" => GameActions.Flight,
            "ship-systems" => GameActions.Systems,
            "panels" => GameActions.Interface,
            "srv" => GameActions.SrvGroup,
            _ => null,
        };

        return group is not null && GameActions.All
            .Where(action => action.Group == group)
            .Any(action => action.For(context) is not null);
    }

    private static string Name(ControlContext context, bool actionsEnabled) =>
        !actionsEnabled ? "no-actions"
        : context switch
        {
            ControlContext.OnFoot => "on-foot",
            ControlContext.Srv => "srv",
            ControlContext.Supercruise => "supercruise",
            ControlContext.NormalSpace => "normal-space",
            ControlContext.Docked => "docked",
            ControlContext.Landed => "landed",
            ControlContext.Fighter => "fighter",
            ControlContext.Hyperspace => "hyperspace",
            _ => "no-game",
        };
}
