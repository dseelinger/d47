using System.Globalization;
using D47.Core.Configuration;
using D47.Core.Vr;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The headset (Phase 9). What it declares is a switch and a state — where the
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

    /// <summary>
    /// How solid the panel is — <b>not under a surface slot</b>, because it is one number for both
    /// of them (asked for 2026-08-24, after <c>vr.panel.opacity</c> was set while the mini panel
    /// was the one on screen and nothing the Commander could see changed).
    /// </summary>
    public const string OpacityKey = "vr.opacity";

    /// <summary>
    /// Whether d47 touches the motion controllers at all. Off out of the box since #198 — see
    /// <see cref="Configuration.VrSettings.Controllers"/> for the whole of why.
    /// </summary>
    public const string ControllersKey = "vr.controllers";

    /// <summary>The surface a placement row belongs to, as it appears in the key.</summary>
    public const string PanelSlot = "panel";

    public const string MiniSlot = "mini";

    /// <summary>
    /// The slot that means <em>the one in front of me</em> (#21). Not a stored surface — the rows
    /// under it resolve <see cref="ModeKey"/> and land on <see cref="PanelSlot"/> or
    /// <see cref="MiniSlot"/>, so nothing new is ever written to <c>settings.json</c>.
    /// </summary>
    public const string CurrentSlot = "current";

    /// <summary>Whether the mini panel is the one on screen.</summary>
    private static bool IsMini(Configuration.D47Settings s) =>
        string.Equals(s.Vr.Mode, MiniSlot, StringComparison.OrdinalIgnoreCase);

    /// <summary>The surface settings for whichever panel is on screen.</summary>
    private static VrSurfaceSettings Facing(Configuration.D47Settings s) =>
        IsMini(s) ? s.Vr.Mini : s.Vr.Panel;

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
        public required Func<(VrState State, string? Reason)> Report { get; init; }

        /// <summary>
        /// Moves whichever panel is on screen one or more steps, and says what happened
        /// (<a href="https://github.com/dseelinger/d47/issues/199">#199</a>).
        /// <para>
        /// The outcome rather than a sentence, so the words live in Core beside every other
        /// thing d47 says about a surface — see <see cref="Vr.VrNudges.Describe"/>. A host that
        /// wrote its own would be a second description of one act, free to disagree with the
        /// first.
        /// </para>
        /// </summary>
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

            // Asking to see the panel used to reach the status tool, because that was the only
            // headset-shaped thing on the surface — so "show the VR panel" was answered with
            // "the overlays are dark", which is a true sentence and not what was asked for.
            // set_setting could always have done it; nothing pointed at that
            // (remediation.md, "Show the VR panel did not show it").
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

            // **Placing a panel without a controller** (#199), and since #219 the only way to
            // move one by voice at all. Re-anchor used to sit on its own capability because the
            // keyword router answers with a capability's *first* argument-free tool; this one is
            // reached through a declared phrase and its arguments instead, so what else this
            // capability offers cannot shadow it and it needs no capability of its own.
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
                Label = "Motion controllers",
                Help = "Whether D47 touches your motion controllers at all - the pointing ray, the "
                       + "trigger and the grip. Off, and that is a withdrawal rather than a preference: "
                       + "D47 read controller poses ninety times a second for a whole session whether "
                       + "or not anything was being pointed at, and that is the untested half of why a "
                       + "controller put down while D47 was running never woke up again. With it off "
                       + "nothing on the panel can be pressed in the headset, the headset Settings tab "
                       + "cannot be reached, and the panel cannot be grabbed and carried - say \"move "
                       + "the panel left\" instead. Turn it on to see whether the fault comes back.",
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

            // **The one the Commander means** (#21). Ruled 2026-08-24: *"whichever panel I'm
            // looking at."* Stores nothing of its own — it resolves vr.mode at the moment it is
            // read or written and lands on that surface's values, so settings.json gains no key
            // and the two surfaces keep their own numbers, which is the point: mini exists to sit
            // further out of the way.
            .. Placement(
                CurrentSlot,
                "Panel you are looking at",
                Facing,
                (s, v) => IsMini(s)
                    ? s with { Vr = s.Vr with { Mini = v } }
                    : s with { Vr = s.Vr with { Panel = v } }),

            // Both explicit sets stay on the page, in full, and are no longer offered to the model.
            // Three ways to say one number is how the wrong one gets picked, and these two are only
            // ever the two wrong answers to "move the panel closer".
            .. Placement(PanelSlot, "Panel", s => s.Vr.Panel, (s, v) => s with { Vr = s.Vr with { Panel = v } }, pageOnly: true),
            .. Placement(MiniSlot, "Mini panel", s => s.Vr.Mini, (s, v) => s with { Vr = s.Vr with { Mini = v } }, pageOnly: true),
            CaptionRow(
                CaptionsEnabledKey,
                "Captions",
                "Everything D47 says, written under it in the headset. They place themselves, "
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
        Func<Configuration.D47Settings, VrSurfaceSettings, Configuration.D47Settings> write,
        bool pageOnly = false)
    {
        var mini = string.Equals(slot, MiniSlot, StringComparison.Ordinal);
        var current = string.Equals(slot, CurrentSlot, StringComparison.Ordinal);

        // <b>Which of the two surfaces this row is about</b>, said on every row rather than left to
        // the key. Reported 2026-08-23 as "opacity does not change the opacity": the Commander was
        // looking at the mini panel, `vr.panel.opacity` went to 0.5, and SteamVR's own readback
        // still said 0.95 four minutes later — because that is the *other* surface's number and the
        // one on screen was never asked to change. Nothing was broken and nothing said so.
        //
        // The row cannot say "you are in mini" — a descriptor is registered once and never mutated,
        // which is what keeps the tool surface byte-identical across turns — so it says which
        // surface it governs and where the other one's copy lives, which is true at any moment.
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
            // "Placing a surface" is the section, and it says outright that there are five
            // settings each with the mini panel keeping its own copies. Ten per-row headings
            // would be documentation written to satisfy a link rather than to be read (#123).
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

        // The big panel only. Mini's pixel budget is not on this ladder and is not meant to be:
        // it is a floor under a reduced content set rather than an aspect, which VrPanelSurface
        // records at length (Phase 25).
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

    /// <summary>
    /// Moves the panel that is on screen (#199).
    /// <para>
    /// Nothing is clamped here. <see cref="VrNudges.Steps"/> already decides what a call is
    /// allowed to do, and a second opinion about it in the capability is a second place for the
    /// two to disagree.
    /// </para>
    /// </summary>
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
    /// The phrases that reach <c>move_headset_panel</c> with no model in the path, which is the
    /// route that has to work: the controller is withdrawn (#198), so voice is the only way a
    /// Commander in a headset can place a panel, and local-only operation is supported.
    /// <para>
    /// One entry per direction and several spellings of each, because a phrase the router does
    /// not have is a phrase that falls through to a model that may not be there. They cost no
    /// schema bytes — see <see cref="ToolCommandPhrase"/>.
    /// </para>
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

    /// <summary>
    /// Turns the headset overlays on or off, and then says what that produced.
    /// <para>
    /// Through the settings service like every other write, so the row, the file and the panel
    /// all move together — and as <see cref="SettingsCaller.Model"/>, so the protections that
    /// apply to a tool call apply to this one too. It is not a way around them; it is a way to
    /// the one row a Commander in a headset most obviously wants to reach by voice.
    /// </para>
    /// <para>
    /// The answer is the status rather than an acknowledgement, because switching it on is not
    /// the same as it appearing: with no runtime installed the setting takes and nothing shows,
    /// and saying "done" there would be the second time this capability answered the wrong
    /// question.
    /// </para>
    /// </summary>
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
