using System.Globalization;
using D47.Core.Configuration;
using D47.Core.Vr;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The headset (Phase 9).</summary>
public static class VrCapability
{
    public const string Id = "vr";

    public const string EnabledKey = "vr.enabled";

    public const string StateKey = "vr.state";

    public const string ModeKey = "vr.mode";

    /// <summary>
    /// How solid the panel is — not under a surface slot, because it is one number for both of them
    /// (asked for 2026-08-24, after <c>vr.panel.opacity</c> was set while the mini panel was the one on
    /// screen and nothing the Commander could see changed).
    /// </summary>
    public const string OpacityKey = "vr.opacity";

    /// <summary>Whether d47 touches the motion controllers at all.</summary>
    public const string ControllersKey = "vr.controllers";

    /// <summary>The surface a placement row belongs to, as it appears in the key.</summary>
    public const string PanelSlot = "panel";

    public const string MiniSlot = "mini";

    /// <summary>The slot that means the one in front of me (#21).</summary>
    public const string CurrentSlot = "current";

    /// <summary>Whether the mini panel is the one on screen.</summary>
    private static bool IsMini(Configuration.D47Settings s) =>
        string.Equals(s.Vr.Mode, MiniSlot, StringComparison.OrdinalIgnoreCase);

    /// <summary>The surface settings for whichever panel is on screen.</summary>
    private static VrSurfaceSettings Facing(Configuration.D47Settings s) =>
        IsMini(s) ? s.Vr.Mini : s.Vr.Panel;

    /// <summary>The lock row's key for a surface.</summary>
    public static string LockKey(string slot) => $"vr.{slot}.lock";

    public const string CaptionsEnabledKey = "vr.captions.enabled";

    /// <summary>The one placement row captions have (#204).</summary>
    public const string CaptionLockKey = "vr.captions.lock";

    public const string CaptionSizeKey = "vr.captions.size";

    public const string CaptionBackgroundKey = "vr.captions.background";

    public const string CaptionSpeedKey = "vr.captions.speed";

    /// <summary>The reading speeds offered, in characters per second.</summary>
    private static readonly IReadOnlyList<string> ReadingSpeeds = ["12", "17", "20"];

    /// <summary>What the app can tell this capability about the live session.</summary>
    public sealed record HeadsetSurface
    {
        public required Func<(VrState State, string? Reason)> Report { get; init; }

        /// <summary>Moves whichever panel is on screen one or more steps, and says what happened (#199).</summary>
        public required Func<VrNudge, int, VrNudgeOutcome> Nudge { get; init; }
    }

    public static CapabilityDescriptor Create(SettingsService settings, HeadsetSurface headset) => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Headset",
        Summary = "Show D47 in the headset as a SteamVR overlay, over Elite, in your own cockpit.",
        Examples = ["is the headset connected", "turn the headset overlay off"],
        Keywords = ["headset status", "vr status", "is the headset connected"],
        Display = new CapabilityDisplay { PanelTitle = "Headset", Order = 45 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_headset_status",
                Description =
                    "Report whether D47 is showing in the headset, and if not, why not. "
                    + "Reports only; use show_in_headset to turn it on or off.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(settings, headset))),
            },

            // Named separately from the status tool, so "show the VR panel" is not answered with a reading.
            new ToolDefinition
            {
                Name = "show_in_headset",
                Description =
                    "Show D47 in the headset, or stop showing it. This is the one that acts; "
                    + "get_headset_status only reports.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "on",
                        Type = ToolParameterType.Boolean,
                        Description = "True to show D47 in the headset, false to leave SteamVR alone.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Show(settings, headset, arguments)),
            },

