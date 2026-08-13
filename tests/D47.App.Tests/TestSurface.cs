using D47.Core;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>
/// The wiring the composition root performs, in a throwaway folder. Shared by every headless
/// UI test so they all render against the real builtin registry rather than a hand-made
/// subset — a settings surface built from a stub proves nothing about the one that ships.
/// </summary>
public static class TestSurface
{
    /// <summary>
    /// Where the render captures land. Unique per test run, because the path used to be a fixed
    /// folder and two runs at once — a second session in a worktree, or a rerun started before
    /// the first finished — then wrote over each other's PNGs and left a set nobody could trust.
    /// The timestamp sorts, the process id disambiguates two runs in the same second, and both
    /// capture tests share the one directory so a run's output stays together.
    /// </summary>
    private static readonly Lazy<string> Captures = new(() =>
    {
        var run = Path.Combine(
            Path.GetTempPath(),
            "d47-ui-captures",
            $"{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}");
        Directory.CreateDirectory(run);
        return run;
    });

    /// <summary>This run's capture folder, created on first use.</summary>
    public static string CaptureDirectory => Captures.Value;

    /// <summary>The wiring the composition root performs, in a throwaway folder.</summary>
    /// <param name="coverage">
    /// A hand-testing coverage summary, which is what makes the Diagnostics coverage row exist
    /// at all. Null on every normal run, and on every test that is not about that row.
    /// </param>
    public static (SettingsService Settings, ViewStateStore ViewState, AppPaths Paths) Create(
        Func<string>? coverage = null)
    {
        var root = Directory.CreateTempSubdirectory("d47-app-tests").FullName;
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        var store = new SettingsStore(paths, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(paths, new NoopProtector(), NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(store, secrets, store.Load(), NullLogger<SettingsService>.Instance);

        CapabilityRegistry? built = null;

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            paths,
            new NoopVerbosity(),
            new GameStateStore(),
            settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-uitest",
            new SpeechCapability.SpeechSurface
            {
                Silence = () => { },
                Beds = [.. CueLibrary.Load().BedNames],
            },
            new TurnCancellation(NullLogger<TurnCancellation>.Instance),
            new CalloutEngine(NullLogger<CalloutEngine>.Instance),
            () => built!,
            new ListeningCapability.ListeningSurface
            {
                InputDevices = () => [],
                DeviceLabel = id => id,
                CaptureState = () => (false, "No microphone in a headless test."),
                TranscriberState = () => (false, null, "No transcriber in a headless test."),
                Binds = () => EliteBinds.None,
                InstalledModels = () => [],
            },
            new VrCapability.HeadsetSurface
            {
                Report = () => (D47.Core.Vr.VrState.Unavailable, "No SteamVR runtime in a headless test.", null),
                Reanchor = () => 0,
            },
            coverage));

        built = registry;

        settings.Bind(registry);

        return (settings, new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance), paths);
    }

    /// <summary>Just the settings service, for tests that need nowhere to put a view state.</summary>
    public static SettingsService Settings() => Create().Settings;

    private sealed class NoopProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;

        public bool TryUnprotect(byte[] ciphertext, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? plaintext)
        {
            plaintext = ciphertext;
            return true;
        }
    }

    private sealed class NoopVerbosity : D47.Core.Diagnostics.ILogVerbosityControl
    {
        private readonly Dictionary<string, LogLevel> _levels =
            D47.Core.Diagnostics.Subsystems.All.ToDictionary(s => s, _ => LogLevel.Information, StringComparer.Ordinal);

        public IReadOnlyDictionary<string, LogLevel> Levels => _levels;

        public void Set(string subsystem, LogLevel level) => _levels[subsystem] = level;

        public void SetDefault(LogLevel level)
        {
        }
    }
}
