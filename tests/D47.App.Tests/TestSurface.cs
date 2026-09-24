using D47.Core;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics.Donation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>The wiring the composition root performs, in a throwaway folder.</summary>
public static class TestSurface
{
    /// <summary>Where the render captures land.</summary>
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

    /// <summary>
    /// A <c>HelpImproveWindow</c> build delegate that renders <paramref name="text"/> and reports an empty
    /// tally — for a test that is not about the four figures (#338).
    /// </summary>
    public static Func<ExcerptRequest, (string Text, ExcerptTally Tally)> Excerpt(string text) =>
        _ => (text, new ExcerptTally(0, 0, 0, 0, 0, false, 0, 0));

    /// <summary>The same wrapping, over a build that varies by request.</summary>
    public static Func<ExcerptRequest, (string Text, ExcerptTally Tally)> Excerpt(
        Func<ExcerptRequest, string> build) =>
        request => (build(request), new ExcerptTally(0, 0, 0, 0, 0, false, 0, 0));

    /// <summary>Settings, view state, paths, registry and secrets, wired as the composition root wires them.</summary>
    /// <param name="coverage">A hand-testing coverage summary, which is what makes the Diagnostics coverage row exist at all.</param>
    /// <param name="personas">The core host the persona rows read and the introductions row clears.</param>
    /// <param name="voices">What the speech provider offers.</param>
    /// <param name="ticking">The loop the diagnostics card names a paused subscriber from (#58).</param>
    public static (SettingsService Settings, ViewStateStore ViewState, AppPaths Paths, CapabilityRegistry Registry, SecretStore Secrets) CreateFull(
        Func<string>? coverage = null,
        D47.Core.Persona.PersonaHost? personas = null,
        IReadOnlyList<VoiceInfo>? voices = null,
        LongPress? localVoice = null,
        D47.Core.Diagnostics.Recording.RecordingLog? recording = null,
        LongPress? rescan = null,
        D47.Core.Ticking.TickLoop? ticking = null,

        // Appended, like every optional here: the composition root passes these positionally.
        Func<string>? resetVoices = null,
        Func<string, VoiceRole, CancellationToken, Task>? audition = null)
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
                VoiceGender = (_, id) => (voices ?? []).FirstOrDefault(voice => voice.Id == id)?.Gender,
                Audition = audition,
                KeyStored = settings.HoldsSecret,

                // Supplied rather than left null, because a null host delegate makes its row ABSENT and the
                // surface these tests bind is then not the one that ships.
                LocalVoiceState = () => "Not downloaded. About 350 MB, fetched once.",
                DownloadLocalVoice = () => localVoice ?? ((_, _) => Task.FromResult<string?>(null)),

                // Supplied rather than left null, for the same reason as the local voice download above.
                ResetVoices = () => (_, _) => Task.FromResult<string?>(resetVoices?.Invoke() ?? string.Empty),
            },
            new ShipsCapability.ShipsSurface
            {
                // Both supplied, for the reason the speech surface above records: a null host delegate makes
                // its row ABSENT, and a test bound to a surface with no rescan button could not tell a
 // missing button from a working one.
                Remembered = () => "Two ships, the oldest last seen 3 months ago.",
                Rescan = () => rescan ?? ((_, _) => Task.FromResult<string?>(null)),
            },
            SpokenNamesSurface.Inert,
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

            // Real stores over real (empty) files.
            new D47.Core.Checklists.ChecklistService(
                new D47.Core.Checklists.ChecklistStore(
                    Path.Combine(paths.Data, "checklist.json"),
                    NullLogger<D47.Core.Checklists.ChecklistStore>.Instance),
                new D47.Core.Checklists.ChecklistProposalStore(
                    Path.Combine(paths.Data, "checklist-proposals.json"),
                    NullLogger<D47.Core.Checklists.ChecklistProposalStore>.Instance),
                () => null),
            coverage: coverage,

            // A real store over a real (empty) file, for the reason the two above are real: the documentation
            // gate reads this registry, and a persona capability built without one registers without its two
            // protected ship-core tools — so the gate would report a fully documented capability while the
 // shipped app carried two tools it had never seen.
            shipCores: new D47.Core.Persona.ShipCoreService(
                new D47.Core.Persona.ShipCoreStore(
                    Path.Combine(paths.Data, "ship-cores.json"),
                    NullLogger<D47.Core.Persona.ShipCoreStore>.Instance),
                () => null),

            // #78: every About delegate supplied, because a null one makes its row *absent* and an absent row
            // is one no test can see.
            about: D47.Core.Capabilities.Builtin.AboutSurface.Inert,
            recording: recording,

            // Always a real loop, for the reason the surfaces above are real: a null one makes the paused
            // row absent, and a test could then not tell a missing row from a working one.
            ticking: ticking ?? new D47.Core.Ticking.TickLoop(NullLogger<D47.Core.Ticking.TickLoop>.Instance)));

        built = registry;

        settings.Bind(registry);

        // The registry and the secret store come back too: the guided key setup is built from the real
        // descriptor rows and asks the real store whether a key is present, so a test that cannot reach
        // either could only assert against a copy of them.
        return (settings, new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance), paths, registry, secrets);
    }

    /// <summary>The three most tests want.</summary>
    public static (SettingsService Settings, ViewStateStore ViewState, AppPaths Paths) Create(
        Func<string>? coverage = null,
        D47.Core.Persona.PersonaHost? personas = null,
        IReadOnlyList<VoiceInfo>? voices = null,
        LongPress? localVoice = null,
        D47.Core.Diagnostics.Recording.RecordingLog? recording = null,
        LongPress? rescan = null,
        Func<string>? resetVoices = null,
        Func<string, VoiceRole, CancellationToken, Task>? audition = null)
    {
        var (settings, viewState, paths, _, _) = CreateFull(
            coverage, personas, voices, localVoice, recording, rescan, resetVoices: resetVoices, audition: audition);
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
