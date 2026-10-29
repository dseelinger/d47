using System.Globalization;
using D47.Core.Interface;
using D47.Core.Storage;

namespace D47.Core.Capabilities.Builtin;

/// <summary>How d47 looks and what reaches it from the keyboard.</summary>
public static class InterfaceCapability
{
    public const string Id = "interface";

    public const string ThemeKey = "ui.theme";

    /// <summary>Whether the settings page is showing all of itself (#60).</summary>
    public const string ShowEverySettingKey = "ui.showEverySetting";

    public const string ZoomKey = "ui.zoom";

    /// <summary>Where the panel's tabs are drawn.</summary>
    public const string TabsKey = "ui.tabs";

    public const string TabsTop = "top";

    public const string TabsLeft = "left";

    public const string OpenSettingsHotkeyKey = "hotkeys.openSettings";

    public const string FocusAskHotkeyKey = "hotkeys.focusAsk";

    public const string FindHotkeyKey = "hotkeys.find";

    public const string OpenSettingsButtonKey = "hotkeys.openSettingsButton";

    public const string FocusAskButtonKey = "hotkeys.focusAskButton";

    public const string FindButtonKey = "hotkeys.findButton";

    /// <summary>The flat mini panel, on or off (Phase 48).</summary>
    public const string OverlayKey = "ui.overlay.enabled";

    public const string OverlayScaleKey = "ui.overlay.scale";

    public const string OverlayOpacityKey = "ui.overlay.opacity";

    /// <summary>What Elite's display mode is, stated rather than set.</summary>
    public const string OverlayDisplayKey = "ui.overlay.display";

    public const string ShowOverlayHotkeyKey = "hotkeys.showOverlay";

    public const string MoveOverlayHotkeyKey = "hotkeys.moveOverlay";

    public const string ShowOverlayButtonKey = "hotkeys.showOverlayButton";

    public const string MoveOverlayButtonKey = "hotkeys.moveOverlayButton";

    private const string OverlayGroup = "The overlay";

    /// <summary>The start of a step for a row shown only while Show every setting is on.</summary>
    internal const string AdvancedPrefix =
        "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), open ";

    /// <summary>Where a hotkey row is, its default key, and what pressing it does.</summary>
    internal sealed record HotkeyExercise(string Where, string DefaultKey, string Effect);

