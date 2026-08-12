using System.Reflection;
using D47.App.Logging;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace D47.App;

/// <summary>
/// The composition root. Startup order matters in one place only: logging comes up before
/// anything that could fail, so a failure has somewhere to go.
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    private AppHost(
        AppPaths paths,
        ILoggerFactory loggerFactory,
        SerilogVerbosityControl verbosity,
        D47Settings settings,
        SecretStore secrets,
        CapabilityRegistry capabilities,
        string version,
        string? startupError)
    {
        Paths = paths;
        _loggerFactory = loggerFactory;
        Verbosity = verbosity;
        Settings = settings;
        Secrets = secrets;
        Capabilities = capabilities;
        Version = version;
        StartupError = startupError;
    }

    public AppPaths Paths { get; }

    public SerilogVerbosityControl Verbosity { get; }

    public D47Settings Settings { get; }

    public SecretStore Secrets { get; }

    public CapabilityRegistry Capabilities { get; }

    public string Version { get; }

    /// <summary>
    /// Set when settings could not be loaded. Surfaced on the panel rather than swallowed:
    /// starting on defaults without saying so would discard the Commander's configuration
    /// silently, which is the failure mode the two-store split exists to prevent.
    /// </summary>
    public string? StartupError { get; }

    public static AppHost Start()
    {
        var paths = AppPaths.BesideExecutable();
        paths.EnsureCreated();

        var version = Assembly.GetEntryAssembly()
                          ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                          ?.InformationalVersion
                      ?? "unknown";

        // Logging first, so everything below has somewhere to report a failure.
        var verbosity = new SerilogVerbosityControl();
        Log.Logger = LoggingSetup.Create(paths, verbosity);
        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger<AppHost>();

        logger.LogInformation("d47 {Version} starting; data folder {Data}", version, paths.Data);

        var settings = new D47Settings();
        string? startupError = null;
        try
        {
            settings = new SettingsStore(paths, loggerFactory.CreateLogger<SettingsStore>()).Load();
        }
        catch (SettingsLoadException ex)
        {
            startupError = ex.Message;
            logger.LogCritical(ex, "Settings could not be loaded; continuing on defaults");
        }

        verbosity.Apply(settings.Logging);

        var secrets = new SecretStore(
            paths,
            new DpapiSecretProtector(),
            loggerFactory.CreateLogger<SecretStore>());

        var capabilities = CapabilityRegistry.Build(BuiltinCapabilities.All(paths, verbosity, version));

        logger.LogInformation(
            "Registered {Count} capabilities exposing {ToolCount} tools",
            capabilities.All.Count,
            capabilities.ToolNames.Count());

        return new AppHost(paths, loggerFactory, verbosity, settings, secrets, capabilities, version, startupError);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
