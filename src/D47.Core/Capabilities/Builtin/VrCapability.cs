using System.Globalization;
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

    public const string ModeKey = "vr.mode";

    /// <summary>The surface a placement row belongs to, as it appears in the key.</summary>
    public const string PanelSlot = "panel";

    public const string MiniSlot = "mini";

    /// <summary>The lock row's key for a surface. One spelling, so a caller cannot invent another.</summary>
    public static string LockKey(string slot) => $"vr.{slot}.lock";

    public const string CaptionsEnabledKey = "vr.captions.enabled";

    public const string CaptionSizeKey = "vr.captions.size";

    public const string CaptionBackgroundKey = "vr.captions.background";

    public const string CaptionSpeedKey = "vr.captions.speed";

    /// <summary>
    /// The reading speeds offered, in characters per second. The standard's own two, plus one
    /// slower: reading speed is the one thing about a caption that is a property of the reader
    /// rather than of the caption.
    /// </summary>
    private static readonly IReadOnlyList<string> ReadingSpeeds = ["12", "17", "20"];

    /// <summary>
    /// What the app can tell this capability about the live session. A delegate rather than a
    /// reference, for the same reason every other capability surface is one: Core cannot see
    /// the runtime, and a capability that could would be a capability the replay harness
    /// cannot construct.
    /// </summary>
    public sealed record HeadsetSurface
    {
        public required Func<(VrState State, string? Reason, string? Adapter)> Report { get; init; }

        /// <summary>
        /// Snaps every world-locked surface back to the current head pose as a group. Returns
        /// how many moved, so saying "nothing to re-anchor" is a real answer rather than
        /// silence that looks like a failure.
        /// </summary>
        public required Func<int> Reanchor { get; init; }
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
                Key = ModeKey,
                Label = "Panel content",
                Help = "Full shows everything the desktop window does. Mini reduces what is on the panel "
                       + "rather than shrinking it - it is the same panel showing less, not a smaller copy.",
                Kind = SettingKind.Choice,
                Choices = ["full", "mini"],
                DocsAnchor = "mode",
                AppliesWhen = s => s.Vr.Enabled,
                Binding = new SettingBinding
                {
                    Read = s => s.Vr.Mode,
                    Write = (s, v) => s with { Vr = s.Vr with { Mode = v == "mini" ? "mini" : "full" } },
                },
                Commands =
                [
                    new SettingCommandPhrase("mini panel", "mini"),
                    new SettingCommandPhrase("full panel", "full"),
                ],
            },
            .. Placement(PanelSlot, "Panel", s => s.Vr.Panel, (s, v) => s with { Vr = s.Vr with { Panel = v } }),
            .. Placement(MiniSlot, "Mini panel", s => s.Vr.Mini, (s, v) => s with { Vr = s.Vr with { Mini = v } }),
            CaptionRow(
                CaptionsEnabledKey,
                "Captions",
                "Everything d47 says, written under it in the headset. They place themselves, "
                + "they clear themselves, and they cannot be moved - a caption you can drag "
                + "somewhere you will not see it is not a caption.",
                SettingKind.Toggle,
                s => s.Vr.Captions.Enabled ? "true" : "false",
                (s, v) => s with { Vr = s.Vr with { Captions = s.Vr.Captions with { Enabled = v == "true" } } },
                "captions"),
            CaptionRow(
                CaptionSizeKey,
                "Caption size",
                "How large the caption text is drawn. Three sizes rather than a number, because "
                + "a caption is either legible at a glance or it is not.",
                SettingKind.Choice,
                s => s.Vr.Captions.Size.ToString().ToLowerInvariant(),
                (s, v) => s with
                {
                    Vr = s.Vr with
                    {
                        Captions = s.Vr.Captions with
                        {
                            Size = Enum.TryParse<CaptionSize>(v, ignoreCase: true, out var size)
                                ? size
                                : CaptionSize.Medium,
                        },
                    },
                },
                "size",
                choices: [.. Enum.GetNames<CaptionSize>().Select(name => name.ToLowerInvariant())]),
            CaptionRow(
                CaptionBackgroundKey,
                "Caption background",
                "How solid the box behind the text is, from 0.2 to 1. Not fully solid by default: "
                + "a caption sits over a starfield and a station's floodlights, and a box you "
                + "cannot see through is a hole cut in the cockpit.",
                SettingKind.Number,
                s => s.Vr.Captions.BackgroundOpacity.ToString("0.##", CultureInfo.InvariantCulture),
                (s, v) => s with
                {
                    Vr = s.Vr with
                    {
                        Captions = s.Vr.Captions with
                        {
                            BackgroundOpacity = double.TryParse(
                                v, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity)
                                ? opacity
                                : 0.78,
                        },
                    },
                },
                "background",
                step: 0.02),
            CaptionRow(
                CaptionSpeedKey,
                "Reading speed",
                "Characters a second, which decides how long a caption stays up after the voice "
                + "stops. 20 is the standard's adult rate and 17 its children's rate.",
                SettingKind.Choice,
                s => ((int)s.Vr.Captions.CharactersPerSecond).ToString(CultureInfo.InvariantCulture),
                (s, v) => s with
                {
                    Vr = s.Vr with
                    {
                        Captions = s.Vr.Captions with
                        {
                            CharactersPerSecond = double.TryParse(
                                v, NumberStyles.Float, CultureInfo.InvariantCulture, out var cps)
                                ? cps
                                : Caption.AdultReadingSpeed,
                        },
                    },
                },
                "speed",
                choices: ReadingSpeeds),
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

    /// <summary>
    /// The six knobs <em>Overlay Positioning &amp; Look</em> names, plus the lock, for one
    /// surface. Generated rather than written twice: the panel and the mini panel want the same
    /// controls over different values, and two hand-written copies are two things to keep in
    /// step.
    /// <para>
    /// Every one of them maps onto exactly one call into SteamVR, which is what keeps the
    /// settings surface honest about what it is changing.
    /// </para>
    /// </summary>
    private static IEnumerable<SettingRow> Placement(
        string slot,
        string what,
        Func<Configuration.D47Settings, VrSurfaceSettings> read,
        Func<Configuration.D47Settings, VrSurfaceSettings, Configuration.D47Settings> write)
    {
        SettingRow Row(
            string name,
            string label,
            string help,
            SettingKind kind,
            Func<VrSurfaceSettings, string?> get,
            Func<VrSurfaceSettings, string?, VrSurfaceSettings> set,
            IReadOnlyList<string>? choices = null,
            double step = 1) => new()
        {
            Step = step,
            Key = $"vr.{slot}.{name}",
            Label = label,
            Help = help,
            Kind = kind,
            Choices = choices ?? [],
            DocsAnchor = $"{slot}-{name}",
            Group = $"{what} placement",
            GroupHelp = $"Where the {what.ToLowerInvariant()} sits and what it looks like. You can also just "
                        + "reach out and grab it with a controller, which is what the numbers are here for "
                        + "when you would rather not.",
            AppliesWhen = s => s.Vr.Enabled,
            Binding = new SettingBinding
            {
                Read = s => get(read(s)),
                Write = (s, v) => write(s, set(read(s), v)),
            },
        };

        yield return Row(
            "lock",
            $"{what} locking",
            "Head-locked follows you and is always in view. World-locked stays where you put it, which is "
            + "what re-anchoring exists to undo when the cockpit moves out from under it.",
            SettingKind.Choice,
            v => v.Lock,
            (v, x) => v with { Lock = x == "world" ? "world" : "head" },
            ["head", "world"]);

        yield return Row(
            "distance",
            "Distance",
            "Metres in front of you. Head-locked only - a surface you have put down is wherever you put it.",
            SettingKind.Number,
            v => Number(v.Distance),
            (v, x) => v with { Distance = Parse(x, v.Distance) },
            step: 0.05);

        yield return Row(
            "size",
            "Size",
            "How wide the quad is, in metres. Height follows from the panel's proportions, because SteamVR "
            + "takes a width and derives the rest.",
            SettingKind.Number,
            v => Number(v.Width),
            (v, x) => v with { Width = Parse(x, v.Width) },
            step: 0.05);

        yield return Row(
            "curve",
            "Curvature",
            "0 is flat and 1 is wrapped right around you. This is the whole of curved versus flat: a number "
            + "reaching zero rather than a second mode, because a mode is a thing that can disagree with it.",
            SettingKind.Number,
            v => Number(v.Curvature),
            (v, x) => v with { Curvature = Parse(x, v.Curvature) },
            step: 0.05);

        yield return Row(
            "opacity",
            "Opacity",
            "How solid the surface is, from 0.1 to 1.",
            SettingKind.Number,
            v => Number(v.Opacity),
            (v, x) => v with { Opacity = Parse(x, v.Opacity) },
            step: 0.05);

        yield return Row(
            "scale",
            "Scale",
            "How large the panel is drawn, as a percentage. Distinct from mini mode: this changes the size "
            + "of everything on the panel, mini changes how much of it there is.",
            SettingKind.Choice,
            v => v.Zoom.ToString(CultureInfo.InvariantCulture),
            (v, x) => v with { Zoom = Interface.ZoomLadder.Snap((int)Parse(x, v.Zoom)) },
            [.. Interface.ZoomLadder.Steps.Select(step => step.ToString(CultureInfo.InvariantCulture))]);
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static double Parse(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    /// <summary>
    /// The caption rows, which all share one group so the explanation is stated once instead of
    /// four times, and all of which are absent when the overlays are off - a row that does not
    /// apply is absent rather than disabled, because a greyed-out control still asserts the
    /// setting exists.
    /// </summary>
    private static SettingRow CaptionRow(
        string key,
        string label,
        string help,
        SettingKind kind,
        Func<Configuration.D47Settings, string?> read,
        Func<Configuration.D47Settings, string?, Configuration.D47Settings> write,
        string anchor,
        IReadOnlyList<string>? choices = null,
        double step = 1) => new()
    {
        Key = key,
        Label = label,
        Help = help,
        Kind = kind,
        Step = step,
        Choices = choices ?? [],
        DocsAnchor = anchor,
        Group = "Captions",
        GroupHelp = "What d47 says, written under it, following the closed-caption standard: "
                    + "at most forty-two characters a line, a rolling three-line window, and a "
                    + "dwell timed from the end of speech rather than the start of it.",
        AppliesWhen = s => s.Vr.Enabled,
        Binding = new SettingBinding { Read = read, Write = write },
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
