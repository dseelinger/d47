using System.Diagnostics;
using System.Runtime.InteropServices;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>Whether Elite is running and in front.</summary>
public interface IEliteWindow
{
    bool IsRunning { get; }

    bool IsForeground { get; }

    /// <summary>Puts Elite in front, and says whether it worked (docs/plans/change-requests.md item 10).</summary>
    FocusResult Raise();

    /// <summary>
    /// Where Elite's window is on the virtual desktop, in physical pixels, or null when it cannot be
    /// found (#36).
    /// </summary>
    (int X, int Y, int Width, int Height)? Bounds { get; }
}

/// <summary>Finding Elite's window and answering whether it is in front.</summary>
public sealed class EliteWindow(ILogger<EliteWindow> logger) : IEliteWindow
{
    /// <summary>Both are shipped: the Odyssey client and the older Horizons one.</summary>
    private static readonly string[] ProcessNames = ["EliteDangerous64", "EliteDangerous32"];

    private nint _handle;

    /// <summary>The cached window, re-found if it has gone away.</summary>
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

    /// <summary>Whether Elite has the foreground.</summary>
    public bool IsForeground
    {
        get
        {
            var elite = Handle;
            return elite != 0 && BelongsToElite(GetForegroundWindow(), elite);
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

    /// <summary>Asks Windows to put Elite in front.</summary>
    public FocusResult Raise()
    {
        var elite = Handle;

        if (elite == 0)
        {
            return FocusResult.NotRunning;
        }

        var already = GetForegroundWindow();

        if (BelongsToElite(already, elite))
        {
            // The likeliest shape of #107's VR conjecture is not a raise landing on a different window — it
            // is Elite already in front through one.
            if (already != elite)
            {
                logger.LogInformation(
                    "Elite already holds the foreground, presenting window 0x{Front:X} rather than the cached 0x{Elite:X}",
                    already,
                    elite);
            }

            return FocusResult.AlreadyThere;
        }

        // Restored first if it was minimised, which is the state a window alt-tabbed away from is most likely
        // to be in.
        if (IsIconic(elite))
        {
            ShowWindow(elite, ShowRestore);
        }

        if (SetForegroundWindow(elite) && Landed(elite))
        {
            logger.LogInformation("Elite was brought forward by the plain SetForegroundWindow");

            return FocusResult.Raised;
        }

        // **Windows only grants the foreground to a process that already has it, or that received the last
        // input** (#27). d47 running behind Elite is neither, so the plain call is refused and the shell
        // flashes a taskbar button instead — which from inside a headset or a full-screen game is no effect
        // at all.
        var front = GetForegroundWindow();
        var holder = DescribeHolder(front);

        if (TryAttach(front, elite, out var attachedTo, out var attachOutcome))
        {
            // The permission is consumed by the calls themselves, so the queues are detached before the
            // landing is verified: attached threads share input state, and holding the attachment across a
            // poll would stall input for the very window the Commander is working in — and, through the Elite
            // half, for the game.
            try
            {
                SetForegroundWindow(elite);
                BringWindowToTop(elite);
            }
            finally
            {
                foreach (var thread in attachedTo)
                {
                    AttachThreadInput(GetCurrentThreadId(), thread, false);
                }
            }

            if (Landed(elite))
            {
                logger.LogInformation("Elite came forward after the attach road ({AttachOutcome})", attachOutcome);

                return FocusResult.Raised;
            }

            attachOutcome = $"{attachOutcome} and was still refused";
        }

        // The third road: what alt-tab itself does, asked for by name.
        SwitchToThisWindow(elite, true);

        if (Landed(elite))
        {
            // "Came forward after" rather than "was brought forward by": the poll credits whatever put
            // Elite's process in front within its quarter second, which is almost always this call and is not
            // proven to be.
            logger.LogInformation(
                "Elite came forward after the alt-tab road; the attach route before it {AttachOutcome}",
                attachOutcome);

            return FocusResult.Raised;
        }

        // Elite exiting between the top of this method and here would otherwise read as a Windows refusal,
        // and speak the flashing-taskbar sentence about a game that is gone.
        if (!IsWindow(elite))
        {
            logger.LogInformation("Elite's window went away mid-raise; not running rather than refused");

            return FocusResult.NotRunning;
        }

        // Still refused.
        logger.LogInformation(
            "Windows refused to bring Elite forward: {AttachOutcome}, and the alt-tab road changed "
            + "nothing. The foreground was held by {Holder}, pumping messages: {Pumping}; Elite's "
            + "window was 0x{Elite:X}",
            attachOutcome,
            holder,
            Pumping(front),
            elite);

        return FocusResult.Refused;
    }

    /// <summary>
    /// Whether <paramref name="front"/> is Elite's — the exact window, or any window owned by the same
    /// process (#107).
    /// </summary>
    private static bool BelongsToElite(nint front, nint elite)
    {
        if (front == 0 || elite == 0)
        {
            return false;
        }

        if (front == elite)
        {
            return true;
        }

        GetWindowThreadProcessId(front, out var frontProcess);
        GetWindowThreadProcessId(elite, out var eliteProcess);

        return frontProcess != 0 && frontProcess == eliteProcess;
    }

    /// <summary>Whether Elite ended up in front, giving Windows a moment to finish the switch.</summary>
    private bool Landed(nint elite)
    {
        for (var waited = 0; ; waited += 50)
        {
            var front = GetForegroundWindow();

            if (front == elite)
            {
                return true;
            }

            if (BelongsToElite(front, elite))
            {
                logger.LogInformation(
                    "Elite is in front, presenting window 0x{Front:X} rather than the 0x{Elite:X} it was raised by",
                    front,
                    elite);

                return true;
            }

            if (waited >= 250)
            {
                return false;
            }

            Thread.Sleep(50);
        }
    }

    /// <summary>
    /// Attaches this thread's input queue to the thread owning the foreground window, so that one <see
    /// cref="SetForegroundWindow"/> is allowed through (#27) — and to Elite's own window thread as well
    /// when that is a third one, which is the half of the classic recipe the first fix left out.
    /// </summary>
    private static bool TryAttach(nint front, nint elite, out uint[] attachedTo, out string outcome)
    {
        attachedTo = [];

        if (front == 0)
        {
            // No foreground window at all is one of SetForegroundWindow's exempt cases, so if this branch is
            // reached the plain call above failed with the lock not even engaged.
            outcome = "could not attach: nothing held the foreground";
            return false;
        }

        var owner = GetWindowThreadProcessId(front, out _);
        var mine = GetCurrentThreadId();

        if (owner == 0)
        {
            outcome = "could not attach: the foreground window names no owning thread";
            return false;
        }

        if (owner == mine)
        {
            outcome = "could not attach: the foreground thread is already this one";
            return false;
        }

        if (!AttachThreadInput(mine, owner, true))
        {
            outcome = $"could not attach to the foreground thread (Win32 error {Marshal.GetLastWin32Error()})";
            return false;
        }

        var target = GetWindowThreadProcessId(elite, out _);

        // The outcome names which halves engaged, because "attached and was still refused" with the Elite
        // half missing and with it present are different faults — and the missing half being the cause is
        // exactly the kind of thing #107's log could not show.
        if (target != 0 && target != mine && target != owner && AttachThreadInput(mine, target, true))
        {
            attachedTo = [owner, target];
            outcome = "attached to the foreground and Elite threads";
        }
        else
        {
            attachedTo = [owner];
            outcome = "attached to the foreground thread only";
        }

        return true;
    }

    /// <summary>
    /// The foreground window described well enough to diagnose from: handle, title, class, and owning
    /// process and thread (#107).
    /// </summary>
    private static string DescribeHolder(nint front)
    {
        if (front == 0)
        {
            return "nothing at all";
        }

        var thread = GetWindowThreadProcessId(front, out var pid);

        var buffer = new char[256];
        var className = new string(buffer, 0, GetClassName(front, buffer, buffer.Length));

        // Reads the cached caption rather than sending WM_GETTEXT to another process's window, so it cannot
        // block on a hung holder.
        var title = new string(buffer, 0, GetWindowText(front, buffer, buffer.Length));

        string process;

        try
        {
            using var owner = Process.GetProcessById((int)pid);
            process = owner.ProcessName;
        }
        catch (ArgumentException)
        {
            process = "unknown";
        }
        catch (InvalidOperationException)
        {
            process = "unknown";
        }

        return $"0x{front:X} '{title}' (class {className}, {process} pid {pid}, thread {thread})";
    }

    /// <summary>
    /// Whether the window's thread is answering messages — a <c>WM_NULL</c> with a short timeout, which
    /// is the standard are-you-pumping probe. "The thread owning the foreground window is not pumping
    /// messages" is a documented way for the attach route to fail that no log line used to show (#107).
    /// </summary>
    private static bool Pumping(nint window) =>
        window != 0 && SendMessageTimeout(window, 0, 0, 0, SmtoAbortIfHung, 200, out _) != 0;

    private const int ShowRestore = 9;

    private const uint SmtoAbortIfHung = 0x0002;

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint hWnd);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool altTab);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, [Out] char[] buffer, int capacity);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, [Out] char[] buffer, int capacity);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint hWnd,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeoutMs,
        out nint result);

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);
}
