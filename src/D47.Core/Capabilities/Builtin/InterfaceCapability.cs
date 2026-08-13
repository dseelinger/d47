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

    public const string ZoomKey = "ui.zoom";

    public const string OpenSettingsHotkeyKey = "hotkeys.openSettings";

    public const string FocusAskHotkeyKey = "hotkeys.focusAsk";

    public const string ReanchorHotkeyKey = "hotkeys.reanchor";

    public static CapabilityDescriptor Create() => new()
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
                ReanchorHotkeyKey,
                "Re-anchor the headset panels",
                "reanchor",
                s => s.Hotkeys.Reanchor,
                (s, v) => s with { Hotkeys = s.Hotkeys with { Reanchor = v } },
                systemWide: true),
        ],
    };

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
