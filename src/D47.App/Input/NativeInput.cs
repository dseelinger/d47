using System.Runtime.InteropServices;

namespace D47.App.Input;

/// <summary>
/// The <c>SendInput</c> declarations shared by <see cref="ChordSender"/> and <see cref="ScancodeInjector"/>, and
/// Windows' last-input time.
/// </summary>
internal static class NativeInput
{
    public const uint InputKeyboard = 1;

    public const uint KeyEventExtendedKey = 0x0001;
    public const uint KeyEventKeyUp = 0x0002;

    public const uint MapVirtualKeyToScancode = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct InputEvent
    {
        public uint Type;
        public InputUnion Data;
    }

    /// <summary>The mouse member sets the union's size.</summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scancode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    private static long _lastSent = -1;

    /// <summary>The tick count when d47 last sent input, or null before it has.</summary>
    public static uint? LastSent => Interlocked.Read(ref _lastSent) is var sent and >= 0 ? (uint)sent : null;

    /// <summary>Sends the inputs and records when, so input d47 sent is not taken for the Commander's.</summary>
    public static uint SendInput(uint count, InputEvent[] inputs, int size)
    {
        // Before as well as after, so a tick between the injection and the stamp still sees it as d47's.
        Interlocked.Exchange(ref _lastSent, unchecked((uint)Environment.TickCount));
        var sent = Inject(count, inputs, size);
        Interlocked.Exchange(ref _lastSent, unchecked((uint)Environment.TickCount));
        return sent;
    }

    /// <summary>The tick count of the last keyboard or mouse input anywhere in Windows, or null when it cannot be read.</summary>
    public static uint? LastInput()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };

        return GetLastInputInfo(ref info) ? info.Time : null;
    }

    [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
    private static extern uint Inject(uint count, InputEvent[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint code, uint mapType);
}
