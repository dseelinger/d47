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
    /// <param name="personas">
    /// The core host the persona rows read and the introductions row clears. Passed in only by
    /// a test that needs to see the state on both sides of that button; every other caller gets
    /// a fresh one, as the composition root does.
    /// </param>
    /// <param name="voices">
    /// What the speech provider offers. Empty on every normal run, as a headless test has no
    /// provider — passed in by the tests about the Voice row, which cannot say anything about a
    /// row with nothing in it.
    /// </param>
    /// <param name="localVoice">
    /// What the local voice's download button does. A fetch that returns at once on every normal
    /// run, and a controllable one for the test about what the row shows while it is running.
    /// </param>
    /// <param name="flight">
    /// What the audio recorder has kept, which is what makes the Privacy recording row
    /// exist at all (#164). Null on every normal run, and on every test that is not about it.
    /// </param>
    public static (SettingsService Settings, ViewStateStore ViewState, AppPaths Paths, CapabilityRegistry Registry, SecretStore Secrets) CreateFull(
        Func<string>? coverage = null,
        D47.Core.Persona.PersonaHost? personas = null,
        IReadOnlyList<VoiceInfo>? voices = null,
        LongPress? localVoice = null,
        D47.Core.Diagnostics.Recording.RecordingLog? recording = null,
        LongPress? rescan = null)
    {
        var root = TempFolders.Create("d47-app-tests");
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
                Beds = () => [.. CueLibrary.Load().BedNames],
                Voices = _ => [.. (voices ?? []).Select(voice => voice.Id)],
                VoiceLabel = (_, id) => (voices ?? []).FirstOrDefault(voice => voice.Id == id)?.Label ?? id,

                // Supplied rather than left null, because a null host delegate makes its row
                // ABSENT and the surface these tests bind is then not the one that ships. That
                // hole let a crash through in 0.76.0 and let the local voice's missing download
                // button through in 0.84.0.
                LocalVoiceState = () => "Not downloaded. About 350 MB, fetched once.",
                DownloadLocalVoice = () => localVoice ?? ((_, _) => Task.FromResult<string?>(null)),
            },
            new ShipsCapability.ShipsSurface
            {
                // Both supplied, for the reason the speech surface above records: a null host
                // delegate makes its row ABSENT, and a test bound to a surface with no rescan
                // button could not tell a missing button from a working one (#128).
                Remembered = () => "Two ships, the oldest last seen 3 months ago.",
                Rescan = () => rescan ?? ((_, _) => Task.FromResult<string?>(null)),
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
                Report = () => (D47.Core.Vr.VrState.Unavailable, "No SteamVR runtime in a headless test."),
                Nudge = (_, _) => D47.Core.Vr.VrNudgeOutcome.NoHeadset,
            },
            ActionSurface.Inert,
            () => "No autonomous actions in a headless test.",
            NavigationSurface.Inert,
            new D47.Core.Actions.MacroStore(Path.Combine(paths.Data, "macros.json"), NullLogger<D47.Core.Actions.MacroStore>.Instance),
            personas ?? new D47.Core.Persona.PersonaHost(),

            // Real stores over real (empty) files. The trust boundary is two files, and a double
            // that collapsed them into one could not fail the way the shipped thing can.
            new D47.Core.Checklists.ChecklistService(
                new D47.Core.Checklists.ChecklistStore(
                    Path.Combine(paths.Data, "checklist.json"),
                    NullLogger<D47.Core.Checklists.ChecklistStore>.Instance),
                new D47.Core.Checklists.ChecklistProposalStore(
                    Path.Combine(paths.Data, "checklist-proposals.json"),
                    NullLogger<D47.Core.Checklists.ChecklistProposalStore>.Instance),
                () => null),
            coverage: coverage,

            // A real store over a real (empty) file, for the reason the two above are real: the
            // documentation gate reads this registry, and a persona capability built without one
            // registers without its two protected ship-core tools — so the gate would report a
            // fully documented capability while the shipped app carried two tools it had never
            // seen (Phase 35).
            shipCores: new D47.Core.Persona.ShipCoreService(
                new D47.Core.Persona.ShipCoreStore(
                    Path.Combine(paths.Data, "ship-cores.json"),
                    NullLogger<D47.Core.Persona.ShipCoreStore>.Instance),
                () => null),

            // #78: every About delegate supplied, because a null one makes its row *absent* and
            // an absent row is one no test can see. The app supplies all of these, so a registry
            // built without them is not the registry that ships — which is how four button-only
            // rows reached a release that could not start. Same trap the ship cores above are
            // real for, one capability along.
            about: D47.Core.Capabilities.Builtin.AboutSurface.Inert,
            recording: recording));

        built = registry;

        settings.Bind(registry);

        // The registry and the secret store come back too: the guided key setup is built from
        // the real descriptor rows and asks the real store whether a key is present, so a test
        // that cannot reach either could only assert against a copy of them.
        return (settings, new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance), paths, registry, secrets);
    }

    /// <summary>
    /// The three most tests want. Kept beside <see cref="CreateFull"/> rather than replaced by it
    /// because forty-four call sites destructure this shape, and widening it would be forty-four
    /// edits to give every one of them two values it does not use.
    /// </summary>
    public static (SettingsService Settings, ViewStateStore ViewState, AppPaths Paths) Create(
        Func<string>? coverage = null,
        D47.Core.Persona.PersonaHost? personas = null,
        IReadOnlyList<VoiceInfo>? voices = null,
        LongPress? localVoice = null,
        D47.Core.Diagnostics.Recording.RecordingLog? recording = null,
        LongPress? rescan = null)
    {
        var (settings, viewState, paths, _, _) = CreateFull(coverage, personas, voices, localVoice, recording, rescan);
        return (settings, viewState, paths);
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
