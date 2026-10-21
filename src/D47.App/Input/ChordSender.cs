using System.Runtime.InteropServices;
using D47.Core.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Presses a <see cref="KeyChord"/> in whatever window is in front, checking nothing about Elite. Blocks for the
/// chord's tap hold, so it must never run on the tick thread.
/// </summary>
public sealed class ChordSender(ILogger logger)
{
    private const uint InputKeyboard = 1;

    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;

    private const uint MapVirtualKeyToScancode = 0;

    private readonly List<InputStep> _recorded = [];

    /// <summary>Counts and records presses but never calls <c>SendInput</c>.</summary>
    public bool DryRun { get; init; }

    /// <summary>How many chords were pressed.</summary>
    public int Presses { get; private set; }

    /// <summary>Every step a dry run would have sent, in order.</summary>
    public IReadOnlyList<InputStep> Recorded => _recorded;

    /// <summary>Presses the chord once.</summary>
    /// <returns>Why nothing was pressed, or null when it was.</returns>
    public string? Press(KeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);

        if (KeyChord.Problem(chord) is { } problem)
        {
            return problem;
        }

        var steps = chord.Steps();

        Presses++;

        if (DryRun)
        {
            _recorded.AddRange(steps);
            return null;
        }

        var batch = new List<Input>();

        foreach (var step in steps)
        {
            if (step.Kind == InputStepKind.Delay)
            {
                if (!Flush(batch, chord, steps))
                {
                    return "Windows refused the keystroke.";
                }

                Thread.Sleep(step.Delay);
                continue;
            }

            if (step.Kind is InputStepKind.KeyDown or InputStepKind.KeyUp)
            {
                batch.Add(Key(step));
            }
        }

        return Flush(batch, chord, steps) ? null : "Windows refused the keystroke.";
    }

    /// <summary>Sends the batch and empties it; after a short send, lets go of every key the chord presses.</summary>
    private bool Flush(List<Input> batch, KeyChord chord, IReadOnlyList<InputStep> steps)
    {
        if (batch.Count == 0)
        {
            return true;
        }

        var count = SendInput((uint)batch.Count, [.. batch], Marshal.SizeOf<Input>());

        if (count == batch.Count)
        {
            batch.Clear();
            return true;
        }

        logger.LogWarning(
            "SendInput sent {Sent} of {Expected} events (error {Error}) for {Chord}",
            count,
            batch.Count,
            Marshal.GetLastWin32Error(),
            chord.Describe());

        // A Windows key left down turns every later keystroke into a shortcut.
        Input[] up =
        [
            .. steps
                .Where(step => step.Kind == InputStepKind.KeyDown)
                .Reverse()
                .Select(step => Key(step with { Kind = InputStepKind.KeyUp })),
        ];

        SendInput((uint)up.Length, up, Marshal.SizeOf<Input>());

        return false;
    }

    private static Input Key(InputStep step) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = (ushort)step.Code,
                Scancode = (ushort)MapVirtualKey(step.Code, MapVirtualKeyToScancode),
                Flags = (step.Extended ? KeyEventExtendedKey : 0) | (step.Kind == InputStepKind.KeyUp ? KeyEventKeyUp : 0),
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
