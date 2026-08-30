using System.Globalization;
using D47.Core.Interface;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// How d47 looks and what reaches it from the keyboard. It registers no tools on purpose: a
/// descriptor exists to declare a capability's whole surface, and this one's surface is
/// settings rows. Giving the model a way to repaint the app or rebind a gesture would be
/// adding reach for the sake of symmetry.
/// </summary>
public static class InterfaceCapability
{
    public const string Id = "interface";

    public const string ThemeKey = "ui.theme";

    /// <summary>
    /// Whether the settings page is showing all of itself
    /// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>).
    /// </summary>
    public const string ShowEverySettingKey = "ui.showEverySetting";

    public const string ZoomKey = "ui.zoom";

    public const string OpenSettingsHotkeyKey = "hotkeys.openSettings";

    public const string FocusAskHotkeyKey = "hotkeys.focusAsk";

    /// <summary>
    /// Which content set the desktop window is showing (Phase 51).
    /// <para>
    /// <c>ui.mode</c> beside <c>vr.mode</c> rather than instead of it, because mode is per
    /// surface: what you are reading is shared and how a surface draws it is not.
    /// </para>
    /// </summary>
    public const string WindowModeKey = "ui.mode";

    public const string WindowModeHotkeyKey = "hotkeys.windowMode";

    /// <summary>The flat mini panel, on or off (Phase 48).</summary>
    public const string OverlayKey = "ui.overlay.enabled";

    public const string OverlayScaleKey = "ui.overlay.scale";

    public const string OverlayOpacityKey = "ui.overlay.opacity";

    /// <summary>
    /// What Elite's display mode is, stated rather than set. <b>This row is the point of the
    /// check behind it</b>: a topmost window is simply not there over an exclusive-fullscreen
    /// game — no error, no log line, nothing to diagnose — and this is the only place that can
    /// say so by name.
    /// </summary>
    public const string OverlayDisplayKey = "ui.overlay.display";

    public const string ShowOverlayHotkeyKey = "hotkeys.showOverlay";

    public const string MoveOverlayHotkeyKey = "hotkeys.moveOverlay";

    private const string OverlayGroup = "The overlay";

    private const string OverlayGroupHelp =
        "The mini panel on your monitor, for flying without a headset. It draws over the game, "
        + "the pointer goes straight through it, and it appears only while Elite is in front.";

