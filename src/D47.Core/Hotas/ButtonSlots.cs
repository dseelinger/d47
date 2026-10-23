using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.Core.Hotas;

/// <summary>One stored stick button: its settings key, the name the found-device card gives it, and how to read and write it.</summary>
public sealed record ButtonSlot(
    string Key, string Named, Func<D47Settings, string?> Read, Func<D47Settings, string?, D47Settings> Write);

/// <summary>Every stick button the settings store.</summary>
public static class ButtonSlots
{
    public static IReadOnlyList<ButtonSlot> All { get; } =
    [
        new(ListeningCapability.PushToTalkButtonKey, "push-to-talk",
            s => s.Listening.PushToTalkButton,
            (s, v) => s with { Listening = s.Listening with { PushToTalkButton = v } }),
        new(ListeningCapability.CancelButtonKey, "cancel",
            s => s.Speech.CancelButton,
            (s, v) => s with { Speech = s.Speech with { CancelButton = v } }),
        new(InterfaceCapability.OpenSettingsButtonKey, "open settings",
            s => s.Hotkeys.OpenSettingsButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { OpenSettingsButton = v } }),
        new(InterfaceCapability.FocusAskButtonKey, "focus ask",
            s => s.Hotkeys.FocusAskButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { FocusAskButton = v } }),
        new(InterfaceCapability.FindButtonKey, "find",
            s => s.Hotkeys.FindButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { FindButton = v } }),
        new(InterfaceCapability.ShowOverlayButtonKey, "show overlay",
            s => s.Hotkeys.ShowOverlayButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { ShowOverlayButton = v } }),
        new(InterfaceCapability.MoveOverlayButtonKey, "move overlay",
            s => s.Hotkeys.MoveOverlayButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { MoveOverlayButton = v } }),
        new(VrCapability.ZoomInButtonKey, "zoom headset in",
            s => s.Hotkeys.ZoomHeadsetInButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { ZoomHeadsetInButton = v } }),
        new(VrCapability.ZoomOutButtonKey, "zoom headset out",
            s => s.Hotkeys.ZoomHeadsetOutButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { ZoomHeadsetOutButton = v } }),
        new(VrCapability.ResetZoomButtonKey, "reset headset zoom",
            s => s.Hotkeys.ResetHeadsetZoomButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { ResetHeadsetZoomButton = v } }),
        new(VrCapability.ResizeButtonKey, "resize headset panel",
            s => s.Hotkeys.ResizeHeadsetPanelButton,
            (s, v) => s with { Hotkeys = s.Hotkeys with { ResizeHeadsetPanelButton = v } }),
    ];
}
