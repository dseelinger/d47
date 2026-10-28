using System.Diagnostics;
using System.Runtime.InteropServices;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>Puts a window in front past Windows' foreground lock (#27).</summary>
public static class ForegroundWindow
{
    private const int ShowRestore = 9;

    private const uint SmtoAbortIfHung = 0x0002;

    /// <summary>
    /// Whether <paramref name="front"/> is <paramref name="window"/> or another window of the same process
    /// (#107).
    /// </summary>
    public static bool Belongs(nint front, nint window)
    {
        if (front == 0 || window == 0)
        {
            return false;
        }

        if (front == window)
        {
            return true;
        }

        GetWindowThreadProcessId(front, out var frontProcess);
        GetWindowThreadProcessId(window, out var windowProcess);

        return frontProcess != 0 && frontProcess == windowProcess;
    }

    /// <summary>Whether <paramref name="window"/>'s process has the foreground.</summary>
    public static bool IsFront(nint window) => Belongs(GetForegroundWindow(), window);

    /// <summary>Whether <paramref name="window"/> still exists.</summary>
    public static bool Exists(nint window) => IsWindow(window);

    /// <summary>
    /// Restores <paramref name="window"/> if minimised and puts it in front by the plain call, the
    /// attach-thread road, then <c>SwitchToThisWindow</c>. <paramref name="name"/> words the log.
    /// </summary>
    public static FocusResult Raise(nint window, string name, ILogger logger)
    {
        if (window == 0)
        {
            return FocusResult.NotRunning;
        }

        var already = GetForegroundWindow();

        if (Belongs(already, window))
        {
            if (already != window)
            {
                logger.LogInformation(
                    "{Name} already holds the foreground, presenting window 0x{Front:X} rather than the cached 0x{Window:X}",
                    name,
                    already,
                    window);
            }

            return FocusResult.AlreadyThere;
        }

        if (IsIconic(window))
        {
            ShowWindow(window, ShowRestore);
        }

        if (SetForegroundWindow(window) && Landed(window, name, logger))
        {
            logger.LogInformation("{Name} was brought forward by the plain SetForegroundWindow", name);

            return FocusResult.Raised;
        }

        // Windows only grants the foreground to a process that already has it, or that received the last
        // input (#27). d47 running behind Elite is neither, so the plain call is refused.
        var front = GetForegroundWindow();
        var holder = DescribeHolder(front);

        if (TryAttach(front, window, out var attachedTo, out var attachOutcome))
        {
            // The queues are detached before the landing is polled: attached threads share input state, so
            // holding the attachment across the poll would stall input for the window the Commander is
            // working in.
            try
            {
                SetForegroundWindow(window);
                BringWindowToTop(window);
            }
            finally
            {
                foreach (var thread in attachedTo)
                {
                    AttachThreadInput(GetCurrentThreadId(), thread, false);
                }
            }

            if (Landed(window, name, logger))
            {
                logger.LogInformation("{Name} came forward after the attach road ({AttachOutcome})", name, attachOutcome);

                return FocusResult.Raised;
            }

            attachOutcome = $"{attachOutcome} and was still refused";
        }

        SwitchToThisWindow(window, true);

        if (Landed(window, name, logger))
        {
            logger.LogInformation(
                "{Name} came forward after the alt-tab road; the attach route before it {AttachOutcome}",
                name,
                attachOutcome);

            return FocusResult.Raised;
        }

        if (!IsWindow(window))
        {
            logger.LogInformation("{Name}'s window went away mid-raise; not running rather than refused", name);

            return FocusResult.NotRunning;
        }

        logger.LogInformation(
            "Windows refused to bring {Name} forward: {AttachOutcome}, and the alt-tab road changed "
            + "nothing. The foreground was held by {Holder}, pumping messages: {Pumping}; the "
            + "window was 0x{Window:X}",
            name,
            attachOutcome,
            holder,
            Pumping(front),
            window);

        return FocusResult.Refused;
    }

    private static bool Landed(nint window, string name, ILogger logger)
    {
        for (var waited = 0; ; waited += 50)
        {
            var front = GetForegroundWindow();

            if (front == window)
            {
                return true;
            }

            if (Belongs(front, window))
            {
                logger.LogInformation(
                    "{Name} is in front, presenting window 0x{Front:X} rather than the 0x{Window:X} it was raised by",
                    name,
                    front,
                    window);

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
    /// Attaches this thread's input queue to the thread owning the foreground window, so that one
    /// <c>SetForegroundWindow</c> is allowed through (#27), and to the target's own thread when that is a
    /// third one.
    /// </summary>
    private static bool TryAttach(nint front, nint window, out uint[] attachedTo, out string outcome)
    {
        attachedTo = [];

        if (front == 0)
        {
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

        var target = GetWindowThreadProcessId(window, out _);

        if (target != 0 && target != mine && target != owner && AttachThreadInput(mine, target, true))
        {
            attachedTo = [owner, target];
            outcome = "attached to the foreground and target threads";
        }
        else
        {
            attachedTo = [owner];
            outcome = "attached to the foreground thread only";
        }

        return true;
    }

    /// <summary>The foreground window described well enough to diagnose from (#107).</summary>
    private static string DescribeHolder(nint front)
    {
        if (front == 0)
        {
            return "nothing at all";
        }

        var thread = GetWindowThreadProcessId(front, out var pid);

        var buffer = new char[256];
        var className = new string(buffer, 0, GetClassName(front, buffer, buffer.Length));

        // The cached caption, so a hung holder cannot block the read.
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

    /// <summary>Whether the window's thread answers a <c>WM_NULL</c> within 200 ms.</summary>
    private static bool Pumping(nint window) =>
        window != 0 && SendMessageTimeout(window, 0, 0, 0, SmtoAbortIfHung, 200, out _) != 0;

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
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);
}
