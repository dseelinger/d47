using System.Text;
using System.Text.Json.Nodes;
using D47.Core.Modules;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace D47.Modules;

/// <summary>Runs the installed modules that are turned on, each as a child process d47 talks to over MCP on stdio.</summary>
public sealed class ModuleHost : IDisposable
{
    /// <summary>How long <see cref="Dispose"/> waits for modules to exit before ending them.</summary>
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(3);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, ModuleRunner> _runners = new(StringComparer.Ordinal);

    /// <summary>Runners stopped but perhaps not yet ended, by module id; a new start waits for them.</summary>
    private readonly Dictionary<string, ModuleRunner> _stopping = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModuleStatusReport> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly JobObject _job = new();
    private readonly ILoggerFactory _loggers;
    private readonly ILogger<ModuleHost> _logger;
    private readonly TimeProvider _time;
    private readonly IReadOnlyList<TimeSpan> _restartWaits;
    private bool _disposed;

    /// <param name="restartWaits">The waits before each restart; <see cref="ModuleRestarts.Waits"/> when null.</param>
    public ModuleHost(ILoggerFactory loggers, TimeProvider? time = null, IReadOnlyList<TimeSpan>? restartWaits = null)
    {
        _loggers = loggers;
        _logger = loggers.CreateLogger<ModuleHost>();
        _time = time ?? TimeProvider.System;
        _restartWaits = restartWaits ?? ModuleRestarts.Waits;
    }

    /// <summary>Raised on any thread when a module's status changes.</summary>
    public event Action<ModuleStatusReport>? StatusChanged;

    /// <summary>The notifications modules have sent, for the tick to drain.</summary>
    public ModuleInbox Inbox { get; } = new();

    public IReadOnlyList<ModuleStatusReport> Statuses
    {
        get
        {
            lock (_gate)
            {
                return [.. _statuses.Values];
            }
        }
    }

    /// <summary>Starts each ready module that is on and not already running, and stops every other one.</summary>
    public void Apply(IReadOnlyList<InstalledModule> installed, Func<string, bool> isOn)
    {
        var reports = new List<ModuleStatusReport>();

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var ready = installed.OfType<ReadyModule>().ToList();

            foreach (var gone in _runners.Keys.Except(ready.Where(m => isOn(m.Manifest.Id)).Select(m => m.Manifest.Id)).ToList())
            {
                StopLocked(gone);
            }

            foreach (var module in ready)
            {
                if (!isOn(module.Manifest.Id))
                {
                    SetLocked(new ModuleStatusReport(module.Folder, module.Manifest.Id, ModuleStatus.Off), reports);
                }
                else if (!_runners.ContainsKey(module.Manifest.Id))
                {
                    StartLocked(module, previous: null);
                }
            }

            foreach (var refused in installed.OfType<RefusedModule>())
            {
                SetLocked(new ModuleStatusReport(refused.Folder, null, ModuleStatus.Refused), reports);
            }

            foreach (var uninstalled in _statuses.Keys.Except(installed.Select(m => m.Folder), StringComparer.OrdinalIgnoreCase).ToList())
            {
                _statuses.Remove(uninstalled);
            }
        }

