using D47.Core.Configuration;
using D47.Core.Vr;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The headset (list.md Phase 9). What it declares is a switch and a state — where the
/// surfaces go belongs to the placement rows, and whether a headset is <em>present</em>
/// belongs to nobody, because that is something d47 discovers and says rather than something
/// the Commander sets.
/// </summary>
public static class VrCapability
{
    public const string Id = "vr";

    public const string EnabledKey = "vr.enabled";

    public const string StateKey = "vr.state";

    /// <summary>
    /// What the app can tell this capability about the live session. A delegate rather than a
    /// reference, for the same reason every other capability surface is one: Core cannot see
    /// the runtime, and a capability that could would be a capability the replay harness
    /// cannot construct.
    /// </summary>
    public sealed record HeadsetSurface
    {
        public required Func<(VrState State, string? Reason, string? Adapter)> Report { get; init; }
    }

    public static CapabilityDescriptor Create(SettingsService settings, HeadsetSurface headset) => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Headset",
        Summary = "Show d47 in the headset as a SteamVR overlay, over Elite, in your own cockpit.",
        Examples = ["is the headset connected", "turn the headset overlay off"],
        Keywords = ["headset status", "vr status", "is the headset connected"],
        Display = new CapabilityDisplay { PanelTitle = "Headset", Order = 45 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_headset_status",
                Description =
                    "Report whether d47 is showing in the headset, and if not, why not.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(settings, headset))),
            },
        ],
        Settings =
        [
            new SettingRow
            {
                Key = EnabledKey,
                Label = "Show d47 in the headset",
                Help = "Off leaves SteamVR alone entirely. On costs nothing on a machine with no headset — "
                       + "d47 looks for one, does not find one, and says so.",
                Kind = SettingKind.Toggle,
                DocsAnchor = "enabled",
                Binding = new SettingBinding
                {
                    Read = s => s.Vr.Enabled ? "true" : "false",
                    Write = (s, v) => s with { Vr = s.Vr with { Enabled = v == "true" } },
                },
                Commands =
                [
                    new SettingCommandPhrase("headset overlay on", "true"),
                    new SettingCommandPhrase("headset overlay off", "false"),
                ],
            },
            new SettingRow
            {
                Key = StateKey,
                Label = "Headset",
                Help = "What d47 can currently see. Not a setting — a state, reported where the switch is, "
                       + "because \"it is off\" and \"SteamVR is not running\" look identical from the outside.",
                Kind = SettingKind.Info,
                DocsAnchor = "state",
                Binding = new SettingBinding { Read = _ => Describe(settings, headset) },
            },
        ],
    };

    private static string Describe(SettingsService settings, HeadsetSurface headset)
    {
        if (!settings.Current.Vr.Enabled)
        {
            return "The headset overlays are switched off.";
        }

        var (state, reason, adapter) = headset.Report();

        return state switch
        {
            VrState.Active => adapter is null
                ? "Showing in the headset."
                : $"Showing in the headset, rendering on {adapter}.",
            VrState.Connecting => reason ?? "Looking for a headset.",
            _ => reason ?? "No SteamVR runtime is installed on this machine.",
        };
    }
}