    /// <summary>
    /// <param name="display"> What Elite's display mode is, for the row that says whether the overlay
    /// will be visible at all.
    /// </summary>
    /// <param name="display">
    /// What Elite's display mode is, for the row that says whether the overlay will be visible at all.
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
                Exercise =
                    "Open Settings › Screens › Window, group Look and keys, and find Theme. Choose each of Elite, "
                    + "Dark, Light and My HUD colours in turn: the whole window recolours at once. Elite is amber on "
                    + "near-black (the default), Dark is neutral dark, Light is neutral light. My HUD colours is Elite "
                    + "recoloured by the Commander's own HUD colour matrix, so it differs from Elite only when the game "
                    + "has one set. Choose Elite to put it back.",
                Label = "Theme",
                Help = "Colour scheme. \"My HUD colours\" follows your own HUD colour matrix if the game has one.",
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
                Exercise =
                    "Open Settings › Screens › Window, group Look and keys, and find Zoom. Choose 150%: everything in "
                    + "the window is drawn larger. Choose 50%: everything is drawn smaller. The choices run 50, 67, 75, "
                    + "80, 90, 100, 110, 125, 150, 175, 200, 250 and 300%. Ctrl with the scroll wheel, Ctrl+plus, "
                    + "Ctrl+minus and Ctrl+0 in the window step the same row, and the row shows the new level. Choose "
                    + "100% to put it back.",
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
                    // Snapped rather than validated: the gestures write rungs, and anything else reaching
                    // here came from a hand-edited file or a tool call, where "nearest level" is a better
                    // answer than "setting silently ignored".
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with { ZoomPercent = ZoomLadder.Snap(Parse(v)) },
                    },
                },
            },
            new SettingRow
            {
                Key = TabsKey,
                Exercise =
                    "Open Settings › Screens › Window, group Look and keys, and find Tabs. Choose Down the left: the "
                    + "panel's tabs move from a row along the top to a column down the left side, in the window and in "
                    + "the headset panel alike. Choose Along the top to put them back (the default).",
                Label = "Tabs",
                Help = "Where the panel's tabs are drawn: along the top, or down the left-hand side. "
                       + "Applies to the window and the headset panel alike.",
                Kind = SettingKind.Choice,
                Choices = [TabsTop, TabsLeft],
                ChoiceLabel = value => value == TabsLeft ? "Down the left" : "Along the top",
                DocsAnchor = "tabs",
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.Tabs == TabsLeft ? TabsLeft : TabsTop,
                    Write = (s, v) => s with { Ui = s.Ui with { Tabs = v == TabsLeft ? TabsLeft : TabsTop } },
                },
            },
            .. HotkeyRows(
                OpenSettingsHotkeyKey,
                OpenSettingsButtonKey,
                "Open settings",
                "open-settings",
                s => s.Hotkeys.OpenSettings,
                (s, v) => s with { Hotkeys = s.Hotkeys with { OpenSettings = v } },
                exercise: new(
                    "Settings › Screens › Window, group Look and keys",
                    "Ctrl+,",
                    "the panel switches to the Settings tab, from whichever tab it was on.")),
            .. HotkeyRows(
                FocusAskHotkeyKey,
                FocusAskButtonKey,
                "Focus the ask box",
                "focus-ask",
                s => s.Hotkeys.FocusAsk,
                (s, v) => s with { Hotkeys = s.Hotkeys with { FocusAsk = v } },
                exercise: new(
                    "Settings › Screens › Window, group Look and keys",
                    "Ctrl+L",
                    "the cursor goes to the ask box and any text in it is selected.")),
            .. HotkeyRows(
                FindHotkeyKey,
                FindButtonKey,
                "Find",
                "find",
                s => s.Hotkeys.Find,
                (s, v) => s with { Hotkeys = s.Hotkeys with { Find = v } },
                exercise: new(
                    "Settings › Screens › Window, group Look and keys",
                    "Ctrl+Shift+F",
                    "a dialog titled Find opens, with a FIND: field that hints \"A page, a setting or a help page\". "
                        + "Press it again and the dialog closes. Nothing opens while another dialog is open.")),

            // Seventy-five knobs is not a welcome (#60).
            new SettingRow
            {
                Key = ShowEverySettingKey,
                Exercise =
                    "At the top of the Settings page find Show every setting (off by default). Turn it on, or say "
                    + "\"show me every setting\": rows marked advanced appear, for example the hotkey rows under "
                    + "Settings › Screens › Window. Turn it off, or say \"just the usual settings\": they hide again, "
                    + "except any you have changed yourself. \"show all the settings\" and \"show the advanced "
                    + "settings\" turn it on; \"hide the advanced settings\" and \"show fewer settings\" turn it off.",
                Label = "Show every setting",
                Help =
                    "Off, D47 shows the settings most Commanders change. Nothing is switched off "
                    + "by being hidden — every hidden setting keeps working at its default, or at "
                    + "the last thing you set it to. Anything you have changed yourself stays on "
                    + "the page either way.",
                Kind = SettingKind.Toggle,
                DocsAnchor = "show-every-setting",

                // At the top of the page, not in this card.
                PageTop = true,

                // At the top of the page, not in this card.
                Commands =
                [
                    new SettingCommandPhrase("show me every setting", "true"),
                    new SettingCommandPhrase("show all the settings", "true"),
                    new SettingCommandPhrase("show the advanced settings", "true"),
                    new SettingCommandPhrase("hide the advanced settings", "false"),
                    new SettingCommandPhrase("show fewer settings", "false"),
                    new SettingCommandPhrase("just the usual settings", "false"),
                ],
                Binding = SettingBinding.Toggle(
                    s => s.Ui.ShowEverySetting,
                    (s, on) => s with { Ui = s.Ui with { ShowEverySetting = on } }),
            },

            // The flat mini panel (Phase 48).
            new SettingRow
            {
                Key = OverlayKey,
                Exercise =
                    "Open Settings › Screens › Overlay, group Overlay, and turn Show the overlay on. Bring Elite to "
                    + "the front in borderless or windowed display mode: the mini panel appears pinned over the game, "
                    + "showing the transcript's last few lines and the story if one is running. It cannot be clicked: "
                    + "the pointer passes through to the game. Switch to another window and it goes. Turn the row off "
                    + "and it stays gone with Elite in front. The Elite's display mode row below it says whether the "
                    + "game's display mode hides it.",
                Label = "Show the overlay",
                Help = "Pins the mini panel over the game: the transcript's last few lines, and "
                       + "the story if one is running. It shows itself only while Elite is in "
                       + "front, so turning it on here shows you nothing until the game is - that "
                       + "is on rather than broken. It cannot be clicked either: the pointer goes "
                       + "straight through, so nothing it shows can take a click Elite wanted.",
                Kind = SettingKind.Toggle,
                Group = OverlayGroup,
                DocsAnchor = "overlay",
                Binding = SettingBinding.Toggle(
                    s => s.Ui.Overlay.Enabled,
                    (s, on) => s with { Ui = s.Ui with { Overlay = s.Ui.Overlay with { Enabled = on } } }),
            },
            new SettingRow
            {
                Key = OverlayScaleKey,
                Exercise =
                    "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                    + "open Settings › Screens › Overlay, group Overlay, and find Overlay size, under Show the overlay "
                    + "(the overlay must be on). With Elite in front, choose 200%: the overlay is drawn larger and its "
                    + "text re-wraps rather than blurring. Choose 50%: it is drawn smaller. Choose 100% to put it back.",
                Advanced = true,
                Label = "Overlay size",
                Help = "How large the overlay is drawn. It re-wraps at each step rather than "
                       + "being blown up, so bigger means more readable and not blurrier.",
                Kind = SettingKind.Choice,
                Choices = [.. ZoomLadder.Steps.Select(step => step.ToString(CultureInfo.InvariantCulture))],
                ChoiceLabel = value => ZoomLadder.Describe(Parse(value)),
                Group = OverlayGroup,
                DocsAnchor = "overlay-size",
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.Overlay.ScalePercent.ToString(CultureInfo.InvariantCulture),

                    // Snapped rather than validated, exactly as the window's zoom is and for the same reason:
                    // a hand-edited 137 should land on 125 rather than on a level no control can step off.
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
                Exercise =
                    "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                    + "open Settings › Screens › Overlay, group Overlay, and find Overlay opacity, under Show the "
                    + "overlay (the overlay must be on). With Elite in front, set it to 0.2: the cockpit shows through "
                    + "the overlay. Set it to 1: the overlay is solid. A value below 0.2 becomes 0.2. Steps are 0.05.",
                Advanced = true,
                Label = "Overlay opacity",
                Help = "How much cockpit shows through it. 1 is solid.",
                Kind = SettingKind.Number,
                Step = 0.05,

                // Not down to nothing.
                Minimum = 0.2,
                Maximum = 1,
                Group = OverlayGroup,
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
                DocsAnchor = "overlay-fullscreen",
                Binding = new SettingBinding
                {
                    Read = _ => (display ?? DefaultDisplay)(),
                },
            },
            .. HotkeyRows(
                ShowOverlayHotkeyKey,
                ShowOverlayButtonKey,
                "Show or hide the overlay",
                "show-overlay",
                s => s.Hotkeys.ShowOverlay,
                (s, v) => s with { Hotkeys = s.Hotkeys with { ShowOverlay = v } },
                systemWide: true,
                exercise: new(
                    "Settings › Screens › Overlay, group Overlay",
                    "Ctrl+Alt+O",
                    "the Show the overlay row flips between on and off. With it on and Elite in front, the mini panel "
                        + "is drawn over the game; with it off the overlay is gone.")),
            .. HotkeyRows(
                MoveOverlayHotkeyKey,
                MoveOverlayButtonKey,
                "Move the overlay",
                "move-overlay",
                s => s.Hotkeys.MoveOverlay,
                (s, v) => s with { Hotkeys = s.Hotkeys with { MoveOverlay = v } },
                systemWide: true,
                exercise: new(
                    "Settings › Screens › Overlay, group Overlay",
                    "Ctrl+Alt+M",
                    "with Show the overlay on, the overlay appears with a border, even when Elite is not in front, "
                        + "and takes the pointer so it can be dragged. Drag it and let go: it stays there, the border goes "
                        + "and the pointer passes through again. Pressing it again with nothing dragged also leaves the "
                        + "mode. If the strip has been lost off every screen, the key brings it back, centred on the "
                        + "screen Elite is on, with the border up and ready to drag.")),
        ],
    };

    /// <summary>The Commander's own <c>DisplaySettings.xml</c>, read fresh every time the row is drawn.</summary>
    private static string DefaultDisplay() => EliteDisplay.Describe(new DiskFileSystem(), EliteDisplay.DefaultPath());

    private static int Parse(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
            ? percent
            : ZoomLadder.Default;

    /// <summary>
    /// A hotkey row and the stick button its control also binds; both protected. Internal so another
    /// capability's hotkeys can share it (#189).
    /// </summary>
    internal static SettingRow[] HotkeyRows(
        string key,
        string buttonKey,
        string label,
        string anchor,
        Func<Configuration.D47Settings, string?> read,
        Func<Configuration.D47Settings, string?, Configuration.D47Settings> write,
        bool systemWide = false,
        HotkeyExercise? exercise = null)
    {
        var slot = Hotas.ButtonSlots.All.Single(slot => slot.Key == buttonKey);

        var keyScope = systemWide
            ? "The key works while Elite has the foreground."
            : "The key works only while the D47 window has focus.";
        var buttonScope = systemWide
            ? "The button works while Elite has the foreground."
            : "The button works only while the D47 window is active.";

        return
        [
            new SettingRow
            {
                Key = key,
                Exercise = exercise is null
                    ? null
                    : AdvancedPrefix + exercise.Where + ", and find " + label + ". The row shows a key slot (default "
                      + exercise.DefaultKey + "), a stick slot that stays hidden while no controller is attached and "
                      + "nothing is bound to it, and the BIND and CLEAR buttons. Press the key: " + exercise.Effect + " "
                      + keyScope + " To rebind, press BIND: the key slot reads PRESS A KEY…. Press another "
                      + "combination and the slot shows it in capitals; that combination now does the action and the "
                      + "old one no longer does. CLEAR empties both slots and the slots read UNBOUND.",
                Advanced = true,
                Label = label,
                Help = "Press a key combination or a stick button to bind it. Bind a key, a stick button, or "
                       + "one of each; giving it the same kind twice replaces that one. Clear removes both and "
                       + "leaves the action unbound."
                       + (systemWide
                           ? " Registered system-wide, so the key and the button both work while Elite has the "
                             + "foreground, which is when they are needed."
                           : " The key and the button both work only while the D47 window has focus."),
                Kind = SettingKind.Hotkey,
                DefaultDisplay = "(unbound)",
                DocsAnchor = anchor,
                Protected = true,

                // The factory already knew this; the row did not.
                SystemWide = systemWide,
                AlsoBinds = buttonKey,
                Binding = new SettingBinding { Read = read, Write = write },
            },
            new SettingRow
            {
                Key = buttonKey,
                Exercise = exercise is null
                    ? null
                    : AdvancedPrefix + exercise.Where + ", and find " + label + ". The stick slot of that row is this "
                      + "setting. It needs a stick or throttle attached; with none, the slot stays hidden until a "
                      + "button is bound. Press BIND: the stick slot reads PRESS A BUTTON… (OR A BUTTON… while a key is "
                      + "listened for too). Press a button that springs back and the slot shows its name. Press that "
                      + "button: " + exercise.Effect + " " + buttonScope + " CLEAR empties the slots and the "
                      + "button then does nothing.",
                Advanced = true,
                Label = label + " button",
                Help =
                    "The same thing on your stick or throttle: press the button you want and D47 works out "
                    + "which one it was. It sits beside the key rather than replacing it. Needs a button "
                    + "that springs back, not a switch that stays where you put it.",
                Kind = SettingKind.HotasButton,
                DocsAnchor = anchor,

                // Held by the key row's control rather than drawn on its own.
                DrawnElsewhere = true,
                Protected = true,
                Binding = new SettingBinding
                {
                    Read = slot.Read,
                    Write = (s, v) => slot.Write(s, string.IsNullOrWhiteSpace(v) ? null : v),
                },
            },
        ];
    }
}
