using System.Runtime.InteropServices;
using D47.Core.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// The only thing in d47 that sends input to the game (architecture.md D4).
/// <para>
/// <b>Scancodes, not virtual keys.</b> Elite reads the keyboard through DirectInput, which
/// sees scancodes and ignores a virtual-key injection entirely — the failure is that nothing
/// happens, with no error anywhere to say why.
/// </para>
/// <para>
/// <b>No low-level keyboard hook, ever.</b> A <c>WH_KEYBOARD_LL</c> hook is a global input
/// chokepoint, and a stall in d47 becomes a stall in the Commander's controls. Nothing here
/// listens; it only sends.
/// </para>
/// <para>
/// <b><see cref="ReleaseAll"/> is unconditional</b> — in a finally around every send, on focus
/// loss, and on exit. A stranded key here is a throttle that will not stop.
/// </para>
/// <para>
/// <b>Two deliberate widenings of "scancodes only", each with its reason.</b> Mouse buttons go
/// through the same <c>SendInput</c> call, because Elite's own default keyboard preset binds
/// the fire groups to <c>Mouse_1</c> and <c>Mouse_2</c> — a keyboard-only injector cannot fire
/// the discovery scanner on the setup most Commanders start with. And chat text uses
/// <c>KEYEVENTF_UNICODE</c>, because a scancode is a physical key position and the character it
/// produces depends on the Commander's layout: sending a system name by scancode types
/// something else entirely on AZERTY. Unicode injection is layout-independent and Elite's chat
/// field accepts it. Neither widening touches the three rules above.
/// </para>
/// </summary>
public sealed class ScancodeInjector(IEliteWindow window, ILogger<ScancodeInjector> logger) : IGameInput, IDisposable
{
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;

    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventScancode = 0x0008;

    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;

    private const uint MapVirtualKeyToScancode = 0;

    /// <summary>The two side buttons, which <c>SendInput</c> distinguishes by this data word.</summary>
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    private readonly Lock _gate = new();

    /// <summary>Everything currently down, newest last, so it can be released in reverse.</summary>
    private readonly List<InputStep> _held = [];

    private readonly SemaphoreSlim _sending = new(1, 1);

    /// <summary>
    /// Composes and records but never calls <c>SendInput</c>. The dry run architecture.md §8
    /// names: the real path is exercised to the syscall and stops there.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>What a dry run composed, for a test or the diagnostics card to read back.</summary>
    public IReadOnlyList<InputStep> LastSequence { get; private set; } = [];

    public bool IsGameRunning => window.IsRunning;

    public bool IsGameForeground => window.IsForeground;