            // **Placing a panel without a controller** (#199), and since #219 the only way to move one by
            // voice at all.
            new ToolDefinition
            {
                Name = "move_headset_panel",
                Description =
                    "Move the headset panel a step at a time: left, right, up, down, nearer, further, "
                    + "or turn or tilt it. Acts on whichever panel is on screen, and puts it down in "
                    + "front of the Commander first if it was still riding their head.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "direction",
                        Type = ToolParameterType.String,
                        Description =
                            "Which way. turn-left and turn-right swing the face of the panel towards "
                            + "that side; tilt-up leans it back to face the Commander.",
                        Required = true,
                        AllowedValues = VrNudges.Names,
                    },
                    new ToolParameter
                    {
                        Name = "steps",
                        Type = ToolParameterType.Integer,
                        Description = "How many steps, 1 to 20. One step is 5 cm or 5 degrees. Defaults to one.",
                    },
                ],
                Commands = [.. NudgePhrases()],
                Handler = (arguments, _) => Task.FromResult(Move(headset, arguments)),
            },
        ],
        Settings =
        [
            new SettingRow
            {
                Key = EnabledKey,
                Label = "Show D47 in the headset",
                Help = "Off leaves SteamVR alone entirely. On costs nothing on a machine with no headset — "
                       + "D47 looks for one, does not find one, and says so.",
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
                Advanced = true,
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
                    new SettingCommandPhrase("small panel", "mini"),
                    new SettingCommandPhrase("little panel", "mini"),
                    new SettingCommandPhrase("minimal panel", "mini"),
                    new SettingCommandPhrase("full panel", "full"),
                    new SettingCommandPhrase("big panel", "full"),
                    new SettingCommandPhrase("large panel", "full"),
                ],
            },
            new SettingRow
            {
                Key = OpacityKey,
                Advanced = true,
                Label = "Panel opacity",
                Help = "How solid the panel is, from 0.1 to 1. One setting for both panels: the mini one "
                       + "and the full one are as see-through as each other, because how much cockpit "
                       + "shows through D47 is one preference and not two.",
                Kind = SettingKind.Number,
                Step = 0.05,
                Minimum = 0.1,
                Maximum = 1,
                DocsAnchor = "opacity",
                AppliesWhen = s => s.Vr.Enabled,
                Binding = new SettingBinding
                {
                    Read = s => s.Vr.Opacity.ToString("0.##", CultureInfo.InvariantCulture),
                    Write = (s, v) => s with
                    {
                        Vr = s.Vr with
                        {
                            Opacity = double.TryParse(
                                v, NumberStyles.Float, CultureInfo.InvariantCulture, out var wanted)
                                ? Math.Clamp(wanted, 0.1, 1)
                                : s.Vr.Opacity,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = ControllersKey,
                Advanced = true,
                // Two sentences and a badge, since #237.
                Label = "Motion controllers",
                Help = "Whether D47 uses your motion controllers - the pointing ray, the trigger "
                       + "and the grip. With it off nothing on the panel can be pressed in the "
                       + "headset and the panel cannot be grabbed - say \"move the panel left\" "
                       + "instead.",
                Warning = "Known bug: with this on, a controller you put down may never wake from "
                          + "standby. When you finish trying it, turn it back off and restart D47.",
                Kind = SettingKind.Toggle,
                DocsAnchor = "controllers",
                AppliesWhen = s => s.Vr.Enabled,
                Binding = new SettingBinding
                {
                    Read = s => s.Vr.Controllers ? "true" : "false",
                    Write = (s, v) => s with { Vr = s.Vr with { Controllers = v == "true" } },
                },
                Commands =
                [
                    new SettingCommandPhrase("motion controllers on", "true"),
                    new SettingCommandPhrase("motion controllers off", "false"),
                ],
            },

            // **The one the Commander means** (#21).
            .. Placement(
                CurrentSlot,
                "Panel you are looking at",
                Facing,
                (s, v) => IsMini(s)
                    ? s with { Vr = s.Vr with { Mini = v } }
                    : s with { Vr = s.Vr with { Panel = v } }),

            // Both explicit sets stay on the page, in full, and are no longer offered to the model.
            .. Placement(PanelSlot, "Panel", s => s.Vr.Panel, (s, v) => s with { Vr = s.Vr with { Panel = v } }, pageOnly: true),
            .. Placement(MiniSlot, "Mini panel", s => s.Vr.Mini, (s, v) => s with { Vr = s.Vr with { Mini = v } }, pageOnly: true),
            CaptionRow(
                CaptionsEnabledKey,
                "Captions",
                "Everything D47 says, written under it in the headset. They place themselves, "
                + "they clear themselves, and there is nothing to drag them with - a caption you "
                + "can drag somewhere you will not see it is not a caption. Position picks "
                + "between the two places D47 works out for you.",
                SettingKind.Toggle,
                s => s.Vr.Captions.Enabled ? "true" : "false",
                (s, v) => s with { Vr = s.Vr with { Captions = s.Vr.Captions with { Enabled = v == "true" } } },
                "captions"),
            CaptionRow(
                CaptionLockKey,
                "Caption position",
                "Head-locked keeps the band in front of you wherever you look. World-locked puts "
                + "it in one place low in the cockpit, between the console and your feet, and "
                + "leaves it there while you look around. Head-locked is always readable and is "
                + "also the one that can make you queasy - it is the only thing in the headset "
                + "that does not move when you turn, which is exactly the disagreement between "
                + "your eyes and your inner ear that motion sickness is. World-locked costs "
                + "having to glance down for it. Two positions and no others: there is no "
                + "distance, no curve and nothing to drag either way.",
                SettingKind.Choice,
                s => s.Vr.Captions.Locking == SurfaceLock.WorldLocked ? "world" : "head",
                (s, v) => s with
                {
                    Vr = s.Vr with
                    {
                        Captions = s.Vr.Captions with
                        {
                            Lock = string.Equals(v, "world", StringComparison.OrdinalIgnoreCase)
                                ? "world"
                                : "head",
                        },
                    },
                },
                "position",
                choices: ["head", "world"]),
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
                "How solid the box behind the text is, from 0.6 to 1. Not fully solid by default: "
                + "a caption sits over a starfield and a station's floodlights, and a box you "
                + "cannot see through is a hole cut in the cockpit. It does not go below 0.6, "
                + "because a station floodlight behind a box any more see-through than that "
                + "leaves nothing you could read.",
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
                Advanced = true,
                Label = "Headset",
                Help = "What D47 can currently see. Not a setting — a state, reported where the switch is, "
                       + "because \"it is off\" and \"SteamVR is not running\" look identical from the outside.",
                Kind = SettingKind.Info,
                DocsAnchor = "state",
                Binding = new SettingBinding { Read = _ => Describe(settings, headset) },
            },
        ],
    };

    /// <summary>The six knobs Overlay Positioning &amp; Look names, plus the lock, for one surface.</summary>
    private static IEnumerable<SettingRow> Placement(
        string slot,
        string what,
        Func<Configuration.D47Settings, VrSurfaceSettings> read,
        Func<Configuration.D47Settings, VrSurfaceSettings, Configuration.D47Settings> write,
        bool pageOnly = false)
    {
        var mini = string.Equals(slot, MiniSlot, StringComparison.Ordinal);
        var current = string.Equals(slot, CurrentSlot, StringComparison.Ordinal);

        // Which of the two surfaces this row is about, said on every row rather than left to the key.
        var scope = current
            ? " Applies to whichever panel is on screen right now — the big one or the mini one, "
              + "whichever vr.mode currently names. Each keeps its own number, so changing this "
              + "while in mini leaves the big panel exactly where it was."
            : $" Applies to the {what.ToLowerInvariant()} alone — what you see while vr.mode "
              + $"is {(mini ? "mini" : "full")}. The {(mini ? "big panel" : "mini panel")} keeps "
              + $"its own, under vr.{(mini ? PanelSlot : MiniSlot)}.";

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
            Advanced = true,
            Label = label,
            Help = help + scope,
            Kind = kind,
            Choices = choices ?? [],
            // "Placing a surface" is the section, and it says outright that there are five settings each with
            // the mini panel keeping its own copies.
            DocsAnchor = "placing-a-surface",
            Group = $"{what} placement",
            GroupHelp = $"Where the {what.ToLowerInvariant()} sits and what it looks like. You can also just "
                        + "reach out and grab it with a controller, which is what the numbers are here for "
                        + "when you would rather not.",
            AppliesWhen = s => s.Vr.Enabled,
            PageOnly = pageOnly,
            Binding = new SettingBinding
            {
                Read = s => get(read(s)),
                Write = (s, v) => write(s, set(read(s), v)),
            },
        };

        yield return Row(
            "lock",
            $"{what} locking",
            "Head-locked follows you and is always in view. World-locked stays where you put it, and "
            + "stays there when the cockpit moves out from under it.",
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
            "How big the panel is, in metres across. Height follows from the panel's proportions, because "
            + "SteamVR takes a width and derives the rest. This is the size of the panel itself; to make "
            + "the writing on it bigger without moving the edges, use scale.",
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
            "scale",
            "Scale",
            "How big everything drawn on the panel is, as a percentage - the text and the controls, without "
            + "the panel's own edges moving. To make the whole panel bigger instead, use size. Distinct "
            + "from mini mode again: this changes how large things are, mini changes how much of them "
            + "there is.",
            SettingKind.Choice,
            v => v.Zoom.ToString(CultureInfo.InvariantCulture),
            (v, x) => v with { Zoom = Interface.ZoomLadder.Snap((int)Parse(x, v.Zoom)) },
            [.. Interface.ZoomLadder.Steps.Select(step => step.ToString(CultureInfo.InvariantCulture))]);

        // The big panel only.
        if (slot != PanelSlot)
        {
            yield break;
        }

        yield return Row(
            "resolution",
            "Resolution",
            "How many pixels the panel is rendered at, and the third of three levers that are worth keeping "
            + "apart: pixels decide how much the image can hold, Size decides how big it looks in the room, "
            + "and Scale decides how much layout those pixels carry. More pixels cost more to render every "
            + "frame, and past what the quad covers in your headset they buy nothing - so this is a trade "
            + "you make by looking, not a number to maximise.",
            SettingKind.Choice,
            v => Interface.PanelResolution.Describe(v.Resolution),
            (v, x) => v with
            {
                Pixels = Interface.PanelResolution.Describe(Interface.PanelResolution.Parse(x)),
            },
            [.. Interface.PanelResolution.Choices]);
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static double Parse(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    /// <summary>
    /// The caption rows, which all share one group so the explanation is stated once instead of four
    /// times, and all of which are absent when the overlays are off - a row that does not apply is
    /// absent rather than disabled, because a greyed-out control still asserts the setting exists.
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
        Advanced = true,
        Label = label,
        Help = help,
        Kind = kind,
        Step = step,
        Choices = choices ?? [],
        DocsAnchor = anchor,
        Group = "Captions",
        GroupHelp = "What D47 says, written under it, following the closed-caption standard: "
                    + "at most forty-two characters a line, a rolling two-line window, a longer "
                    + "sentence shown two lines at a time until it is done, and a dwell timed "
                    + "from the end of speech rather than the start of it.",
        AppliesWhen = s => s.Vr.Enabled,
        Binding = new SettingBinding { Read = read, Write = write },
    };

    /// <summary>Moves the panel that is on screen (#199).</summary>
    private static ToolResult Move(HeadsetSurface headset, ToolArguments arguments)
    {
        if (!arguments.TryGetString("direction", out var said) || VrNudges.Parse(said) is not { } nudge)
        {
            return ToolResult.Error(
                $"Say which way to move the panel: {string.Join(", ", VrNudges.Names)}.");
        }

        var steps = arguments.TryGetInt32("steps", out var asked) ? asked : 1;

        return ToolResult.Ok(VrNudges.Describe(nudge, headset.Nudge(nudge, steps)));
    }

    /// <summary>
    /// The phrases that reach <c>move_headset_panel</c> with no model in the path, which is the route
    /// that has to work: the controller is withdrawn (#198), so voice is the only way a Commander in a
    /// headset can place a panel, and local-only operation is supported.
    /// </summary>
    private static IEnumerable<ToolCommandPhrase> NudgePhrases()
    {
        foreach (var (nudge, spellings) in Spellings())
        {
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["direction"] = VrNudges.Names[(int)nudge],
            };

            foreach (var spelling in spellings)
            {
                yield return new ToolCommandPhrase(spelling, arguments);
            }
        }
    }

    private static IEnumerable<(VrNudge Nudge, string[] Spellings)> Spellings()
    {
        yield return (VrNudge.Left, ["move the panel left", "panel left", "nudge the panel left"]);
        yield return (VrNudge.Right, ["move the panel right", "panel right", "nudge the panel right"]);
        yield return (VrNudge.Up, ["move the panel up", "panel up", "nudge the panel up", "raise the panel"]);
        yield return (VrNudge.Down, ["move the panel down", "panel down", "nudge the panel down", "lower the panel"]);
        yield return (VrNudge.Nearer, ["move the panel closer", "move the panel nearer", "panel closer", "bring the panel closer"]);
        yield return (VrNudge.Further, ["move the panel away", "move the panel further away", "panel further away", "push the panel away"]);
        yield return (VrNudge.TurnLeft, ["turn the panel left", "yaw the panel left"]);
        yield return (VrNudge.TurnRight, ["turn the panel right", "yaw the panel right"]);
        yield return (VrNudge.TiltUp, ["tilt the panel up", "tilt the panel back"]);
        yield return (VrNudge.TiltDown, ["tilt the panel down", "tilt the panel forward"]);
    }

    /// <summary>Turns the headset overlays on or off, and then says what that produced.</summary>
    private static ToolResult Show(SettingsService settings, HeadsetSurface headset, ToolArguments arguments)
    {
        if (!arguments.TryGetBoolean("on", out var on))
        {
            return ToolResult.Error("Say whether to show D47 in the headset or not.");
        }

        var applied = settings.Apply(EnabledKey, on ? "true" : "false", SettingsCaller.Model);

        if (applied.Status != SettingApplyStatus.Applied)
        {
            return ToolResult.Error(applied.Message ?? "That could not be changed.");
        }

        return ToolResult.Ok(Describe(settings, headset));
    }

    private static string Describe(SettingsService settings, HeadsetSurface headset)
    {
        if (!settings.Current.Vr.Enabled)
        {
            return "The headset overlays are switched off.";
        }

        var (state, reason) = headset.Report();

        return state switch
        {
            VrState.Active => "Showing in the headset.",
            VrState.Connecting => reason ?? "Looking for a headset.",
            _ => reason ?? "No SteamVR runtime is installed on this machine.",
        };
    }
}
