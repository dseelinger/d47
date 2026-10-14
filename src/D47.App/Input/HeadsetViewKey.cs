using System.Runtime.InteropServices;
using D47.Core.Hotas;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Sends Shift+Win+D, Virtual Desktop's shortcut between its desktop view and SteamVR, and no other key.
/// Virtual Desktop registers it with <c>RegisterHotKey</c>, so it is heard whatever window is in front; this
/// checks nothing about Elite.
/// </summary>
public sealed class HeadsetViewKey(IHeadsetViewSource view, ILogger<HeadsetViewKey> logger)
{
    private const uint InputKeyboard = 1;

    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;

    private const ushort VirtualKeyLeftShift = 0xA0;
    private const ushort VirtualKeyLeftWindows = 0x5B;
    private const ushort VirtualKeyD = 0x44;

    private const uint MapVirtualKeyToScancode = 0;

    /// <summary>Counts presses but never calls <c>SendInput</c>.</summary>
    public bool DryRun { get; init; }

    /// <summary>How many times the shortcut was sent.</summary>
    public int Presses { get; private set; }

    /// <summary>Presses the shortcut unless the view is unreadable or already <paramref name="wanted"/>.</summary>
    /// <returns>Why nothing was pressed when that is a refusal, or null.</returns>
    private string? Press(HeadsetView wanted, out bool sent)
    {
        sent = false;

        var showing = view.Read();

        if (showing == HeadsetView.Unknown)
        {
            return "Virtual Desktop stopped streaming, so I left the headset view alone.";
        }

        if (showing == wanted)
        {
            logger.LogInformation("The headset is already showing {View}, so Shift+Win+D was not pressed", wanted);
            return null;
        }

        Presses++;

        if (DryRun)
        {
            sent = true;
            return null;
        }

        Input[] down =
        [
            Key(VirtualKeyLeftShift, 0),
            Key(VirtualKeyLeftWindows, KeyEventExtendedKey),
            Key(VirtualKeyD, 0),
        ];

        Input[] up =
        [
            Key(VirtualKeyD, KeyEventKeyUp),
            Key(VirtualKeyLeftWindows, KeyEventExtendedKey | KeyEventKeyUp),
            Key(VirtualKeyLeftShift, KeyEventKeyUp),
        ];

        var count = SendInput((uint)(down.Length + up.Length), [.. down, .. up], Marshal.SizeOf<Input>());

        if (count == down.Length + up.Length)
        {
            sent = true;
            return null;
        }

        logger.LogWarning(
            "SendInput sent {Sent} of {Expected} events (error {Error}) for Shift+Win+D",
            count,
            down.Length + up.Length,
            Marshal.GetLastWin32Error());

        // A Windows key left down turns every later keystroke into a shortcut.
        SendInput((uint)up.Length, up, Marshal.SizeOf<Input>());

        return "Windows refused the keystroke.";
    }

    /// <summary>
    /// Carries out one reconcile for a headset view, the press or what it has to say, and tells the reconciler
    /// whether a press was sent.
    /// </summary>
    /// <returns>A sentence to say, or null.</returns>
    public string? CarryOut(PendingReconcile reconcile, SwitchReconciler reconciler)
    {
        if (reconcile.Say is { } say)
        {
            return say;
        }

        if (reconcile.View is not { } wanted)
        {
            return null;
        }

        var refused = Press(wanted, out var sent);

        reconciler.HeadsetPressed(reconcile.Switch, wanted, sent);

        logger.LogInformation(
            "Switch {Name} set the headset view to {View}: {Outcome}",
            reconcile.Switch,
            wanted,
            refused ?? "done");

        return refused is null ? null : $"I could not change the headset to {reconcile.Label} from {reconcile.Switch}. {refused}";
    }

    private static Input Key(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Scancode = (ushort)MapVirtualKey(virtualKey, MapVirtualKeyToScancode),
                Flags = flags,
                Time = 0,
                ExtraInfo = 0,
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    /// <summary>The mouse member is never sent; it is here because it sets the union's size.</summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scancode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);
}
