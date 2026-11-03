using D47.Core.Configuration;
using D47.Core.Modules;
using D47.Core.Storage;
using D47.Modules;
using Microsoft.Extensions.Logging;

namespace D47.App;

/// <summary>Reads the installed modules off the tick, then keeps each one running while its checkbox is set.</summary>
internal sealed class ModuleRunning : IDisposable
{
    private readonly ModuleCatalog _catalog;
    private readonly SettingsService _settings;
    private readonly ILogger<ModuleRunning> _logger;
    private volatile bool _loaded;

    public ModuleRunning(IFileSystem files, string modulesFolder, SettingsService settings, ILoggerFactory loggers)
    {
        _catalog = new ModuleCatalog(files, modulesFolder);
        _settings = settings;
        _logger = loggers.CreateLogger<ModuleRunning>();
        Host = new ModuleHost(loggers);
        Host.StatusChanged += _ => Changed?.Invoke();
    }

    /// <summary>Raised on any thread when the installed list or a module's status changes.</summary>
    public event Action? Changed;

    public ModuleHost Host { get; }

    public IReadOnlyList<InstalledModule> Installed => _catalog.Modules;

    public IReadOnlyList<ModuleStatusReport> Statuses => Host.Statuses;

    public void Restart(string id) => Host.Restart(id);

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

    private void Apply()
    {
        var current = _settings.Current;
        Host.Apply(_catalog.Modules, id => current.ModuleFor(id).Enabled);
    }
}
