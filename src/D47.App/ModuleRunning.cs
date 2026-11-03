using D47.Core.Configuration;
using D47.Core.Modules;
using D47.Core.Storage;
using D47.Modules;
using Microsoft.Extensions.Logging;

namespace D47.App;

/// <summary>
/// Reads the installed modules off the tick, then keeps each one running while its checkbox is set, and holds the
/// tools each running module offers.
/// </summary>
internal sealed class ModuleRunning : IModuleTools, IDisposable
{
    private readonly ModuleCatalog _catalog;
    private readonly SettingsService _settings;
    private readonly ILogger<ModuleRunning> _logger;
    private readonly Lock _toolsGate = new();
    private readonly Dictionary<string, IReadOnlyList<ModuleToolOffer>> _toolsById = new(StringComparer.Ordinal);

    /// <summary>Counts each module's status changes, so a tool list read before the latest one is not kept.</summary>
    private readonly Dictionary<string, int> _statusCounts = new(StringComparer.Ordinal);
    private IReadOnlyList<ModuleToolOffer> _tools = [];
    private volatile bool _loaded;

    public ModuleRunning(IFileSystem files, string modulesFolder, SettingsService settings, ILoggerFactory loggers)
    {
        _catalog = new ModuleCatalog(files, modulesFolder);
        _settings = settings;
        _logger = loggers.CreateLogger<ModuleRunning>();
        Host = new ModuleHost(loggers);
        Host.StatusChanged += report =>
        {
            Track(report);
            Changed?.Invoke();
        };
    }

    /// <summary>Raised on any thread when the installed list or a module's status changes.</summary>
    public event Action? Changed;

    public ModuleHost Host { get; }

    public IReadOnlyList<InstalledModule> Installed => _catalog.Modules;

    public IReadOnlyList<ModuleStatusReport> Statuses => Host.Statuses;

    public IReadOnlyList<ModuleToolOffer> Tools
    {
        get
        {
            lock (_toolsGate)
            {
                return _tools;
            }
        }
    }

    public void Restart(string id) => Host.Restart(id);

    public string? InstalledName(string moduleId) =>
        _catalog.Modules.OfType<ReadyModule>().FirstOrDefault(module => module.Manifest.Id == moduleId)?.Manifest.Name;

    public bool IsRunning(string moduleId) =>
        Host.Statuses.Any(report => report.Id == moduleId && report.Status is ModuleStatus.Running or ModuleStatus.NotResponding);

    public async Task<ModuleCallResult> CallAsync(
        string moduleId, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellation)
    {
        var result = await Host.CallToolAsync(moduleId, tool, arguments, cancellation).ConfigureAwait(false);
        return new ModuleCallResult(result.IsError, result.Text);
    }

    public void Start()
    {
        _settings.Changed += OnSettingsChanged;

        _ = Task.Run(() =>
        {
            try
            {
                var installed = _catalog.Reload();
                _logger.LogInformation(
                    "{Ready} modules ready and {Refused} refused",
                    installed.OfType<ReadyModule>().Count(),
                    installed.OfType<RefusedModule>().Count());

                foreach (var refused in installed.OfType<RefusedModule>())
                {
                    _logger.LogWarning("Module in {Folder} refused: {Reason}. {Detail}", refused.Folder, refused.Reason, refused.Detail);
                }

                _loaded = true;
                Apply();
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not start the installed modules");
            }
        });
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        Host.Dispose();
    }

    /// <summary>Applies the checkboxes off the caller's thread, which may be the tick.</summary>
    private void OnSettingsChanged(SettingsChanged change)
    {
        if (_loaded)
        {
            _ = Task.Run(Apply);
        }
    }

    /// <summary>Lists a module's tools off the caller's thread when it starts running, and forgets them when it stops.</summary>
    private void Track(ModuleStatusReport report)
    {
        if (report.Id is not { } id || report.Status == ModuleStatus.NotResponding)
        {
            return;
        }

        int count;

        lock (_toolsGate)
        {
            count = _statusCounts[id] = _statusCounts.GetValueOrDefault(id) + 1;
        }

        if (report.Status != ModuleStatus.Running)
        {
            SetTools(id, count, null);
            return;
        }

        _ = Task.Run(async () =>
        {
            var tools = await Host.ListToolsAsync(id, CancellationToken.None).ConfigureAwait(false);
            var name = InstalledName(id) ?? id;

            SetTools(id, count, [
                .. tools.Select(tool => new ModuleToolOffer(
                    id,
                    name,
                    tool.Name,
                    tool.Description,
                    tool.InputSchema.ValueKind == System.Text.Json.JsonValueKind.Undefined ? string.Empty : tool.InputSchema.GetRawText(),
                    tool.Phrases)),
            ]);
        });
    }

    /// <summary>Replaces a module's tools unless its status has changed since <paramref name="count"/>.</summary>
    private void SetTools(string id, int count, IReadOnlyList<ModuleToolOffer>? tools)
    {
        lock (_toolsGate)
        {
            if (_statusCounts.GetValueOrDefault(id) != count)
            {
                return;
            }

            if (tools is not null)
            {
                _toolsById[id] = tools;
            }
            else if (!_toolsById.Remove(id))
            {
                return;
            }

            _tools = [.. _toolsById.Values.SelectMany(offers => offers)];
        }
    }

    private void Apply()
    {
        var current = _settings.Current;
        Host.Apply(_catalog.Modules, id => current.ModuleFor(id).Enabled);
    }
}
