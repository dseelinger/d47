using D47.Core.Capabilities;
using D47.Core.Input;

namespace D47.Core.Conversation;

/// <summary>
/// One pre-declared tool set (list.md Phase 10, "Decide which tools ship on a turn as the count
/// grows").
/// </summary>
/// <param name="Id">Stable name. It is what a cache entry is keyed by in practice.</param>
/// <param name="Tools">The advertisement, in a fixed order.</param>
public sealed record ToolProfile(string Id, IReadOnlyList<ToolAdvertisement> Tools)
{
    /// <summary>Roughly what this profile costs to ship, for deciding whether to degrade.</summary>
    public int Bytes => Tools.Sum(tool => tool.Name.Length + tool.Description.Length + tool.InputSchemaJson.Length);
}

/// <summary>
/// Choosing the tool set for a turn — <b>between profiles, never between individual tools</b>.
/// <para>
/// Tool definitions serialize first, so any per-turn change to the set invalidates the
/// <em>entire</em> cached prefix rather than a tail of it (architecture.md §6). Two pressures
/// both want to mutate the set per turn: mode-gated advertisement, and running out of room.
/// Letting either pick tools individually would produce a different prefix almost every turn,
/// destroying caching on exactly the turn caching starts to matter.
/// </para>
/// <para>
/// The resolution is to quantize. There is a closed enumeration of profiles, each byte-identical
/// every time it ships, so a Commander who stays in supercruise for twenty minutes pays for one
/// cache entry and reads it twenty times. Crossing into normal space costs one miss, once.
/// </para>
/// <para>
/// <b>The guardrails are not in here and cannot be.</b> They are prompt position 2, static text
/// on <see cref="PromptAssembly"/> with no setter — so no budget setting, effort setting or
/// profile choice reaches them. Degrading under pressure drops tools; it can never drop the
/// block that says what d47 must not invent.
/// </para>
/// </summary>
public static class ToolProfiles
{
    /// <summary>
    /// The point past which a profile is considered too big to ship comfortably, in characters
    /// of advertised schema. Deliberately generous: degrading is a real cost — the Commander
    /// loses the ability to act on their ship — so it is a relief valve rather than a tuning
    /// knob, and it should not open on an ordinary turn.
    /// <para>
    /// <b>Raised from 24,000 on 2026-08-15, after measuring what it was protecting.</b> A real
    /// request was captured off the wire — d47 pointed at a local endpoint, so the body is the
    /// one the provider actually builds — and it carries <b>42 tools in 21,914 characters</b>
    /// beside a 5,327-character system block, with the cache breakpoint on the system block and
    /// therefore covering both. That puts the advertised block at roughly 6,000 to 7,000 tokens,
    /// <em>inside the cached prefix</em>: about a third of a penny a turn at Claude Opus 5's
    /// cache-read rate, against 4.4 cents once per profile when it is written cold.
    /// </para>
    /// <para>
    /// So the old number was not approximating a provider limit — there is none published for
    /// the Messages API, and Anthropic's own guidance is qualitative: keep the set focused, and
    /// past a few dozen tools prefer deferred loading to advertising every schema. 24,000 was a
    /// proxy for "the surface has grown enough to think about", and it began firing on ordinary
    /// turns, which is precisely what a relief valve must not do.
    /// </para>
    /// <para>
    /// <b>Raised again to 40,000 on 2026-08-16, and this time the valve had actually opened.</b>
    /// Phase 18 added three knowledge capabilities in a day, and the SRV profile — the largest,
    /// because it carries the SRV's controls on top of everything else — landed at <b>32,539</b>
    /// bytes against a 32,000 ceiling. It degraded, which took the SRV's own controls away from a
    /// Commander driving one, and it was caught by a test asserting those controls ship rather than
    /// by anything watching the size. Every other profile was under: docked, landed, normal space
    /// and supercruise at 31,827, on foot at 30,111, no-game at 28,930. So the surface had grown
    /// 1.7% past a threshold and lost a whole capability group for it, which is precisely the
    /// "relief valve, not a tuning knob" failure the paragraph above describes. 40,000 restores the
    /// headroom the 32,000 was meant to provide, and <see cref="Conversation"/>'s size test now
    /// names the cause directly instead of leaving it to be inferred from a missing tool.
    /// </para>
    /// <para>
    /// <b>The next move is not another number, and it is now available.</b> Mid-conversation tool
    /// changes (<c>defer_loading</c> plus <c>tool_addition</c>/<c>tool_removal</c>, Claude Opus 5
    /// onward) let the tool set vary per mode <em>without</em> invalidating the cached prefix —
    /// which is the exact conflict the profile enumeration exists to work around (architecture.md
    /// §6). That model is what d47 runs on today, so this constant and the profiles beneath it are
    /// removable work rather than a someday. **Raising the number a third time is the wrong answer.**
    /// </para>
    /// </summary>
    public const int ComfortableBytes = 40_000;

