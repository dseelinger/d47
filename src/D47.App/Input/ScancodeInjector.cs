using System.Runtime.InteropServices;
using D47.Core.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>The only thing in d47 that sends input to the game.</summary>
/// <param name="observer">The instrument watching what is sent, or null (#365).</param>
public sealed class ScancodeInjector(
    IEliteWindow window,
    ILogger<ScancodeInjector> logger,
    Func<D47.Core.Journal.GameStatus>? status = null,
    IInputStepObserver? observer = null) : IGameInput, IDisposable
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

    /// <summary>Composes and records but never calls <c>SendInput</c>.</summary>
    public bool DryRun { get; init; }

    /// <summary>What a dry run composed, for a test or the diagnostics card to read back.</summary>
    public IReadOnlyList<InputStep> LastSequence { get; private set; } = [];

    public bool IsGameRunning => window.IsRunning;

    public bool IsGameForeground => window.IsForeground;

    /// <summary>
    /// Whether the Commander is in the game world right now (#242): in something per the status flags.
    /// </summary>
    private bool Online()
    {
        if (status is null)
        {
            return true;
        }

        // The flags alone, deliberately: not <see cref="GameStatus.IsLiveAt"/>, which also asks that the file
        // was written in the last ten minutes. **Elite does not rewrite Status.json on a heartbeat.** It
        // writes it when something changes, and for a Commander docked and away from the keyboard nothing
        // does — so the file's own stamp ages while the game sits there perfectly alive.
        return status().CommanderIsInTheGame;
    }

    /// <summary>Opens a trace for one named caller, or null when nothing is watching (#365).</summary>
    public IInputTrace? Trace(string caller) => observer?.Open(caller);

    public Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        CancellationToken cancellationToken = default) =>
        SendAsync(steps, null, cancellationToken);

    public async Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        IInputTrace? trace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        // A caller with several sends and evidence of its own hands its trace in; everything else — the
        // launch walk, a Commander's macro, an autonomous honk — is traced one sequence at a time with no
        // code of its own, which is the reason for hanging this here rather than on the plot (#365).
        var own = trace is null ? observer?.Open(IInputStepObserver.Anonymous) : null;

        try
        {
            var result = await Run(steps, trace ?? own, cancellationToken).ConfigureAwait(false);

            // Only a trace this send opened gets the verdict written into it: a caller's own trace spans
            // several sends and ends on the caller's verdict, not on one send's.
            own?.Verdict(result.Outcome.ToString(), result.Reason);

            return result;
        }
        finally
        {
            own?.Dispose();
        }
    }

    private async Task<InjectionResult> Run(
        IReadOnlyList<InputStep> steps,
        IInputTrace? watching,
        CancellationToken cancellationToken)
    {
        if (steps.Count == 0)
        {
            return new InjectionResult(InjectionOutcome.NothingToSend, "There was nothing to press.");
        }

        if (!window.IsRunning)
        {
            return new InjectionResult(InjectionOutcome.GameNotFound, "Elite is not running.");
        }

        // Running is not the same as being in the game (#242).
        if (!Online())
        {
            return new InjectionResult(
                InjectionOutcome.NotOnline,
                "Elite is up, but you are not in the game yet, so I did not send that.");
        }

        // Checked here rather than at each call site: the number of things that send input grows across this
        // phase, and a rule enforced per caller is a rule that gets missed.
        if (!window.IsForeground)
        {
            return new InjectionResult(
                InjectionOutcome.NotForeground,
                "Elite is not the window in front, so I did not send that.");
        }

        // One sequence at a time.
        await _sending.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            LastSequence = steps;

            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];

                // Re-checked between steps.
                if (step.Kind != InputStepKind.Delay && !window.IsForeground)
                {
                    logger.LogInformation("Elite lost the foreground mid-sequence; stopping");
                    return new InjectionResult(
                        InjectionOutcome.NotForeground,
                        "Elite stopped being the window in front part-way through, so I stopped.");
                }

                // And the game itself, for the same reason (#242): quitting to the menu keeps Elite in front,
                // and a macro must not carry on typing into it.
                if (step.Kind != InputStepKind.Delay && !Online())
                {
                    logger.LogInformation("The Commander left the game mid-sequence; stopping");
                    return new InjectionResult(
                        InjectionOutcome.NotOnline,
                        "You left the game part-way through, so I stopped.");
                }

                if (step.Kind == InputStepKind.Delay)
                {
                    // The wait watches for itself (#206).
                    if (await WaitAsync(step.Delay, cancellationToken).ConfigureAwait(false) is { } stopped)
                    {
                        return stopped;
                    }

                    Report(watching, index, step);
                    continue;
                }

                if (!Send(step))
                {
                    return new InjectionResult(InjectionOutcome.Failed, "Windows refused the keystroke.");
                }

                Report(watching, index, step);
            }

            return InjectionResult.Ok;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal end to a hold, not a failure — but it still has to let go of the key,
            // which the finally does.
            return new InjectionResult(InjectionOutcome.Failed, "That was interrupted.");
        }
        finally
        {
            ReleaseAll();
            _sending.Release();
        }
    }

    /// <summary>One step, to whatever is watching (#365).</summary>
    private void Report(IInputTrace? watching, int index, InputStep step)
    {
        // The check first, so a run with nothing watching does not pay for the foreground call this would
        // otherwise make per step.
        if (watching is null)
        {
            return;
        }

        watching.Stepped(new InputStepReport(index, step, DateTimeOffset.Now, window.IsForeground));
    }

    /// <summary>Lets go of everything, in reverse order, whether or not anything is held.</summary>
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

            // Deliberately not tracked and not failure-checked.
            Emit(release);
        }

        logger.LogDebug("Released {Count} held inputs", releasing.Length);
    }

    /// <summary>Delays at or below this are waited out on a stopwatch rather than the timer.</summary>
    private static readonly TimeSpan PreciseBelow = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// How often a long hold looks up to see whether it is still allowed to be holding anything (#206).
    /// </summary>
    private static readonly TimeSpan WatchEvery = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Waits out one delay step, and gives up on it the moment the sequence stops being allowed to hold
    /// a key.
    /// </summary>
    private async Task<InjectionResult?> WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay > PreciseBelow)
        {
            var holding = System.Diagnostics.Stopwatch.GetTimestamp();

            while (true)
            {
                var left = delay - System.Diagnostics.Stopwatch.GetElapsedTime(holding);

                if (left <= TimeSpan.Zero)
                {
                    return null;
                }

                await Task.Delay(left < WatchEvery ? left : WatchEvery, cancellationToken).ConfigureAwait(false);

                // The same two guards the step loop keeps, asked on the same terms — a hold is a step that
                // lasts, and there is no reason for it to be the one step during which they stop being true.
                if (!window.IsForeground)
                {
                    logger.LogInformation("Elite lost the foreground mid-hold; letting go");
                    return new InjectionResult(
                        InjectionOutcome.NotForeground,
                        "Elite stopped being the window in front while I was holding a key, so I let go.");
                }

                if (!Online())
                {
                    logger.LogInformation("The Commander left the game mid-hold; letting go");
                    return new InjectionResult(
                        InjectionOutcome.NotOnline,
                        "You left the game while I was holding a key, so I let go.");
                }
            }
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        // Still an asynchronous hop, like the timer path: the foreground check between steps is what a
        // Commander alt-tabbing mid-hold relies on, and a wait that never yields would let the sequence run
        // on ahead of anything that wanted to stop it.
        await Task.Yield();

        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < delay)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.SpinWait(200);
        }

        // Nothing to watch for down here: fifty milliseconds is shorter than the alt-tab it would be watching
        // for, and the step loop asks both questions the moment this returns.
        return null;
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

        // UIPI blocks injection into a window running at a higher integrity level, which is what this looks
        // like when Elite has been started as administrator and d47 has not.
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
                // The virtual key is translated to the scancode here and then discarded: what goes on the
                // wire is the physical key position, which is the only thing Elite reads.
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

                // One down/up pair per UTF-16 unit, which keeps surrogate pairs intact without the caller
                // having to know about them.
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
