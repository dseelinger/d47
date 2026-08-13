using System.Diagnostics;
using System.Runtime.InteropServices;

namespace D47.App;

/// <summary>
/// One d47 per Commander. A second copy is not a second workspace — it is a second journal
/// tailer, a second microphone, a second set of global hotkeys and two writers over one
/// <c>data/</c> folder, and the Commander sees none of that until something behaves oddly.
/// <para>
/// <b>The name deliberately carries no version.</b> The point is that an older build already
/// running blocks a newer one just as firmly as an identical one would; a versioned name would
/// let 0.2.2 and 0.2.3 run side by side, which is the exact case worth preventing after an
/// update.
/// </para>
/// <para>
/// Per-user, not machine-wide: <c>Local\</c> rather than <c>Global\</c>. Two Commanders on one
/// machine have separate profiles, separate installs and separate data folders, and stopping
/// one of them from running because the other is logged in would be a bug.
/// </para>
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string Name = @"Local\d47-single-instance";

    private Mutex? _held;

    private SingleInstance(Mutex held) => _held = held;

    /// <summary>
    /// Claims the one slot, or returns null when another copy already holds it. Claiming only —
    /// raising the copy that is already running is <see cref="SurfaceRunningCopy"/>, kept
    /// separate because taking a lock and bringing someone else's window to the front are not
    /// the same act and only one of them is safe to do in a test.
    /// </summary>
    public static SingleInstance? Claim()
    {
        // Created rather than opened, so the first copy to start is the one that owns it and
        // there is no window in which neither does.
        var mutex = new Mutex(initiallyOwned: true, Name, out var mine);

        if (mine)
        {
            return new SingleInstance(mutex);
        }

        mutex.Dispose();

        return null;
    }

    /// <summary>
    /// Hands the slot over before starting the build that replaces this one. Without this the
    /// replacement would find the slot still held by the copy that launched it and exit
    /// immediately, so an accepted update would look like d47 simply closing.
    /// </summary>
    public void ReleaseForSuccessor() => Dispose();

    public void Dispose()
    {
        if (_held is null)
        {
            return;
        }

        try
        {
            _held.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not the owning thread, which can only happen if something else already released
            // it. Either way the slot is free, which is all this needs to be true.
        }

        _held.Dispose();
        _held = null;
    }

    /// <summary>
    /// Brings the copy that is already running to the front, so a double-clicked shortcut shows
    /// the Commander their d47 rather than appearing to do nothing at all. Found by executable
    /// rather than by window title, because the title carries the version and the copy already
    /// running may be an older one — which is precisely the case this exists for.
    /// </summary>
    public static void SurfaceRunningCopy()
    {
        try
        {
            using var self = Process.GetCurrentProcess();

            foreach (var other in Process.GetProcessesByName(self.ProcessName))
            {
                using (other)
                {
                    if (other.Id != self.Id && other.MainWindowHandle != nint.Zero)
                    {
                        // Restores it if it was minimised, which is the state it is most likely
                        // to be in when someone clicks the taskbar pin a second time.
                        ShowWindow(other.MainWindowHandle, ShowRestore);
                        SetForegroundWindow(other.MainWindowHandle);
                        return;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            // Best effort. Failing to raise the other window is not a reason for this copy to
            // stay running, which would defeat the whole point.
        }
    }

    private const int ShowRestore = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
}
