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
    /// <para>
    /// Compared by owning process rather than by exact handle (#107). The question this answers
    /// is where injected keys would land, and that is a property of the focused <em>process</em>:
    /// Elite holding the foreground through a different top-level window of its own — which a VR
    /// session is suspected of arranging — is still Elite, and a browser can never be. The doubt
    /// rule survives the widening, because a window whose process cannot be read matches nothing.
    /// </para>
    /// </summary>
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
    /// <para>
    /// <b>Three roads are tried, in order, and the log says which one carried it</b> (#27, #107):
    /// the plain call, the call with this thread's input queue attached to the foreground
    /// thread's, and alt-tab emulation. A refusal names which step failed and what held the
    /// foreground when it did, because the one field report to date collapsed four different
    /// faults into a single sentence and left nothing to diagnose from.
    /// </para>
    /// </summary>
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
            // The likeliest shape of #107's VR conjecture is not a raise landing on a different
            // window — it is Elite already in front through one. This is the only place that
            // state is visible on a Commander's ask, so it is the place that records it.
            if (already != elite)
            {
                logger.LogInformation(
                    "Elite already holds the foreground, presenting window 0x{Front:X} rather than the cached 0x{Elite:X}",
                    already,
                    elite);
            }

            return FocusResult.AlreadyThere;
        }

        // Restored first if it was minimised, which is the state a window alt-tabbed away from
        // is most likely to be in. SetForegroundWindow on a minimised window raises it without
        // unminimising it.
        if (IsIconic(elite))
        {
            ShowWindow(elite, ShowRestore);
        }

        if (SetForegroundWindow(elite) && Landed(elite))
        {
            logger.LogInformation("Elite was brought forward by the plain SetForegroundWindow");

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
        //
        // Who held the foreground is read before anything tries to move it (#107): a refusal has
        // to name the holder, and by the time it is reported the attempts below may have changed
        // the answer.
        var front = GetForegroundWindow();
        var holder = DescribeHolder(front);

        if (TryAttach(front, elite, out var attachedTo, out var attachOutcome))
        {
            // The permission is consumed by the calls themselves, so the queues are detached
            // before the landing is verified: attached threads share input state, and holding
            // the attachment across a poll would stall input for the very window the Commander
            // is working in — and, through the Elite half, for the game.
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

        // The third road: what alt-tab itself does, asked for by name. Declared in winuser.h and
        // stable for decades, discouraged for general use — the same standing as the attach above,
        // which is why the honest refusal sentence stays behind both. It synthesises no input and
        // changes nothing machine-wide, so it is within the injection rule where the inert
        // keystroke (#27's other technique) is not; and it never minimises Elite, which is what
        // ruled out minimise-then-restore.
        SwitchToThisWindow(elite, true);

        if (Landed(elite))
        {
            // "Came forward after" rather than "was brought forward by": the poll credits
            // whatever put Elite's process in front within its quarter second, which is almost
            // always this call and is not proven to be.
            logger.LogInformation(
                "Elite came forward after the alt-tab road; the attach route before it {AttachOutcome}",
                attachOutcome);

            return FocusResult.Raised;
        }

        // Elite exiting between the top of this method and here would otherwise read as a
        // Windows refusal, and speak the flashing-taskbar sentence about a game that is gone.
        if (!IsWindow(elite))
        {
            logger.LogInformation("Elite's window went away mid-raise; not running rather than refused");

            return FocusResult.NotRunning;
        }

        // Still refused. Said out loud rather than only logged, because these are workarounds
        // against a lock Microsoft has tightened before and may tighten again — the honest
        // sentence has to stay behind them. The log now carries the diagnosis #107 asked for:
        // which step failed, and what held the foreground when it did — including whether the
        // holder's thread was answering messages, which is probed only now because it can cost
        // 200ms against a busy holder and a raise that landed never needs the answer.
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
    /// Whether <paramref name="front"/> is Elite's — the exact window, or any window owned by the
    /// same process (#107). Process identity is what the callers actually mean: injected keys land
    /// in whichever process holds the focus, whatever handle is presenting it.
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

    /// <summary>
    /// Whether Elite ended up in front, giving Windows a moment to finish the switch. Activation
    /// through the shell is not synchronous, and reporting a refusal for a switch that was merely
    /// in flight would be a lying log — a quarter of a second is invisible against the Commander's
    /// stated three-second ceiling.
    /// <para>
    /// A different window of Elite's own process counts, and is logged when it happens: whether a
    /// VR session presents a window other than the one d47 raises is exactly the question #107
    /// could not answer.
    /// </para>
    /// </summary>
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
    /// Attaches this thread's input queue to the thread owning the foreground window, so that one
    /// <see cref="SetForegroundWindow"/> is allowed through (#27) — and to Elite's own window
    /// thread as well when that is a third one, which is the half of the classic recipe the first
    /// fix left out. The Elite half is best effort; the foreground half is the permission.
    /// <para>
    /// False when there is nothing to attach to, or when the attach itself is refused — and
    /// <paramref name="outcome"/> says which, with the Win32 error when there is one, because
    /// "could not attach" and "attached and was still refused" are different faults with
    /// different answers and the log used to collapse them (#107).
    /// </para>
    /// </summary>
    private static bool TryAttach(nint front, nint elite, out uint[] attachedTo, out string outcome)
    {
        attachedTo = [];

        if (front == 0)
        {
            // No foreground window at all is one of SetForegroundWindow's exempt cases, so if
            // this branch is reached the plain call above failed with the lock not even engaged.
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

        // The outcome names which halves engaged, because "attached and was still refused" with
        // the Elite half missing and with it present are different faults — and the missing half
        // being the cause is exactly the kind of thing #107's log could not show.
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
    /// The foreground window described well enough to diagnose from: handle, title, class, and
    /// owning process and thread (#107). Read before the raise attempts run, because by the time
    /// a refusal is reported they may have changed what the answer would be — but deliberately
    /// without the pumping probe, which does not go stale and can cost 200ms, so it waits for
    /// <see cref="Pumping"/> on the refusal path.
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

        // Reads the cached caption rather than sending WM_GETTEXT to another process's window,
        // so it cannot block on a hung holder.
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
    /// Whether the window's thread is answering messages — a <c>WM_NULL</c> with a short
    /// timeout, which is the standard are-you-pumping probe. "The thread owning the foreground
    /// window is not pumping messages" is a documented way for the attach route to fail that no
    /// log line used to show (#107). Costs up to 200ms against a busy-but-alive holder, which is
    /// why only the refusal path asks.
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
