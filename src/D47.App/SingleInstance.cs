using System.Diagnostics;
using System.Runtime.InteropServices;

namespace D47.App;

/// <summary>One d47 per Commander.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string Name = @"Local\d47-single-instance";

    private Mutex? _held;

    private SingleInstance(Mutex held) => _held = held;

    /// <summary>Claims the one slot, or returns null when another copy already holds it.</summary>
    public static SingleInstance? Claim() => Claim(Name);

    /// <summary>Claims a named slot.</summary>
    internal static SingleInstance? Claim(string name)
    {
        // Created rather than opened, so the first copy to start is the one that owns it and there is no
        // window in which neither does.
        var mutex = new Mutex(initiallyOwned: true, name, out var mine);

        if (mine)
        {
            return new SingleInstance(mutex);
        }

        mutex.Dispose();

        return null;
    }

    /// <summary>Hands the slot over before starting the build that replaces this one.</summary>
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
        // Not the owning thread, which can only happen if something else already released it.
        }

        _held.Dispose();
        _held = null;
    }

    /// <summary>
    /// Brings the copy that is already running to the front, so a double-clicked shortcut shows the
    /// Commander their d47 rather than appearing to do nothing at all.
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
                        // Restores it if it was minimised, which is the state it is most likely to be in when
                        // someone clicks the taskbar pin a second time.
                        ShowWindow(other.MainWindowHandle, ShowRestore);
                        SetForegroundWindow(other.MainWindowHandle);
                        return;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
        // Best effort.
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
