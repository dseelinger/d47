using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using D47.Core.Modules;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace D47.Modules;

/// <summary>Runs one module: starts it, watches it, and starts it again after a crash as <see cref="ModuleRestarts"/> allows.</summary>
internal sealed class ModuleRunner
{
    public const string SayMethod = "notifications/d47/say";

    /// <summary>How long a module has to exit on its own once its input is closed.</summary>
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly JobObject _job;
    private readonly ModuleInbox _inbox;
    private readonly ILoggerFactory _loggers;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly ModuleRestarts _restarts;
    private readonly Action<ModuleRunner, ModuleStatus, DateTimeOffset?> _report;
    private readonly CancellationTokenSource _stop = new();

    private volatile Session? _session;

    public ModuleRunner(
        ReadyModule module,
        JobObject job,
        ModuleInbox inbox,
        ILoggerFactory loggers,
        TimeProvider time,
        ModuleRestarts restarts,
        Action<ModuleRunner, ModuleStatus, DateTimeOffset?> report)
    {
        Module = module;
        _job = job;
        _inbox = inbox;
        _loggers = loggers;
        _logger = loggers.CreateLogger<ModuleRunner>();
        _time = time;
        _restarts = restarts;
        _report = report;
    }

    public ReadyModule Module { get; }

    public string Id => Module.Manifest.Id;

    /// <summary>Completes once the runner has stopped and its process has ended.</summary>
    public Task Ended { get; private set; } = Task.CompletedTask;

    /// <summary>Starts the module once <paramref name="after"/> has completed.</summary>
    public void Start(Task after) => Ended = Task.Run(async () =>
    {
        await after.ConfigureAwait(false);
        await RunAsync(_stop.Token).ConfigureAwait(false);
    });

    /// <summary>Stops the module without counting the exit as a crash.</summary>
    public void Stop() => _stop.Cancel();

    /// <summary>Runs a call against the live session, timing it for the not-responding rule.</summary>
    public async Task<T> CallAsync<T>(Func<McpClient, CancellationToken, Task<T>> call, CancellationToken cancellation)
    {
        var session = _session ?? throw new InvalidOperationException($"Module {Id} is not running.");
        var started = session.Health.CallStarted(_time.GetUtcNow());

        try
        {
            var result = await call(session.Client, cancellation).ConfigureAwait(false);
            session.Health.Answered(_time.GetUtcNow());
            return result;
        }
        catch (ModelContextProtocol.McpProtocolException)
        {
            session.Health.Answered(_time.GetUtcNow());
            throw;
        }
        finally
        {
            session.Health.CallEnded(started);
        }
    }

