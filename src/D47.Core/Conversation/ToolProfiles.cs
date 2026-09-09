using D47.Core.Capabilities;
using D47.Core.Input;

namespace D47.Core.Conversation;

/// <summary>
/// One pre-declared tool set (Phase 10, "Decide which tools ship on a turn as the count grows").
/// </summary>
/// <param name="Id">Stable name.</param>
/// <param name="Tools">The advertisement, in a fixed order.</param>
public sealed record ToolProfile(string Id, IReadOnlyList<ToolAdvertisement> Tools)
{
    /// <summary>Roughly what this profile costs to ship, for deciding whether to degrade.</summary>
    public int Bytes => Tools.Sum(tool => tool.Name.Length + tool.Description.Length + tool.InputSchemaJson.Length);
}

/// <summary>Choosing the tool set for a turn — between profiles, never between individual tools.</summary>
public static class ToolProfiles
{
    /// <summary>
    /// The point past which a profile is considered too big to ship comfortably, in characters of
    /// advertised schema.
    /// </summary>
    public const int ComfortableBytes = 50_000;

    /// <summary>The tools that ship in every profile, including the degraded one.</summary>
    private static bool IsAlwaysOffered(string capabilityId) =>
        capabilityId is not ("flight-controls" or "ship-systems" or "panels" or "srv" or "macros");

    /// <summary>The profile a context wants, before the relief valve is considered.</summary>
    public static ToolProfile Full(CapabilityRegistry registry, ControlContext context) =>
        Build(registry, context, actionsEnabled: true, degraded: false);

    /// <summary>The profile for a turn.</summary>
    /// <param name="registry">The registered capabilities.</param>
    /// <param name="context">The mode the Commander is in.</param>
    /// <param name="actionsEnabled">
    /// Whether the Commander has allowed key presses at all.
    /// </param>
    public static ToolProfile For(CapabilityRegistry registry, ControlContext context, bool actionsEnabled)
    {
        var full = Build(registry, context, actionsEnabled, degraded: false);

        // Degrading is the second choice and is stated as such.
        return full.Bytes <= ComfortableBytes
            ? full
            : Build(registry, context, actionsEnabled, degraded: true);
    }

    /// <summary>Every profile that can ever ship.</summary>
    public static IEnumerable<ToolProfile> All(CapabilityRegistry registry)
    {
        ControlContext[] contexts =
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

        foreach (var context in contexts)
        {
            yield return Build(registry, context, actionsEnabled: true, degraded: false);
            yield return Build(registry, context, actionsEnabled: true, degraded: true);
        }

        yield return Build(registry, ControlContext.NormalSpace, actionsEnabled: false, degraded: false);
    }

    private static ToolProfile Build(
        CapabilityRegistry registry,
        ControlContext context,
        bool actionsEnabled,
        bool degraded)
    {
        var tools = new List<ToolAdvertisement>();

        // Registration order, which is stable, so the same profile serialises identically every time it
        // ships.
        foreach (var capability in registry.All)
        {
            if (!Includes(capability.Descriptor.Id, context, actionsEnabled, degraded))
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
                    capability.ToolSchemas[tool.Name]));
            }
        }

        return new ToolProfile(Name(context, actionsEnabled, degraded), tools);
    }

    private static bool Includes(string capabilityId, ControlContext context, bool actionsEnabled, bool degraded)
    {
        if (IsAlwaysOffered(capabilityId))
        {
            return true;
        }

        if (degraded || !actionsEnabled)
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

    private static string Name(ControlContext context, bool actionsEnabled, bool degraded) =>
        degraded ? "degraded"
        : !actionsEnabled ? "no-actions"
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
