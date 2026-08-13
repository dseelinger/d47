using System.Diagnostics;
using System.Runtime.InteropServices;
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

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);
}