    public async Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        CancellationToken cancellationToken = default)
    {
        if (steps.Count == 0)
        {
            return new InjectionResult(InjectionOutcome.NothingToSend, "There was nothing to press.");
        }

        if (!window.IsRunning)
        {
            return new InjectionResult(InjectionOutcome.GameNotFound, "Elite is not running.");
        }

        // Checked here rather than at each call site: the number of things that send input
        // grows across this phase, and a rule enforced per caller is a rule that gets missed.
        if (!window.IsForeground)
        {
            return new InjectionResult(
                InjectionOutcome.NotForeground,
                "Elite is not the window in front, so I did not send that.");
        }

        // One sequence at a time. Two overlapping sends interleave their key-downs and can
        // leave a modifier held by one while the other releases it.
        await _sending.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            LastSequence = steps;

            foreach (var step in steps)
            {
                // Re-checked between steps. A hold can span a second, and the Commander
                // alt-tabbing mid-hold must not carry on pressing keys into whatever is now in
                // front. The finally below then lets go of what is down.
                if (step.Kind != InputStepKind.Delay && !window.IsForeground)
                {
                    logger.LogInformation("Elite lost the foreground mid-sequence; stopping");
                    return new InjectionResult(
                        InjectionOutcome.NotForeground,
                        "Elite stopped being the window in front part-way through, so I stopped.");
                }

                if (step.Kind == InputStepKind.Delay)
                {
                    await WaitAsync(step.Delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!Send(step))
                {
                    return new InjectionResult(InjectionOutcome.Failed, "Windows refused the keystroke.");
                }
            }

            return InjectionResult.Ok;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal end to a hold, not a failure — but it still has to let
            // go of the key, which the finally does.
            return new InjectionResult(InjectionOutcome.Failed, "That was interrupted.");
        }
        finally
        {
            ReleaseAll();
            _sending.Release();
        }
    }

    /// <summary>
    /// Lets go of everything, in reverse order, whether or not anything is held. Safe to call
    /// from anywhere at any time, which is the property that lets it be called from a finally,
    /// a focus-loss handler and a shutdown path without any of them coordinating.
    /// </summary>
    public void ReleaseAll()
    {
        InputStep[] releasing;

        lock (_gate)
        {
            if (_held.Count == 0)
            {
                return;
            }

            releasing = [.. _held];
            _held.Clear();
        }

        for (var i = releasing.Length - 1; i >= 0; i--)
        {
            var step = releasing[i];

            var release = step.Kind == InputStepKind.MouseDown
                ? new InputStep(InputStepKind.MouseUp, step.Code, step.Extended)
                : new InputStep(InputStepKind.KeyUp, step.Code, step.Extended);

            // Deliberately not tracked and not failure-checked. This is the path that runs when
            // something has already gone wrong, and its whole job is to leave nothing down.
            Emit(release);
        }

        logger.LogDebug("Released {Count} held inputs", releasing.Length);
    }

    /// <summary>
    /// Delays at or below this are waited out on a stopwatch rather than the timer. Windows'
    /// timer runs on a grain of about fifteen milliseconds, so a thirty-millisecond hold asked of
    /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/> lands anywhere up to forty-five — and
    /// the galaxy-map camera brush is a hold where the difference is the selector staying on the
    /// star or not. Spinning a thread for under fifty milliseconds costs nothing worth noticing;
    /// spinning it for a 1.2-second hold would, so longer waits still go to the timer.
    /// </summary>
    private static readonly TimeSpan PreciseBelow = TimeSpan.FromMilliseconds(50);

    private static async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay > PreciseBelow)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return;
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < delay)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.SpinWait(200);
        }
    }

    private bool Send(InputStep step)
    {
        lock (_gate)
        {
            if (step.Kind is InputStepKind.KeyDown or InputStepKind.MouseDown)
            {
                _held.Add(step);
            }
            else if (step.Kind is InputStepKind.KeyUp or InputStepKind.MouseUp)
            {
                _held.RemoveAll(held =>
                    held.Code == step.Code &&
                    (held.Kind == InputStepKind.KeyDown) == (step.Kind == InputStepKind.KeyUp));
            }
        }

        return Emit(step);
    }

    private bool Emit(InputStep step)
    {
        if (DryRun)
        {
            return true;
        }

        var inputs = Compose(step);

        if (inputs.Length == 0)
        {
            return true;
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());

        if (sent == inputs.Length)
        {
            return true;
        }

        // UIPI blocks injection into a window running at a higher integrity level, which is
        // what this looks like when Elite has been started as administrator and d47 has not.
        logger.LogWarning(
            "SendInput sent {Sent} of {Expected} events (error {Error}) for {Step}",
            sent,
            inputs.Length,
            Marshal.GetLastWin32Error(),
            step);

        return false;
    }

    private static Input[] Compose(InputStep step)
    {
        switch (step.Kind)
        {
            case InputStepKind.KeyDown:
            case InputStepKind.KeyUp:
            {
                // The virtual key is translated to the scancode here and then discarded: what
                // goes on the wire is the physical key position, which is the only thing Elite
                // reads.
                var scancode = (ushort)MapVirtualKey(step.Code, MapVirtualKeyToScancode);

                if (scancode == 0)
                {
                    return [];
                }

                var flags = KeyEventScancode;

                if (step.Extended)
                {
                    flags |= KeyEventExtendedKey;
                }

                if (step.Kind == InputStepKind.KeyUp)
                {
                    flags |= KeyEventKeyUp;
                }

                return [Keyboard(0, scancode, flags)];
            }

            case InputStepKind.MouseDown:
            case InputStepKind.MouseUp:
            {
                var up = step.Kind == InputStepKind.MouseUp;

                var (flags, data) = step.Code switch
                {
                    1 => (up ? MouseEventLeftUp : MouseEventLeftDown, 0u),
                    2 => (up ? MouseEventRightUp : MouseEventRightDown, 0u),
                    3 => (up ? MouseEventMiddleUp : MouseEventMiddleDown, 0u),
                    4 => (up ? MouseEventXUp : MouseEventXDown, XButton1),
                    5 => (up ? MouseEventXUp : MouseEventXDown, XButton2),
                    _ => (0u, 0u),
                };

                return flags == 0 ? [] : [Mouse(flags, data)];
            }

            case InputStepKind.Text:
            {
                if (string.IsNullOrEmpty(step.Text))
                {
                    return [];
                }

                // One down/up pair per UTF-16 unit, which keeps surrogate pairs intact without
                // the caller having to know about them.
                var inputs = new List<Input>(step.Text.Length * 2);

                foreach (var unit in step.Text)
                {
                    inputs.Add(Keyboard(0, unit, KeyEventUnicode));
                    inputs.Add(Keyboard(0, unit, KeyEventUnicode | KeyEventKeyUp));
                }

                return [.. inputs];
            }

            default:
                return [];
        }
    }

    private static Input Keyboard(ushort virtualKey, ushort scancode, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Scancode = scancode,
                Flags = flags,
                Time = 0,
                ExtraInfo = 0,
            },
        },
    };

    private static Input Mouse(uint flags, uint data) => new()
    {
        Type = InputMouse,
        Data = new InputUnion
        {
            Mouse = new MouseInput
            {
                X = 0,
                Y = 0,
                Data = data,
                Flags = flags,
                Time = 0,
                ExtraInfo = 0,
            },
        },
    };

    public void Dispose()
    {
        // The last chance to let go, and the reason this implements IDisposable at all.
        ReleaseAll();
        _sending.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

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