    /// <summary>
    /// The tools that ship in every profile, including the degraded one. Answering, help,
    /// settings and diagnostics are what d47 is when it cannot act — and a degraded profile
    /// that could not answer "what can you do" would be one nobody could diagnose.
    /// </summary>
    private static bool IsAlwaysOffered(string capabilityId) =>
        capabilityId is not ("flight-controls" or "ship-systems" or "panels" or "srv" or "macros");

    /// <summary>
    /// The profile a context <em>wants</em>, before the relief valve is considered.
    /// <para>
    /// <b>Exists so a guard can measure the thing that matters.</b> <see cref="For"/> hands back
    /// the degraded profile once the full one does not fit, and a degraded profile is under the
    /// ceiling by construction, so a test that asks the returned profile its size is asking after
    /// the answer has been hidden. Actions are on, because a profile with no action tools is not
    /// the one that runs out of room.
    /// </para>
    /// </summary>
    public static ToolProfile Full(CapabilityRegistry registry, ControlContext context) =>
        Build(registry, context, actionsEnabled: true, degraded: false);

    /// <summary>
    /// The profile for a turn.
    /// <para>
    /// The mode half is a projection of the action catalogue rather than a hand-written table:
    /// a group's tool ships when at least one of its actions has a variant valid in the current
    /// mode. That keeps the profiles honest as the catalogue grows, and it is still a closed
    /// enumeration because <see cref="ControlContext"/> is one.
    /// </para>
    /// </summary>
    /// <param name="registry">The registered capabilities. Immutable, so this is cheap.</param>
    /// <param name="context">The mode the Commander is in.</param>
    /// <param name="actionsEnabled">
    /// Whether the Commander has allowed key presses at all. When they have not, no action tool
    /// ships in any mode — advertising a tool that will refuse every call is paying for a
    /// refusal on every turn.
    /// </param>
    public static ToolProfile For(CapabilityRegistry registry, ControlContext context, bool actionsEnabled)
    {
        var full = Build(registry, context, actionsEnabled, degraded: false);

        // Degrading is the second choice and is stated as such. It is reached only when the
        // full profile for this mode does not fit, which the checklist frames as a thing that
        // happens as the capability count grows rather than as a routine event.
        return full.Bytes <= ComfortableBytes
            ? full
            : Build(registry, context, actionsEnabled, degraded: true);
    }

    /// <summary>Every profile that can ever ship. Exists so a test can pin all of them at once.</summary>
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

        // Registration order, which is stable, so the same profile serialises identically every
        // time it ships. This is the whole property the quantization exists to protect.
        foreach (var capability in registry.All)
        {
            if (!Includes(capability.Descriptor.Id, context, actionsEnabled, degraded))
            {
                continue;
            }

            foreach (var tool in capability.Descriptor.Tools)
            {
                // A protected tool is never advertised. Not caution — arithmetic: it would refuse
                // every call it received, and a refusal advertised in the cached prefix is paid
                // for on every turn of the session (architecture.md §6). The registry refuses it
                // again at the call, which is where the guarantee actually lives.
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
