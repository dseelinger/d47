using System.Diagnostics;
using System.Runtime.InteropServices;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Whether Elite is running and in front. An interface so the injector's foreground rule can
/// be tested both ways round — the refusal is the behaviour that matters most, and it is the
/// one that cannot be observed by running the real thing.
/// </summary>
public interface IEliteWindow
{
    bool IsRunning { get; }

    bool IsForeground { get; }

    /// <summary>
    /// Puts Elite in front, and says whether it worked
    /// (docs/plans/change-requests.md item 10).
    /// <para>
    /// The answer is a result rather than a bool because three of the four outcomes need
    /// different words: not running, already there, raised, and refused by Windows. The last is
    /// the one this exists to make speakable — it is invisible from inside a game.
    /// </para>
    /// </summary>
    FocusResult Raise();

    /// <summary>
    /// Where Elite's window is on the virtual desktop, in physical pixels, or null when it cannot
    /// be found (#36).
    /// <para>
    /// Here rather than on the concrete class because the one thing that asks — the flat overlay,
    /// deciding which monitor to sit on — has to be testable on a machine with no game running and
    /// on a headless platform with one screen.
    /// </para>
    /// <para>
    /// A rectangle rather than a screen: which monitor a rectangle is on is Avalonia's question to
    /// answer, and this interface is the one place in d47 that already knows how to find Elite.
    /// </para>
    /// </summary>
    (int X, int Y, int Width, int Height)? Bounds { get; }
}

/// <summary>
/// Finding Elite's window and answering whether it is in front (architecture.md D4, rule 3).
/// <para>
/// The handle is cached because the check runs before every injected key and enumerating
/// processes at that rate is not free — but it is re-found whenever the cached handle stops
/// being a window, since Elite restarting is ordinary and a stale handle would answer "not in
/// front" forever afterwards.
/// </para>
/// </summary>
public sealed class EliteWindow(ILogger<EliteWindow> logger) : IEliteWindow
{
    /// <summary>
    /// Both are shipped: the Odyssey client and the older Horizons one. Named without the
    /// extension because that is what <see cref="Process.GetProcessesByName"/> matches on.
    /// </summary>
    private static readonly string[] ProcessNames = ["EliteDangerous64", "EliteDangerous32"];

    private nint _handle;

    /// <summary>The cached window, re-found if it has gone away. Zero when Elite is not running.</summary>
    public nint Handle
    {
        get
        {
            if (_handle != 0 && IsWindow(_handle))
            {
                return _handle;
            }

            _handle = Find();
            return _handle;
        }
    }

    public bool IsRunning => Handle != 0;

    /// <summary>
    /// Whether Elite has the foreground. <b>The one check that stands between a voice command
    /// and typing into a browser.</b> False whenever there is any doubt, including when Elite
    /// cannot be found at all.
    /// </summary>
    public bool IsForeground
    {
        get
        {
            var elite = Handle;
            return elite != 0 && GetForegroundWindow() == elite;
        }
    }

    /// <inheritdoc />
    public (int X, int Y, int Width, int Height)? Bounds
    {
        get
        {
            var elite = Handle;

            if (elite == 0 || !GetWindowRect(elite, out var rect))
            {
                return null;
            }

            return (rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
    }

    private nint Find()
    {
        foreach (var name in ProcessNames)
        {
            Process[] processes;

            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            try
            {
                foreach (var process in processes)
                {
                    var handle = process.MainWindowHandle;

                    if (handle != 0)
                    {
                        logger.LogDebug("Found Elite's window for {Process} (pid {Pid})", name, process.Id);
                        return handle;
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Asks Windows to put Elite in front.
    /// <para>
    /// <b>Windows is entitled to refuse this and usually will.</b> A process that does not
    /// already hold the foreground cannot take it, outside a short list of exemptions; the call
    /// returns false and the taskbar button flashes instead. So this works when the Commander
    /// asked from d47's own window and is refused when they asked from a browser — which is the
    /// case the feature is most for. There is no honest way around that: the tricks that defeat
    /// the rule work by faking input, and d47's one input rule is that it injects into Elite and
    /// nowhere else.
    /// </para>
    /// <para>
    /// <c>SingleInstance</c> calls the same function and discards the result, which is why that
    /// call site is not evidence this works — a second copy of d47 being launched <em>is</em>
    /// one of the exempt cases, and this is not.
    /// </para>
    /// <para>
    /// The foreground is re-read afterwards rather than trusting the return value, because it
    /// can report success for a call that only flashed.
    /// </para>
    /// </summary>
    public FocusResult Raise()
    {
        var elite = Handle;

        if (elite == 0)
        {
            return FocusResult.NotRunning;
        }

        if (GetForegroundWindow() == elite)
        {
            return FocusResult.AlreadyThere;
        }

        // Restored first if it was minimised, which is the state a window alt-tabbed away from
        // is most likely to be in. SetForegroundWindow on a minimised window raises it without
        // unminimising it.
        if (IsIconic(elite))
        {
            ShowWindow(elite, ShowRestore);
        }

        if (SetForegroundWindow(elite) && GetForegroundWindow() == elite)
        {
            return FocusResult.Raised;
        }

        // **Windows only grants the foreground to a process that already has it, or that received
        // the last input** (#27). d47 running behind Elite is neither, so the plain call is
        // refused and the shell flashes a taskbar button instead — which from inside a headset or
        // a full-screen game is no effect at all.
        //
        // Attaching this thread's input queue to the thread owning the current foreground window
        // makes the two share input state for the length of one call, and the lock does not apply
        // between them. It is the ordinary answer and it costs microseconds; the Commander's stated
        // ceiling was three seconds on top of a manual desktop round trip, so cost was never the
        // constraint here — only how often it works.
        //
        // The system setting that removes the lock outright (SPI_SETFOREGROUNDLOCKTIMEOUT) would
        // also work and is deliberately not used: it is machine-wide, it persists, and it affects
        // every application. d47 is a guest.
        if (Attached(elite, out var attachedTo))
        {
            try
            {
                if (SetForegroundWindow(elite) && GetForegroundWindow() == elite)
                {
                    logger.LogInformation("Elite was brought forward by attaching to the foreground thread");

                    return FocusResult.Raised;
                }
            }
            finally
            {
                AttachThreadInput(GetCurrentThreadId(), attachedTo, false);
            }
        }

        // Still refused. Said out loud rather than logged, because these are workarounds against a
        // lock Microsoft has tightened before and may tighten again — the honest sentence has to
        // stay behind them.
        logger.LogInformation("Windows refused to bring Elite forward, with and without attaching");

        return FocusResult.Refused;
    }

    private const int ShowRestore = 9;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    /// <summary>
    /// Attaches this thread's input queue to whichever thread owns the foreground window, so that
    /// one <see cref="SetForegroundWindow"/> is allowed through (#27).
    /// <para>
    /// False when there is nothing to attach to, or when the foreground window is already ours —
    /// attaching a thread to itself is not a thing, and if we are the foreground the plain call
    /// would have worked.
    /// </para>
    /// </summary>
    private static bool Attached(nint elite, out uint attachedTo)
    {
        attachedTo = 0;

        var front = GetForegroundWindow();

        if (front == 0 || front == elite)
        {
            return false;
        }

        var owner = GetWindowThreadProcessId(front, out _);
        var mine = GetCurrentThreadId();

        if (owner == 0 || owner == mine)
        {
            return false;
        }

        if (!AttachThreadInput(mine, owner, true))
        {
            return false;
        }

        attachedTo = owner;

        return true;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);
}
