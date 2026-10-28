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
            if (_handle != 0 && ForegroundWindow.Exists(_handle))
            {
                return _handle;
            }

            _handle = Find();
            return _handle;
        }
    }

    public bool IsRunning => Handle != 0;

    /// <summary>The process that owns Elite's window, or 0 when it is not running.</summary>
    public uint ProcessId
    {
        get
        {
            var elite = Handle;

            if (elite == 0)
            {
                return 0;
            }

            GetWindowThreadProcessId(elite, out var process);
            return process;
        }
    }

    /// <summary>Whether Elite has the foreground.</summary>
    public bool IsForeground
    {
        get
        {
            var elite = Handle;
            return elite != 0 && ForegroundWindow.IsFront(elite);
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
    public FocusResult Raise() => ForegroundWindow.Raise(Handle, "Elite", logger);

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
}
