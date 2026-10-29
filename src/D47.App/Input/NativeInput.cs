using System.Runtime.InteropServices;

namespace D47.App.Input;

/// <summary>The <c>SendInput</c> declarations shared by <see cref="ChordSender"/> and <see cref="ScancodeInjector"/>.</summary>
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

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, InputEvent[] inputs, int size);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint code, uint mapType);
}
