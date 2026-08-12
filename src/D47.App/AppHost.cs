using System.Reflection;
using D47.App.Logging;
using D47.App.Updates;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
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

    private AppHost(
        AppPaths paths,
        ILoggerFactory loggerFactory,
        SerilogVerbosityControl verbosity,
        D47Settings settings,
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

    public D47Settings Settings { get; }

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

        var capabilities = CapabilityRegistry.Build(BuiltinCapabilities.All(paths, verbosity, gameState, version));

        logger.LogInformation(
            "Registered {Count} capabilities exposing {ToolCount} tools",
            capabilities.All.Count,
            capabilities.ToolNames.Count());

        var updates = new UpdateChecker(loggerFactory.CreateLogger<UpdateChecker>());

        // The provider is built only if there is a key to build it with. No key is not an error
        // state: it produces a null provider, which the turn loop reads as "the model capability
        // is off" and routes around (list.md Phase 3, "Capabilities as state, not guard").
        ILlmProvider? provider = null;
        if (!string.Equals(settings.Llm.Provider, "none", StringComparison.OrdinalIgnoreCase)
            && ResolveAnthropicKey(secrets) is { } resolved)
        {
            provider = new AnthropicLlmProvider(resolved.Key);
            logger.LogInformation("Anthropic provider configured from {Source}", resolved.Source);
        }

        var llmAvailability = new LlmAvailabilityState(provider is not null);
        var spend = new SpendTracker();

        var turns = new TurnLoop(
            capabilities,
            new KeywordRouter(capabilities),
            llmAvailability,
            spend,
            PriceTable.Default,
            loggerFactory.CreateLogger<TurnLoop>(),
            provider,
            settings.Llm.Model)
        {
            AboutMe = settings.Llm.AboutMe,
        };

        logger.LogInformation(
            "Conversation ready; model {State} ({Provider}/{Model})",
            llmAvailability.Current,
            provider?.Id ?? "none",
            settings.Llm.Model ?? provider?.DefaultModel ?? "none");

        return new AppHost(
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
    }

    /// <summary>
    /// Where the Anthropic key lives in the secret store. DPAPI-encrypted, scoped to this
    /// Windows account, and never written to a log.
    /// </summary>
    public const string AnthropicApiKeySecret = "anthropic.apiKey";

    /// <summary>
    /// The secret store is the real home for the key, but nothing can write to it until the
    /// settings surface exists in Phase 4 — so the environment variable is the interim way to
    /// configure a model at all. The store wins when both are present.
    /// <para>
    /// Only the <em>source</em> is ever logged, never the key.
    /// </para>
    /// </summary>
    private static (string Key, string Source)? ResolveAnthropicKey(SecretStore secrets)
    {
        if (secrets.TryGet(AnthropicApiKeySecret, out var stored))
        {
            return (stored, "the secret store");
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
        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
