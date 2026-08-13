using System.Diagnostics.CodeAnalysis;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Tests;

/// <summary>A throwaway folder standing in for an install directory.</summary>
public sealed class TempInstall : IDisposable
{
    public TempInstall()
    {
        Root = Path.Combine(Path.GetTempPath(), "d47-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new AppPaths(Root);
        Paths.EnsureCreated();
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }
    }
}

/// <summary>
/// Reversible "encryption". Keeps the store's behaviour testable without a real DPAPI blob,
/// which is per-user and per-machine and therefore useless in CI.
/// </summary>
public sealed class ReversibleProtector : ISecretProtector
{
    public byte[] Protect(byte[] plaintext) => plaintext.Select(b => (byte)(b ^ 0x5A)).ToArray();

    public bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext)
    {
        plaintext = ciphertext.Select(b => (byte)(b ^ 0x5A)).ToArray();
        return true;
    }
}

/// <summary>Stands in for a blob written by another user or on another machine.</summary>
public sealed class NeverUnprotects : ISecretProtector
{
    public byte[] Protect(byte[] plaintext) => plaintext;

    public bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext)
    {
        plaintext = null;
        return false;
    }
}

public sealed class FakeVerbosityControl : ILogVerbosityControl
{
    private readonly Dictionary<string, LogLevel> _levels =
        Subsystems.All.ToDictionary(s => s, _ => LogLevel.Information, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, LogLevel> Levels => _levels;

    public LogLevel DefaultLevel { get; private set; } = LogLevel.Information;

    public void Set(string subsystem, LogLevel level) =>
        _levels[Subsystems.Canonical(subsystem) ?? throw new ArgumentException(subsystem)] = level;

    public void SetDefault(LogLevel level) => DefaultLevel = level;
}

/// <summary>Captures what would have been logged, so a test can assert a warning happened.</summary>
public sealed class RecordingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

/// <summary>
/// The composition the app performs at startup, in a throwaway folder: both stores, a settings
/// service, the builtin capability set, and the row table bound to it in that order. Tests
/// that touch capabilities or settings need all of it, and assembling it by hand in each one is
/// how the wiring order quietly drifts from the composition root's.
/// </summary>
public sealed class TestSurface
{
    private TestSurface(
        AppPaths paths,
        SettingsStore store,
        SecretStore secrets,
        SettingsService settings,
        CapabilityRegistry registry,
        GameStateStore gameState,
        LlmAvailabilityState availability,
        SpendTracker spend,
        FakeVerbosityControl verbosity)
    {
        Verbosity = verbosity;
        Paths = paths;
        Store = store;
        Secrets = secrets;
        Settings = settings;
        Registry = registry;
        GameState = gameState;
        Availability = availability;
        Spend = spend;
    }

    public const string Version = "1.0.0-test";

    public AppPaths Paths { get; }

    public SettingsStore Store { get; }

    public SecretStore Secrets { get; }

    public SettingsService Settings { get; }

    public CapabilityRegistry Registry { get; }

    public GameStateStore GameState { get; }

    public LlmAvailabilityState Availability { get; }

    public SpendTracker Spend { get; }

    public FakeVerbosityControl Verbosity { get; }

    public KeywordRouter Router => new(Registry);

    public static TestSurface For(
        TempInstall install,
        GameStateStore? gameState = null,
        D47Settings? settings = null)
    {
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance);
        var state = gameState ?? new GameStateStore();
        var availability = new LlmAvailabilityState(providerConfigured: false);
        var spend = new SpendTracker();

        // Loaded, not defaulted: a second surface over the same folder is a restart, which is
        // how tests assert that a change outlived the process that made it.
        var service = new SettingsService(
            store, secrets, settings ?? store.Load(), NullLogger<SettingsService>.Instance);

        var verbosity = new FakeVerbosityControl();

        CapabilityRegistry? built = null;

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths, verbosity, state, service, availability, spend, Version, SilentSpeech(), new D47.Core.Conversation.TurnCancellation(NullLogger<D47.Core.Conversation.TurnCancellation>.Instance),
            new D47.Core.Callouts.CalloutEngine(NullLogger<D47.Core.Callouts.CalloutEngine>.Instance),
            () => built!,
            SilentListening(),
            NoHeadset(),
            D47.Core.Capabilities.Builtin.ActionSurface.Inert));

        built = registry;

        service.Bind(registry);

        // The same binding the composition root performs, for the same reason: a log level
        // change has to become live wherever it came from.
        verbosity.Apply(service.Current.Logging);
        verbosity.FollowSettings(service);

        return new TestSurface(
            install.Paths, store, secrets, service, registry, state, availability, spend, verbosity);
    }

    /// <summary>
    /// The headset surface on a machine with none. Unavailable rather than Connecting, because
    /// a test that never reaches a runtime should read as a machine that has none rather than
    /// as one still looking.
    /// </summary>
    public static Capabilities.Builtin.VrCapability.HeadsetSurface NoHeadset() => new()
    {
        Report = () => (D47.Core.Vr.VrState.Unavailable, "No SteamVR runtime in a test.", null),
        Reanchor = () => 0,
    };

    /// <summary>
    /// The speech capability's surface with no sound card behind it. The bed names still come
    /// from the real shipped set, so the settings row is exercised against what actually ships
    /// rather than against a list invented for the test.
    /// </summary>
    public static Capabilities.Builtin.SpeechCapability.SpeechSurface SilentSpeech(
        Action? onSilence = null) => new()
    {
        Silence = onSilence ?? (() => { }),
        Beds = [.. D47.Core.Audio.CueLibrary.Load().BedNames],
    };

    /// <summary>
    /// The listening surface with no microphone behind it: no devices, not capturing, no
    /// transcriber, no binds. Every one of those is a real state d47 has to answer for, so the
    /// double exercises an unconfigured machine rather than an invented happy path.
    /// </summary>
    public static Capabilities.Builtin.ListeningCapability.ListeningSurface SilentListening() => new()
    {
        InputDevices = () => [],
        DeviceLabel = id => id,
        CaptureState = () => (false, "No microphone in a test."),
        TranscriberState = () => (false, null, "No transcriber in a test."),
        Binds = () => D47.Core.Input.EliteBinds.None,
        InstalledModels = () => [],
    };
}

/// <summary>
/// A clock that does not pass. <see cref="Waited"/> records what the retry policy asked for,
/// so a backoff schedule is assertable without a test ever spending it — which is the reason
/// no Core component reads the clock directly (architecture.md §8).
/// </summary>
public sealed class InstantClock : D47.Core.Conversation.ITurnClock
{
    public List<TimeSpan> Waited { get; } = [];

    /// <summary>When set, the next attempt's timeout trips immediately — a provider that hangs.</summary>
    public bool TimeOutImmediately { get; set; }

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        Waited.Add(duration);
        return Task.CompletedTask;
    }

    public CancellationTokenSource CreateTimeout(TimeSpan duration, CancellationToken linkedTo)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);

        if (TimeOutImmediately)
        {
            source.Cancel();
        }

        return source;
    }
}