    private async Task RunAsync(CancellationToken stop)
    {
        DateTimeOffset? lastAnswer = null;

        while (!stop.IsCancellationRequested)
        {
            _report(this, ModuleStatus.Starting, lastAnswer);
            lastAnswer = await RunOnceAsync(stop).ConfigureAwait(false) ?? lastAnswer;

            if (stop.IsCancellationRequested)
            {
                return;
            }

            if (_restarts.Exited(_time.GetUtcNow()) is not { } wait)
            {
                _logger.LogWarning("Module {Id} stopped after repeated crashes", Id);
                _report(this, ModuleStatus.StoppedAfterRepeatedCrashes, lastAnswer);
                return;
            }

            _logger.LogWarning("Module {Id} exited; starting it again in {Wait}", Id, wait);

            try
            {
                await Task.Delay(wait, _time, stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Starts the process and serves it until it exits or the runner stops; the time of its last answer, if any.</summary>
    private async Task<DateTimeOffset?> RunOnceAsync(CancellationToken stop)
    {
        Process? process = null;
        Task stderr = Task.CompletedTask;
        ModuleResponsiveness? health = null;

        try
        {
            process = Launch();
            stderr = StderrLines.ReadAsync(
                process.StandardError,
                line => _logger.LogInformation("Module {Id}: {Line}", Id, line),
                CancellationToken.None);

            var transport = new StreamClientTransport(process.StandardInput.BaseStream, process.StandardOutput.BaseStream, _loggers);
            var client = await McpClient.CreateAsync(transport, Options(), _loggers, stop).ConfigureAwait(false);

            await using (client.ConfigureAwait(false))
            {
                health = new ModuleResponsiveness(_time.GetUtcNow());
                _session = new Session(client, health);
                _logger.LogInformation("Module {Id} is running as process {Pid}", Id, process.Id);
                _report(this, ModuleStatus.Running, health.LastAnswer);

                await WatchAsync(process, client, health, stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Module {Id} failed", Id);
        }
        finally
        {
            _session = null;

            if (process is not null)
            {
                await EndAsync(process).ConfigureAwait(false);
                await Task.WhenAny(stderr, Task.Delay(ExitGrace, CancellationToken.None)).ConfigureAwait(false);
                process.Dispose();
            }
        }

        return health?.LastAnswer;
    }

    private Process Launch()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var info = new ProcessStartInfo(Module.CommandPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Module.Folder,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8,
        };

        foreach (var arg in Module.Manifest.Args)
        {
            info.ArgumentList.Add(arg);
        }

        var process = Process.Start(info) ?? throw new InvalidOperationException($"Module {Id} did not start.");

        try
        {
            _job.Add(process);
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            process.Dispose();
            throw;
        }

        return process;
    }

    private async Task WatchAsync(Process process, McpClient client, ModuleResponsiveness health, CancellationToken stop)
    {
        var exited = process.WaitForExitAsync(stop);
        var status = ModuleStatus.Running;

        while (!exited.IsCompleted && !client.Completion.IsCompleted)
        {
            await Task.WhenAny(exited, client.Completion, Task.Delay(WatchEvery, _time, stop)).ConfigureAwait(false);
            stop.ThrowIfCancellationRequested();

            var now = _time.GetUtcNow();

            if (health.PingDue(now))
            {
                health.PingSent(now);
                _ = PingAsync(client, health);
            }

            var next = health.Status(now);

            if (next != status)
            {
                status = next;
                _logger.LogWarning("Module {Id} is {Status}; last answered at {LastAnswer:O}", Id, next, health.LastAnswer);
                _report(this, next, health.LastAnswer);
            }
        }
    }

    private async Task PingAsync(McpClient client, ModuleResponsiveness health)
    {
        using var timeout = new CancellationTokenSource(ModuleResponsiveness.PingTimeout, _time);

        try
        {
            await client.PingAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
            health.Answered(_time.GetUtcNow());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Module {Id} did not answer a ping", Id);
        }
    }

    private async Task EndAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.Close();

                using var grace = new CancellationTokenSource(ExitGrace);

                try
                {
                    await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Module {Id} exited with code {Code}", Id, process.ExitCode);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Module {Id} could not be ended cleanly", Id);
        }
    }

    private McpClientOptions Options() => new()
    {
        ClientInfo = new Implementation
        {
            Name = "d47",
            Version = typeof(ModuleRunner).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
        },
        InitializationTimeout = ModuleResponsiveness.CallTimeout,
        Handlers = new McpClientHandlers
        {
            NotificationHandlers =
            [
                new(SayMethod, OnSay),
                new(NotificationMethods.ResourceUpdatedNotification, OnResourceUpdated),
            ],
        },
    };

    private ValueTask OnSay(JsonRpcNotification notification, CancellationToken cancellation)
    {
        if (Text(notification.Params, "key") is { } key && Text(notification.Params, "text") is { Length: > 0 } text)
        {
            _inbox.Post(new ModuleLine(Id, key, text));
        }
        else
        {
            _logger.LogDebug("Module {Id} sent a d47/say without a key and text", Id);
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask OnResourceUpdated(JsonRpcNotification notification, CancellationToken cancellation)
    {
        if (Text(notification.Params, "uri") is { Length: > 0 } uri)
        {
            _inbox.Post(new ModuleResourceUpdated(Id, uri));
        }

        return ValueTask.CompletedTask;
    }

    private static string? Text(JsonNode? parameters, string name) =>
        parameters is JsonObject named
        && named[name] is JsonValue value
        && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private sealed record Session(McpClient Client, ModuleResponsiveness Health);
}
