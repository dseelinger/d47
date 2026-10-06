using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Hulls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>With the setting on and no GPU interop, the hull viewer draws on the CPU and logs why once (#626).</summary>
public class AHullViewerWithNoGpuDrawsOnTheCpuTests
{
    private static HullPose Square() => new(new HullMesh(
        [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)],
        [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
        [0, 1, 2, 0, 2, 3],
        [new HullPart("hull", 0, 2, false)],
        MathF.Sqrt(2),
        Vector3.UnitZ));

    private static Window Shown(HullViewer viewer)
    {
        var window = new Window { Content = viewer, Width = 640, Height = 480 };

        window.Show();

        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaFact]
    public void TheCpuDrawsAndTheReasonIsLoggedOnce()
    {
        var log = new Captured();
        HullViewer.Enable(() => true, log);

        try
        {
            var first = new HullViewer(Square());
            var one = Shown(first);

            Assert.False(first.OnGpu);
            Assert.NotNull(first.Frame);

            var second = new HullViewer(Square());
            var two = Shown(second);

            Assert.NotNull(second.Frame);
            Assert.Contains("GPU interop", Assert.Single(log.Lines), StringComparison.Ordinal);

            one.Close();
            two.Close();
        }
        finally
        {
            HullViewer.Enable(() => false, NullLogger.Instance);
        }
    }

    [AvaloniaFact]
    public void WithTheSettingOffNothingIsLogged()
    {
        var log = new Captured();
        HullViewer.Enable(() => false, log);

        try
        {
            var viewer = new HullViewer(Square());
            var window = Shown(viewer);

            Assert.False(viewer.OnGpu);
            Assert.NotNull(viewer.Frame);
            Assert.Empty(log.Lines);

            window.Close();
        }
        finally
        {
            HullViewer.Enable(() => false, NullLogger.Instance);
        }
    }

    private sealed class Captured : ILogger
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