    /// <param name="display">
    /// What Elite's display mode is, for the row that says whether the overlay will be visible
    /// at all. Defaults to reading the Commander's own <c>DisplaySettings.xml</c> — read-only and
    /// fail-soft, the same terms <see cref="ThemeCatalog"/>'s HUD matrix is read on, because this
    /// is the Commander's game configuration and d47 is a guest in it.
    /// </param>
    public static CapabilityDescriptor Create(Func<string>? display = null) => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Interface",
        Summary = "Choose D47's theme and the keys that reach it.",
        Examples = ["change the theme in settings", "rebind the settings hotkey"],
        Display = new CapabilityDisplay { PanelTitle = "Interface", Order = 40 },
        Settings =
        [
            new SettingRow
            {
                Key = ThemeKey,
                Label = "Theme",
                Help = "Colour scheme. \"Elite colour scheme\" follows your own HUD matrix if the game has one.",
                Kind = SettingKind.Choice,
                Choices = ThemeCatalog.Ids,
                ChoiceLabel = id => ThemeCatalog.Selected(id).Name,
                DocsAnchor = "theme",
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.Theme,
                    Write = (s, v) => s with { Ui = s.Ui with { Theme = v ?? ThemeCatalog.Elite } },
                },
            },
            new SettingRow
            {
                Key = ZoomKey,
                Label = "Zoom",
                Help = "How large the panel is drawn. Ctrl and the scroll wheel, Ctrl+plus, "
                       + "Ctrl+minus, and Ctrl+0 for 100% do the same thing from the panel itself.",
                Kind = SettingKind.Choice,
                Choices = [.. ZoomLadder.Steps.Select(step => step.ToString(CultureInfo.InvariantCulture))],
                ChoiceLabel = value => ZoomLadder.Describe(Parse(value)),
                DocsAnchor = "zoom",
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.ZoomPercent.ToString(CultureInfo.InvariantCulture),
                    // Snapped rather than validated: the gestures write rungs, and anything
                    // else reaching here came from a hand-edited file or a tool call, where
                    // "nearest level" is a better answer than "setting silently ignored".
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with { ZoomPercent = ZoomLadder.Snap(Parse(v)) },
                    },
                },
            },
            new SettingRow
            {
                Key = WindowModeKey,
                Label = "Window content",
                Help = "Full is everything. Mini is the transcript's tail, the ask box and the "
                       + "line under it - the same panel showing less, not a smaller copy. The "
                       + "window keeps its title bar in mini, so it can still be moved and closed.",
                Kind = SettingKind.Choice,
                Choices = ["full", "mini"],
                DocsAnchor = "window-mode",
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.Mode,
                    Write = (s, v) => s with { Ui = s.Ui with { Mode = v == "mini" ? "mini" : "full" } },
                },

                // <b>And these must not collide with the headset's.</b> `VrCapability.ModeKey`
                // already owns "mini panel" and "full panel"; a Commander in a headset who says
                // those must not shrink a window they cannot see, and one at a desk must not
                // resize a quad they are not wearing. The router matches a whole utterance
                // exactly, so "mini window" and "mini panel" reach one surface each.
                Commands =
                [
                    new SettingCommandPhrase("mini window", "mini"),
                    new SettingCommandPhrase("small window", "mini"),
                    new SettingCommandPhrase("little window", "mini"),
                    new SettingCommandPhrase("shrink the window", "mini"),
                    new SettingCommandPhrase("full window", "full"),
                    new SettingCommandPhrase("big window", "full"),
                    new SettingCommandPhrase("large window", "full"),
                ],
            },
            HotkeyRow(
                OpenSettingsHotkeyKey,
                "Open settings",
                "open-settings",
                s => s.Hotkeys.OpenSettings,
                (s, v) => s with { Hotkeys = s.Hotkeys with { OpenSettings = v } }),
            HotkeyRow(
                FocusAskHotkeyKey,
                "Focus the ask box",
                "focus-ask",
                s => s.Hotkeys.FocusAsk,
                (s, v) => s with { Hotkeys = s.Hotkeys with { FocusAsk = v } }),
            HotkeyRow(
                WindowModeHotkeyKey,
                "Switch the window between full and mini",
                "window-mode-key",
                s => s.Hotkeys.WindowMode,
                (s, v) => s with { Hotkeys = s.Hotkeys with { WindowMode = v } }),

            // Seventy-five knobs is not a welcome (#60). Here beside Theme, Zoom and the window
            // rows, whose grammar it copies — "Show the overlay", "Show every setting".
            //
            // A Toggle rather than a Simple/Advanced choice: those labels describe the Commander
            // rather than the settings, and a choice would force a name for the calm state that
            // then needs defining in help. A toggle needs no name for off.
            //
            // The label never says "advanced" and the phrases below happily accept it, which is
            // not an inconsistency: the phrase list is where d47 meets the Commander's words and
            // the label is where it chooses its own. "Go local only" writes the provider to none
            // under a label that just says Provider.
            new SettingRow
            {
                Key = ShowEverySettingKey,
                Label = "Show every setting",
                Help =
                    "Off, D47 shows the settings most Commanders change. Nothing is switched off "
                    + "by being hidden — every hidden setting keeps working at its default, or at "
                    + "the last thing you set it to. Anything you have changed yourself stays on "
                    + "the page either way.",
                Kind = SettingKind.Toggle,
                DocsAnchor = "show-every-setting",

                // At the top of the page, not in this card. It governs what the whole page draws,
                // and a Commander who cannot see the rest of the settings will not go looking for
                // the reason four rows into Interface (the Commander's call, 2026-08-26).
                PageTop = true,

                // At the top of the page, not in this card. It governs what the whole page draws,
                // and a Commander who cannot see the rest of the settings will not go looking for
                // the reason four rows into Interface (the Commander's call, 2026-08-26).
                Commands =
                [
                    new SettingCommandPhrase("show me every setting", "true"),
                    new SettingCommandPhrase("show all the settings", "true"),
                    new SettingCommandPhrase("show the advanced settings", "true"),
                    new SettingCommandPhrase("hide the advanced settings", "false"),
                    new SettingCommandPhrase("show fewer settings", "false"),
                    new SettingCommandPhrase("just the usual settings", "false"),
                ],
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.ShowEverySetting ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with { ShowEverySetting = bool.TryParse(v, out var show) && show },
                    },
                },
            },

            // The flat mini panel (Phase 48). Here rather than under VR because there is
            // no headset in it — a Commander goes looking where the theme, the zoom and the
            // hotkeys already are rather than under a card about a device they are not wearing.
            new SettingRow
            {
                Key = OverlayKey,
                Label = "Show the overlay",
                Help = "Pins the mini panel over the game: the transcript's last few lines, and "
                       + "the story if one is running. It shows itself only while Elite is in "
                       + "front, so turning it on here shows you nothing until the game is - that "
                       + "is on rather than broken. It cannot be clicked either: the pointer goes "
                       + "straight through, so nothing it shows can take a click Elite wanted.",
                Kind = SettingKind.Toggle,
                Group = OverlayGroup,
                GroupHelp = OverlayGroupHelp,
                DocsAnchor = "overlay",
                Binding = new SettingBinding
                {
                    // "true" and "false", spelled out, because the toggle control compares this
                    // string ordinally and `bool.ToString()` is "True" (#37). The write side parses
                    // case-insensitively, which is exactly what made this invisible: the setting
                    // took, the overlay appeared, and the switch that had just been flipped read
                    // off.
                    Read = s => s.Ui.Overlay.Enabled ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with
                        {
                            Overlay = s.Ui.Overlay with { Enabled = bool.TryParse(v, out var on) && on },
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = OverlayScaleKey,
                Advanced = true,
                Label = "Overlay size",
                Help = "How large the overlay is drawn. It re-wraps at each step rather than "
                       + "being blown up, so bigger means more readable and not blurrier.",
                Kind = SettingKind.Choice,
                Choices = [.. ZoomLadder.Steps.Select(step => step.ToString(CultureInfo.InvariantCulture))],
                ChoiceLabel = value => ZoomLadder.Describe(Parse(value)),
                Group = OverlayGroup,
                GroupHelp = OverlayGroupHelp,
                DocsAnchor = "overlay-size",
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.Overlay.ScalePercent.ToString(CultureInfo.InvariantCulture),

                    // Snapped rather than validated, exactly as the window's zoom is and for the
                    // same reason: a hand-edited 137 should land on 125 rather than on a level no
                    // control can step off.
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with
                        {
                            Overlay = s.Ui.Overlay with { ScalePercent = ZoomLadder.Snap(Parse(v)) },
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = OverlayOpacityKey,
                Advanced = true,
                Label = "Overlay opacity",
                Help = "How much cockpit shows through it. 1 is solid.",
                Kind = SettingKind.Number,
                Step = 0.05,

                // Not down to nothing. An overlay at zero is on, invisible, and indistinguishable
                // from broken - which is the failure the row below exists to prevent, arriving by
                // a different road.
                Minimum = 0.2,
                Maximum = 1,
                Group = OverlayGroup,
                GroupHelp = OverlayGroupHelp,
                DocsAnchor = "overlay-opacity",
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.Overlay.Opacity.ToString(CultureInfo.InvariantCulture),
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with
                        {
                            Overlay = s.Ui.Overlay with
                            {
                                Opacity = double.TryParse(
                                    v, NumberStyles.Float, CultureInfo.InvariantCulture, out var solid)
                                    ? Math.Clamp(solid, 0.2, 1)
                                    : Configuration.D47Settings.Defaults.Ui.Overlay.Opacity,
                            },
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = OverlayDisplayKey,
                Advanced = true,
                Label = "Elite's display mode",
                Help = "A window pinned on top draws over a borderless or windowed game and is "
                       + "simply not there over an exclusive-fullscreen one - with no error and "
                       + "nothing to diagnose. So D47 reads which one Elite is set to and says.",
                Kind = SettingKind.Info,
                Group = OverlayGroup,
                GroupHelp = OverlayGroupHelp,
                DocsAnchor = "overlay-fullscreen",
                Binding = new SettingBinding
                {
                    Read = _ => (display ?? DefaultDisplay)(),
                },
            },
            HotkeyRow(
                ShowOverlayHotkeyKey,
                "Show or hide the overlay",
                "show-overlay",
                s => s.Hotkeys.ShowOverlay,
                (s, v) => s with { Hotkeys = s.Hotkeys with { ShowOverlay = v } },
                systemWide: true),
            HotkeyRow(
                MoveOverlayHotkeyKey,
                "Move the overlay",
                "move-overlay",
                s => s.Hotkeys.MoveOverlay,
                (s, v) => s with { Hotkeys = s.Hotkeys with { MoveOverlay = v } },
                systemWide: true),
        ],
    };

    /// <summary>
    /// The Commander's own <c>DisplaySettings.xml</c>, read fresh every time the row is drawn.
    /// Fresh rather than captured because the fix for the sentence it may print is to go and
    /// change the setting in Elite, and a Commander who has just done that should be able to come
    /// back and see it say something else.
    /// </summary>
    private static string DefaultDisplay() => EliteDisplay.Describe(EliteDisplay.DefaultPath());

    private static int Parse(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
            ? percent
            : ZoomLadder.Default;

    /// <summary>
    /// Every hotkey row is protected. A gesture is one of the three callers that can reach a
    /// protected setting, so a model that could rebind one could hand itself a caller it is
    /// not allowed to be (architecture.md §7).
    /// </summary>
    private static SettingRow HotkeyRow(
        string key,
        string label,
        string anchor,
        Func<Configuration.D47Settings, string?> read,
        Func<Configuration.D47Settings, string?, Configuration.D47Settings> write,
        bool systemWide = false) => new()
    {
        Key = key,
        Advanced = true,
        Label = label,
        Help = "Press the key combination to bind it. Clear it to leave the action unbound."
               + (systemWide
                   ? " Registered system-wide, so it works while Elite has the foreground - which is "
                     + "the only time it is wanted."
                   : string.Empty),
        Kind = SettingKind.Hotkey,
        DefaultDisplay = "(unbound)",
        DocsAnchor = anchor,
        Protected = true,

        // The factory already knew this; the row did not. Saying so is what lets a bare key be
        // refused as it is pressed rather than failing to register afterwards.
        SystemWide = systemWide,
        Binding = new SettingBinding { Read = read, Write = write },
    };
}
