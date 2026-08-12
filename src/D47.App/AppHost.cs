using System.Reflection;
using D47.App.Logging;
using D47.App.Updates;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics;
using D47.Core.Journal;
using D47.Llm;
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
    private readonly ILogger<AppHost> _logger;

    private AppHost(
        AppPaths paths,
        ILoggerFactory loggerFactory,
        SerilogVerbosityControl verbosity,
        SettingsService settings,
        SecretStore secrets,
        GameStateStore gameState,
        JournalSpine journal,
        CapabilityRegistry capabilities,
        UpdateChecker updates,
        TurnLoop turns,
        LlmAvailabilityState llmAvailability,
        SpendTracker spend,
        string version,
        string? startupError)
    {
        Paths = paths;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AppHost>();
        Verbosity = verbosity;
        Settings = settings;
        Secrets = secrets;
        GameState = gameState;
        Journal = journal;
        Capabilities = capabilities;
        Updates = updates;
        Turns = turns;
        LlmAvailability = llmAvailability;
        Spend = spend;
        Version = version;
        StartupError = startupError;
    }

    public AppPaths Paths { get; }

    public SerilogVerbosityControl Verbosity { get; }

    /// <summary>
    /// The settings surface. Everything that changes a setting goes through here, whichever
    /// surface asked, which is what makes the protected set enforceable in one place.
    /// </summary>
    public SettingsService Settings { get; }

    public SecretStore Secrets { get; }

    public GameStateStore GameState { get; }

    public JournalSpine Journal { get; }

    public CapabilityRegistry Capabilities { get; }

    public UpdateChecker Updates { get; }

    /// <summary>One turn of conversation, whichever path answers it.</summary>
    public TurnLoop Turns { get; }

    /// <summary>Whether the model is usable right now, and why not when it isn't.</summary>
    public LlmAvailabilityState LlmAvailability { get; }

    /// <summary>Per-turn cost and the running total.</summary>
    public SpendTracker Spend { get; }

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

        var store = new SettingsStore(paths, loggerFactory.CreateLogger<SettingsStore>());
        var loaded = new D47Settings();
        string? startupError = null;
        try
        {
            loaded = store.Load();
        }
        catch (SettingsLoadException ex)
        {
            startupError = ex.Message;
            logger.LogCritical(ex, "Settings could not be loaded; continuing on defaults");
        }

        verbosity.Apply(loaded.Logging);

        var secrets = new SecretStore(
            paths,
            new DpapiSecretProtector(),
            loggerFactory.CreateLogger<SecretStore>());

        var settings = new SettingsService(store, secrets, loaded, loggerFactory.CreateLogger<SettingsService>());

        // From here a level change is live wherever it came from — panel, tool or settings file.
        verbosity.FollowSettings(settings);

        var journalDirectory = ResolveJournalDirectory();
        var gameState = new GameStateStore();
        var journal = new JournalSpine(journalDirectory, gameState, loggerFactory);

        // One tick now, at startup, rather than a repeating timer: nothing in the app yet
        // drives a recurring cadence, and adding one ahead of Phase 3's real turn loop would
        // be structure this phase does not need. Reading once still means a journal already on
        // disk when d47 starts is answered correctly, backlog and all.
        journal.Poll();
        logger.LogInformation(
            "Journal folder {Directory}; tailing {File}",
            journalDirectory,
            journal.CurrentFile ?? "(none found)");

        // Availability and spend exist before the registry because capabilities report on them;
        // the provider itself is built afterwards, from settings, by ApplyLlmSettings.
        var llmAvailability = new LlmAvailabilityState(providerConfigured: false);
        var spend = new SpendTracker();

        var capabilities = CapabilityRegistry.Build(
            BuiltinCapabilities.All(paths, verbosity, gameState, settings, llmAvailability, spend, version));

        // The one late-bound edge in the composition: descriptors declare the settings rows and
        // some descriptors read settings, so the row table is supplied once the registry exists.
        settings.Bind(capabilities);

        logger.LogInformation(
            "Registered {Count} capabilities exposing {ToolCount} tools",
            capabilities.All.Count,
            capabilities.ToolNames.Count());

        var updates = new UpdateChecker(loggerFactory.CreateLogger<UpdateChecker>());

        var turns = new TurnLoop(
            capabilities,
            new KeywordRouter(capabilities),
            llmAvailability,
            spend,
            PriceTable.Default,
            loggerFactory.CreateLogger<TurnLoop>(),
            settings: settings);

        var host = new AppHost(
            paths,
            loggerFactory,
            verbosity,
            settings,
            secrets,
            gameState,
            journal,
            capabilities,
            updates,
            turns,
            llmAvailability,
            spend,
            version,
            startupError);

        host.ApplyLlmSettings();

        // From here on, a setting takes effect because it changed — not because something was
        // restarted (list.md Phase 4, "Apply every setting without a restart").
        settings.Changed += host.OnSettingsChanged;

        return host;
    }

    /// <summary>
    /// Where the Anthropic key lives in the secret store. DPAPI-encrypted, scoped to this
    /// Windows account, and never written to a log.
    /// </summary>
    public const string AnthropicApiKeySecret = "anthropic.apiKey";

    /// <summary>
    /// Rebuilds everything downstream of the language model settings: the provider itself, the
    /// pinned model, the standing About Me text, and whether the model capability is on at all.
    /// Called at startup and again whenever one of those settings changes, so the two paths
    /// cannot drift.
    /// </summary>
    private void ApplyLlmSettings()
    {
        var current = Settings.Current;
        var selected = LlmProviderCatalog.Selected(current.Llm.Provider);

        ILlmProvider? provider = null;
        string? reason = null;

        if (selected.Id == LlmProviderCatalog.NoneId)
        {
            reason = "No language model is selected — that is a setting, not a fault.";
        }
        else if (ResolveKey(selected) is { } resolved)
        {
            provider = selected.Id switch
            {
                LlmProviderCatalog.AnthropicId => new AnthropicLlmProvider(resolved.Key, current.Llm.Endpoint),
                _ => null,
            };

            if (provider is null)
            {
                reason = $"d47 has no client for {selected.Name} yet.";
            }
            else
            {
                _logger.LogInformation(
                    "{Provider} configured from {Source}, endpoint {Endpoint}",
                    selected.Name,
                    resolved.Source,
                    current.Llm.Endpoint ?? selected.DefaultEndpoint ?? "(provider default)");
            }
        }
        else
        {
            reason = $"No {selected.Name} API key is stored. Add one in Settings.";
        }

        Turns.Provider = provider;
        Turns.Model = current.Llm.Model;
        Turns.AboutMe = current.Llm.AboutMe;

        // The persona block itself arrives in Phase 11; this is the switch it will read.
        if (!current.Llm.PersonalityEnabled)
        {
            Turns.Persona = null;
        }

        LlmAvailability.SetProviderConfigured(provider is not null, reason);
    }

    private void OnSettingsChanged(SettingsChanged change)
    {
        if (change.Key.StartsWith("llm.", StringComparison.OrdinalIgnoreCase))
        {
            ApplyLlmSettings();
        }
    }

    /// <summary>
    /// The secret store is the real home for a key. The environment variable stays supported
    /// as the way to run d47 from a shell that already has one, and the store wins when both
    /// are present.
    /// <para>
    /// Only the <em>source</em> is ever logged, never the key.
    /// </para>
    /// </summary>
    private (string Key, string Source)? ResolveKey(LlmProviderInfo provider)
    {
        if (provider.KeySecretName is not { } name)
        {
            return null;
        }

        if (Secrets.TryGet(name, out var stored))
        {
            return (stored, "the secret store");
        }

        // Only Anthropic has a conventional environment variable worth honouring.
        if (provider.Id != LlmProviderCatalog.AnthropicId)
        {
            return null;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        return string.IsNullOrWhiteSpace(fromEnvironment)
            ? null
            : (fromEnvironment, "the ANTHROPIC_API_KEY environment variable");
    }

    /// <summary>
    /// The real Elite Dangerous journal folder, unless overridden — useful for developing and
    /// testing d47 without needing a live game session.
    /// </summary>
    private static string ResolveJournalDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("D47_JOURNAL_DIR");
        return string.IsNullOrWhiteSpace(overridePath) ? JournalFolder.DefaultPath() : overridePath;
    }

    public void Dispose()
    {
        Settings.Changed -= OnSettingsChanged;
        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
