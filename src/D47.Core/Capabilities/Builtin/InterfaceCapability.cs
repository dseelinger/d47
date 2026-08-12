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

    public const string OpenSettingsHotkeyKey = "hotkeys.openSettings";

    public const string FocusAskHotkeyKey = "hotkeys.focusAsk";

    public static CapabilityDescriptor Create() => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Interface",
        Summary = "Choose d47's theme and the keys that reach it.",
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
        ],
    };

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
        Func<Configuration.D47Settings, string?, Configuration.D47Settings> write) => new()
    {
        Key = key,
        Label = label,
        Help = "Press the key combination to bind it. Clear it to leave the action unbound.",
        Kind = SettingKind.Hotkey,
        DefaultDisplay = "(unbound)",
        DocsAnchor = anchor,
        Protected = true,
        Binding = new SettingBinding { Read = read, Write = write },
    };
}
