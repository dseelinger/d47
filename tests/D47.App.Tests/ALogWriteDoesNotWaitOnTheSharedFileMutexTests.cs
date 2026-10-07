using System.Diagnostics;
using D47.App.Logging;
using D47.Core;
using D47.Core.Diagnostics;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The shared file sink takes a named mutex, keyed on the file's full path, before each write and waits
/// up to ten seconds for it. The tick logs, so a call must return while another holder has it.
/// </summary>
public class ALogWriteDoesNotWaitOnTheSharedFileMutexTests
{
    [Fact]
    public void ALineLoggedWhileTheMutexIsHeldReturnsAtOnceAndStillReachesTheFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "d47-log-mutex", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        const string line = "a line logged while the mutex was held";

        // Serilog.Sinks.File 7.0.0, SharedFileSink.OSMutex.cs: the full path, separators as colons,
        // then ".serilog".
        var file = Path.Combine(paths.Logs, $"d47-{DateTime.Now:yyyyMMdd}.log");
        var mutexName = Path.GetFullPath(file).Replace(Path.DirectorySeparatorChar, ':') + ".serilog";

        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        // A mutex belongs to the thread that took it, so another thread holds it.
        var holder = new Thread(() =>
        {
            using var mutex = new Mutex(false, mutexName);
            mutex.WaitOne();
            held.Set();
            release.Wait();
            mutex.ReleaseMutex();
        });

        try
        {
            var logger = LoggingSetup.Create(paths, new SerilogVerbosityControl());

            holder.Start();
            Assert.True(held.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

            var clock = Stopwatch.StartNew();
            logger.Information(line);
            clock.Stop();

            release.Set();
            holder.Join();
            (logger as IDisposable)?.Dispose();

            Assert.True(
                clock.Elapsed < TimeSpan.FromSeconds(2),
                $"the log call took {clock.Elapsed.TotalMilliseconds:0} ms while the mutex was held");
            Assert.Contains(line, File.ReadAllText(file), StringComparison.Ordinal);
        }
        finally
        {
            release.Set();

            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
