using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace D47.App.Tests;

/// <summary>
/// Diagnostic hook for the headless-session flake (bugs.md, docs/plans/flake-hunt.md).
/// The failure is a VerifyAccess throw during EnsureIsolatedApplication: after
/// Dispatcher.ResetBeforeUnitTests() nulls the global dispatcher, the first thread to read
/// Dispatcher.UIThread claims ownership — and when that thread is not the session's dispatch
/// thread, the next DefaultRenderLoop.Add on the session thread throws. This hook captures,
/// at the moment of any such throw, who owns the dispatcher and every thread that has claimed
/// one since the last reset. Active only when D47_FLAKE_LOG names a file, so the suite under
/// normal runs carries none of it.
/// </summary>
internal static class FlakeInstrumentation
{
    private static readonly object Gate = new();
    private static string? _path;

    [ModuleInitializer]
    internal static void Install()
    {
        _path = Environment.GetEnvironmentVariable("D47_FLAKE_LOG");
        if (string.IsNullOrEmpty(_path))
        {
            return;
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
    }

    private static void OnFirstChance(object? sender, FirstChanceExceptionEventArgs e)
    {
        // The exact message VerifyAccess throws; anything else is not this investigation.
        if (e.Exception is not InvalidOperationException ex ||
            !ex.Message.StartsWith("The calling thread cannot access", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            Capture();
        }
        catch
        {
            // A diagnostic that can fail the run it is diagnosing is worse than no diagnostic.
        }
    }

    private static void Capture()
    {
        var dispatcher = typeof(Avalonia.Threading.Dispatcher);
        var uiThread = dispatcher
            .GetField("s_uiThread", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
        var owner = uiThread is null
            ? null
            : dispatcher.GetField("_thread", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(uiThread) as Thread;
        var current = Thread.CurrentThread;

        var sb = new StringBuilder();
        sb.AppendLine($"=== VerifyAccess violation {DateTime.Now:HH:mm:ss.fff} ===");
        sb.AppendLine(Describe("current", current));
        sb.AppendLine(owner is null ? "owner  : (no dispatcher, or _thread unreadable)" : Describe("owner  ", owner));

        // Every thread holding a per-thread dispatcher. ResetGlobalState clears this table, so
        // its contents are exactly the threads that claimed one since the last reset — the
        // interloper is in here by name. A ConditionalWeakTable in 12.1.1, so enumerated as
        // pairs rather than cast to IDictionary, which it is not.
        try
        {
            if (dispatcher.GetField("s_dispatchers", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetValue(null) is IEnumerable table)
            {
                foreach (var entry in table)
                {
                    if (entry?.GetType().GetProperty("Key")?.GetValue(entry) is Thread claimant)
                    {
                        sb.AppendLine(Describe("claimed", claimant));
                    }
                }
            }
        }
        catch
        {
            sb.AppendLine("claimed: (table changed while being read)");
        }

        sb.AppendLine("current stack:");
        sb.AppendLine(new StackTrace(2, fNeedFileInfo: false).ToString());

        lock (Gate)
        {
            File.AppendAllText(_path!, sb.ToString());
        }
    }

    private static string Describe(string label, Thread thread) =>
        $"{label}: id={thread.ManagedThreadId} name='{thread.Name}' " +
        $"pool={thread.IsThreadPoolThread} alive={thread.IsAlive} state={thread.ThreadState}";
}