        Raise(reports);
    }

    /// <summary>Stops a running module and starts it again with its crash count cleared; does nothing to a module that is off.</summary>
    public void Restart(string id)
    {
        lock (_gate)
        {
            if (!_disposed && _runners.TryGetValue(id, out var runner))
            {
                StartLocked(runner.Module, runner);
            }
        }
    }

    /// <summary>The tools the module offers, or none when it is not running or does not answer.</summary>
    public async Task<IReadOnlyList<ModuleTool>> ListToolsAsync(string id, CancellationToken cancellation)
    {
        try
        {
            if (Runner(id) is not { } runner)
            {
                return [];
            }

            var tools = await runner.CallAsync(
                async (client, token) => await client.ListToolsAsync(cancellationToken: token).ConfigureAwait(false),
                cancellation).ConfigureAwait(false);

            return [.. tools.Select(tool => new ModuleTool(id, tool.Name, tool.Description, tool.ProtocolTool.InputSchema, Phrases(tool.ProtocolTool.Meta)))];
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not list the tools of module {Id}", id);
            return [];
        }
    }

    public async Task<ModuleToolResult> CallToolAsync(
        string id, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellation)
    {
        try
        {
            if (Runner(id) is not { } runner)
            {
                return new ModuleToolResult(true, $"The module {id} is not running.");
            }

            var result = await runner.CallAsync(
                async (client, token) => await client.CallToolAsync(tool, arguments, cancellationToken: token).ConfigureAwait(false),
                cancellation).ConfigureAwait(false);

            return new ModuleToolResult(result.IsError == true, Join(result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Module {Id} could not run {Tool}", id, tool);
            return new ModuleToolResult(true, $"The module {id} did not answer.");
        }
    }

    /// <summary>The resource's text, or null when the module is not running or does not answer.</summary>
    public async Task<ModuleResource?> ReadResourceAsync(string id, string uri, CancellationToken cancellation)
    {
        try
        {
            if (Runner(id) is not { } runner)
            {
                return null;
            }

            var result = await runner.CallAsync(
                async (client, token) => await client.ReadResourceAsync(uri, cancellationToken: token).ConfigureAwait(false),
                cancellation).ConfigureAwait(false);
            var texts = result.Contents.OfType<TextResourceContents>().ToList();

            return new ModuleResource(uri, texts.FirstOrDefault()?.MimeType, Join(texts.Select(text => text.Text)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Module {Id} could not read {Uri}", id, uri);
            return null;
        }
    }

    /// <summary>Stops every module, ending any that has not exited within a few seconds.</summary>
    public void Dispose()
    {
        List<ModuleRunner> runners;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            runners = [.. _runners.Values, .. _stopping.Values];
            _runners.Clear();
            _stopping.Clear();
        }

        foreach (var runner in runners)
        {
            runner.Stop();
        }

        Task.WaitAny(Task.WhenAll(runners.Select(runner => runner.Ended)), Task.Delay(StopGrace));
        _job.Dispose();
    }

    /// <summary>The strings in <c>"d47/phrases"</c>; anything else there is ignored.</summary>
    private static IReadOnlyList<string> Phrases(JsonObject? meta) =>
        meta?["d47/phrases"] is JsonArray phrases
            ? [.. phrases.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text : null).OfType<string>()]
            : [];

    private static string Join(IEnumerable<string> parts)
    {
        var joined = new StringBuilder();

        foreach (var part in parts)
        {
            joined.Append(joined.Length > 0 ? "\n" : string.Empty).Append(part);
        }

        return joined.ToString();
    }

    private ModuleRunner? Runner(string id)
    {
        lock (_gate)
        {
            return _runners.GetValueOrDefault(id);
        }
    }

    /// <summary>Starts <paramref name="module"/> once <paramref name="previous"/>, if any, has stopped and ended.</summary>
    private void StartLocked(ReadyModule module, ModuleRunner? previous)
    {
        previous?.Stop();

        if (previous is null && _stopping.Remove(module.Manifest.Id, out var stopped))
        {
            previous = stopped;
        }

        var runner = new ModuleRunner(module, _job, Inbox, _loggers, _time, new ModuleRestarts(_restartWaits), Report);
        _runners[module.Manifest.Id] = runner;
        runner.Start(previous?.Ended ?? Task.CompletedTask);
    }

    private void StopLocked(string id)
    {
        if (_runners.Remove(id, out var runner))
        {
            runner.Stop();
            _stopping[id] = runner;
            _logger.LogInformation("Module {Id} stopped", id);
        }
    }

    private void Report(ModuleRunner runner, ModuleStatus status, DateTimeOffset? lastAnswer)
    {
        var reports = new List<ModuleStatusReport>();

        lock (_gate)
        {
            if (_disposed || !_runners.TryGetValue(runner.Id, out var current) || current != runner)
            {
                return;
            }

            SetLocked(new ModuleStatusReport(runner.Module.Folder, runner.Id, status, lastAnswer), reports);
        }

        Raise(reports);
    }

    /// <summary>Records the report, adding it to <paramref name="changed"/> when it differs from the one held.</summary>
    private void SetLocked(ModuleStatusReport report, List<ModuleStatusReport> changed)
    {
        if (_statuses.GetValueOrDefault(report.Folder) != report)
        {
            _statuses[report.Folder] = report;
            changed.Add(report);
        }
    }

    private void Raise(IEnumerable<ModuleStatusReport> reports)
    {
        foreach (var report in reports)
        {
            StatusChanged?.Invoke(report);
        }
    }
}
