using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using D47.Core.Modules;
using Xunit;

namespace D47.Modules.Tests;

/// <summary>Runs the fake module exe built from <c>tests/D47.Modules.FakeModule</c>.</summary>
[Trait("Category", "Integration")]
public sealed class AModuleRunsAndIsRestartedAfterACrashTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan[] ShortWaits =
        [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50)];

    private readonly RecordingLoggers _logs = new();
    private readonly ModuleHost _host;
    private readonly List<ModuleStatusReport> _reports = [];
    private readonly ReadyModule _module;

    public AModuleRunsAndIsRestartedAfterACrashTests()
    {
        _host = new ModuleHost(_logs, restartWaits: ShortWaits);
        var command = typeof(AModuleRunsAndIsRestartedAfterACrashTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "FakeModule").Value!;
        command = Path.GetFullPath(command);

        _module = new ReadyModule(
            Path.GetDirectoryName(command)!,
            new ModuleManifest(1, "fake", "Fake", "Tests", "1.0.0", Path.GetFileName(command), [], 300, [], null),
            command);

        _host.StatusChanged += report =>
        {
            lock (_reports)
            {
                _reports.Add(report);
            }
        };
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task ItRunsAnswersSpeaksAndIsRestartedUntilItKeepsCrashing()
    {
        var cancellation = TestContext.Current.CancellationToken;

        _host.Apply([_module], _ => true);
        await Until(ModuleStatus.Running, from: 0);

        Assert.Equal(new ModuleToolResult(false, "hello"), await _host.CallToolAsync("fake", "echo", Args(("text", "hello")), cancellation));

        var tools = await _host.ListToolsAsync("fake", cancellation);
        Assert.Equal(["which process is the fake"], tools.Single(tool => tool.Name == "pid").Phrases);
        Assert.Empty(tools.Single(tool => tool.Name == "echo").Phrases);

        await _host.CallToolAsync("fake", "say", Args(("key", "tally"), ("text", "Three bounties open.")), cancellation);
        Assert.Equal(new ModuleLine("fake", "tally", "Three bounties open."), await NextNotice());

        for (var crash = 1; crash <= 3; crash++)
        {
            var seen = Count();
            await Kill(cancellation);
            await Until(ModuleStatus.Running, from: seen);
        }

        var before = Count();
        await Kill(cancellation);
        await Until(ModuleStatus.StoppedAfterRepeatedCrashes, from: before);
    }

    [Fact]
    public async Task ASamplingRequestFromTheModuleIsAnsweredWithAnError()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var older = _module with { Manifest = _module.Manifest with { Args = ["--protocol", "2025-06-18"] } };

        _host.Apply([older], _ => true);
        await Until(ModuleStatus.Running, from: 0);

        var result = await _host.CallToolAsync("fake", "sample", Args(), cancellation);

        Assert.True(result.Text.StartsWith("refused: McpProtocolException", StringComparison.Ordinal), result.Text);
    }

    [Fact]
    public async Task ASamplingInputRequestInAToolResultIsNotMet()
    {
        var cancellation = TestContext.Current.CancellationToken;

        _host.Apply([_module], _ => true);
        await Until(ModuleStatus.Running, from: 0);

        var result = await _host.CallToolAsync("fake", "sample", Args(), cancellation);

        Assert.True(result.IsError, result.Text);
        Assert.DoesNotContain("answered", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TurningItOffIsNotACrash()
    {
        var cancellation = TestContext.Current.CancellationToken;

        _host.Apply([_module], _ => true);
        await Until(ModuleStatus.Running, from: 0);
        var pid = await Pid(cancellation);

        var seen = Count();
        _host.Apply([_module], _ => false);
        await Until(ModuleStatus.Off, from: seen);

        await WaitForExit(pid);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellation);

        lock (_reports)
        {
            Assert.Equal([ModuleStatus.Off], _reports.Skip(seen).Select(report => report.Status));
        }
    }

    [Fact]
    public async Task TurningItBackOnWaitsForTheOldProcessToEnd()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var lingering = _module with { Manifest = _module.Manifest with { Args = ["--linger"] } };

        _host.Apply([lingering], _ => true);
        await Until(ModuleStatus.Running, from: 0);
        var old = await Pid(cancellation);

        var seen = Count();
        _host.Apply([lingering], _ => false);
        _host.Apply([lingering], _ => true);
        await Until(ModuleStatus.Running, from: seen);

        Assert.False(Alive(old), $"Process {old} was still running when its replacement started.");
    }

    [Fact]
    public async Task RestartStartsAModuleThatStoppedAfterRepeatedCrashes()
    {
        var cancellation = TestContext.Current.CancellationToken;

        _host.Apply([_module], _ => true);
        await Until(ModuleStatus.Running, from: 0);

        for (var crash = 1; crash <= 4; crash++)
        {
            var seen = Count();
            await Kill(cancellation);
            await Until(crash < 4 ? ModuleStatus.Running : ModuleStatus.StoppedAfterRepeatedCrashes, from: seen);
        }

        var before = Count();
        _host.Restart("fake");
        await Until(ModuleStatus.Running, from: before);

        Assert.Equal(new ModuleToolResult(false, "again"), await _host.CallToolAsync("fake", "echo", Args(("text", "again")), cancellation));
    }

    private static Dictionary<string, object?> Args(params (string Name, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Name, pair => (object?)pair.Value);

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task WaitForExit(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            using var patience = new CancellationTokenSource(Patience);
            await process.WaitForExitAsync(patience.Token);
        }
        catch (ArgumentException)
        {
        }
    }

    private int Count()
    {
        lock (_reports)
        {
            return _reports.Count;
        }
    }

    private async Task<int> Pid(CancellationToken cancellation) =>
        int.Parse((await _host.CallToolAsync("fake", "pid", Args(), cancellation)).Text, CultureInfo.InvariantCulture);

    private async Task Kill(CancellationToken cancellation)
    {
        var pid = await Pid(cancellation);

        using (var process = Process.GetProcessById(pid))
        {
            process.Kill();
        }

        await WaitForExit(pid);
    }

    private async Task Until(ModuleStatus status, int from)
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < Patience)
        {
            lock (_reports)
            {
                if (_reports.Skip(from).Any(report => report.Status == status))
                {
                    return;
                }
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        lock (_reports)
        {
            Assert.Fail($"No {status} report after #{from}; saw {string.Join(", ", _reports.Select(report => report.Status))}.\n{string.Join("\n", _logs.Lines)}");
        }
    }

    private async Task<ModuleNotice> NextNotice()
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < Patience)
        {
            if (_host.Inbox.Drain() is [var first, ..])
            {
                return first;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("No notice arrived.");
    }
}
