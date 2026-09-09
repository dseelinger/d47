using System.Security.Cryptography;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Lore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Scenarios.Tests;

/// <summary>
/// One scenario's world: a real <see cref="SettingsService"/>, a real <see cref="SecretStore"/> and the
/// real builtin <see cref="CapabilityRegistry"/>, over a temporary <c>data/</c> tree.
/// </summary>
public sealed class ScenarioWorld : IDisposable
{
    private readonly TempInstall _install = new();

    private readonly List<(string Tool, string ArgumentsJson, bool Succeeded)> _ran = [];

    private readonly Dictionary<string, string> _hostileResults = new(StringComparer.Ordinal);

    private readonly List<TracedSettingApply> _applies = [];

    /// <summary>What d47 would have pressed.</summary>
    private readonly D47.Core.Input.RecordingGameInput _input = new()
    {
        IsGameRunning = true,
        IsGameForeground = true,
    };

    public ScenarioWorld()
    {
        var store = new SettingsStore(_install.Paths, NullLogger<SettingsStore>.Instance);
        Secrets = new SecretStore(_install.Paths, new PlainProtector(), NullLogger<SecretStore>.Instance);
        Settings = new SettingsService(store, Secrets, store.Load(), NullLogger<SettingsService>.Instance);

        GameState = new GameStateStore();

        // A real book over a real (empty) file: a null book answers every attempt with "I have nowhere to keep notes", a pass this suite would not have earned.
        Lore = new LoreBook(new LoreStore(
            Path.Combine(_install.Paths.Data, "lore.json"),
            NullLogger<LoreStore>.Instance));

        CapabilityRegistry? built = null;

        var descriptors = BuiltinCapabilities.All(
            _install.Paths,
            new NoopVerbosity(),
            GameState,
            Settings,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            "0.30.0-scenarios",
            new SpeechCapability.SpeechSurface
            {
                Silence = () => { },
                Beds = () => [],
                Voices = _ => [],
                VoiceLabel = (_, id) => id,
            },
            ShipsCapability.ShipsSurface.Inert,
            SpokenNamesSurface.Inert,
            new TurnCancellation(NullLogger<TurnCancellation>.Instance),
            new D47.Core.Callouts.CalloutEngine(NullLogger<D47.Core.Callouts.CalloutEngine>.Instance),
            () => built!,
            new ListeningCapability.ListeningSurface
            {
                InputDevices = () => [],
                DeviceLabel = id => id,
                CaptureState = () => (false, "No microphone in a scenario run."),
                TranscriberState = () => (false, null, "No transcriber in a scenario run."),
                Binds = () => D47.Core.Input.EliteBinds.None,
                InstalledModels = () => [],
            },
            new VrCapability.HeadsetSurface
            {
                Report = () => (D47.Core.Vr.VrState.Unavailable, "No SteamVR runtime in a scenario run."),
                Nudge = (_, _) => D47.Core.Vr.VrNudgeOutcome.NoHeadset,
            },
            // NOT ActionSurface.Inert, and that is the point.
            new ActionSurface
            {
                Binds = () => Bound,
                Status = () => LiveStatus,
                Input = _input,
                Enabled = () => ActionsEnabled,
            },
            () => "No autonomous actions in a scenario run.",
            NavigationSurface.Inert,
            new D47.Core.Actions.MacroStore(
                Path.Combine(_install.Paths.Data, "macros.json"),
                NullLogger<D47.Core.Actions.MacroStore>.Instance),
            Personas,
            new D47.Core.Checklists.ChecklistService(
                new D47.Core.Checklists.ChecklistStore(
                    Path.Combine(_install.Paths.Data, "checklist.json"),
                    NullLogger<D47.Core.Checklists.ChecklistStore>.Instance),
                new D47.Core.Checklists.ChecklistProposalStore(
                    Path.Combine(_install.Paths.Data, "checklist-proposals.json"),
                    NullLogger<D47.Core.Checklists.ChecklistProposalStore>.Instance),
                () => null),
            lore: Lore);

        Registry = CapabilityRegistry.Build(descriptors.Select(Recording));
        built = Registry;
        Settings.Bind(Registry);

        Router = new KeywordRouter(Registry, () => []);

        Settings.Applied += applied =>
            _applies.Add(new TracedSettingApply(applied.Key, applied.Status, CurrentSettingsCaller));
    }

    /// <summary>Which caller is driving the settings service right now.</summary>
    internal SettingsCaller CurrentSettingsCaller { get; set; } = SettingsCaller.Panel;

    public SettingsService Settings { get; }

    public SecretStore Secrets { get; }

    public CapabilityRegistry Registry { get; }

    public KeywordRouter Router { get; }

    public GameStateStore GameState { get; }

    public LoreBook Lore { get; }

    public D47.Core.Persona.PersonaHost Personas { get; } = new();

    /// <summary>Whether key injection is on for this run.</summary>
    public bool ActionsEnabled { get; set; }

    /// <summary>Every input step d47 tried to send.</summary>
    public IReadOnlyList<D47.Core.Input.InputStep> Pressed => _input.Steps;

    /// <summary>Enough of a bindings file that an action resolves rather than refusing for want of one.</summary>
    private static D47.Core.Input.EliteBinds Bound { get; } = new()
    {
        PresetName = "Scenario",
        SourceFile = "Scenario.binds",
        Bindings =
        [
            // Elite's own bind names, not d47's action ids: ActionReachability resolves through the action
            // definition to the name the bindings file uses, so an id here would resolve to nothing and every
            // action would refuse for want of a binding.
            new D47.Core.Input.EliteBinding("FocusCommsPanel", "Primary", "Keyboard", "Key_2"),
            new D47.Core.Input.EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L"),
            new D47.Core.Input.EliteBinding("ToggleCargoScoop", "Primary", "Keyboard", "Key_Home"),
        ],
    };

    /// <summary>In the main ship, in flight.</summary>
    private static D47.Core.Journal.GameStatus Flying { get; } = new()
    {
        Flags = D47.Core.Journal.StatusFlags.InMainShip,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    /// <summary>
    /// The <c>Status.json</c> read this run stands on, which both the action surface and the live
    /// game-state block ask for.
    /// </summary>
    public GameStatus LiveStatus { get; set; } = Flying;

    public AppPaths Paths => _install.Paths;

    /// <summary>Every tool whose handler actually ran, with the arguments it received.</summary>
    public IReadOnlyList<(string Tool, string ArgumentsJson, bool Succeeded)> Ran => _ran;

    /// <summary>Every settings row a turn touched, refusals included.</summary>
    public IReadOnlyList<TracedSettingApply> Applies => _applies;

    /// <summary>Drops what has been recorded so far.</summary>
    public void ForgetApplies() => _applies.Clear();

    /// <summary>
    /// Capabilities whose tools act outside d47 — the two channels that reach something other than the
    /// Commander reading the panel.
    /// </summary>
    private static readonly HashSet<string> OutwardCapabilities = new(StringComparer.Ordinal)
    {
        "flight-controls", "ship-systems", "panels", "srv", "macros", "comms",
    };

    /// <summary>
    /// Every tool that can act outside d47, taken from the registry rather than listed, so a capability
    /// that gains one is covered the day it registers.
    /// </summary>
    public IReadOnlySet<string> OutwardToolNames =>
        Registry.All
            .Where(capability => OutwardCapabilities.Contains(capability.Descriptor.Id))
            .SelectMany(capability => capability.Descriptor.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The keys no model may write, read off the registered descriptors rather than listed here.
    /// </summary>
    public IReadOnlySet<string> ProtectedSettingKeys =>
        Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Where(row => row.Protected || row.Kind == SettingKind.Secret)
            .Select(row => row.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Makes <paramref name="tool"/> answer with <paramref name="content"/> instead of running.</summary>
    public void PoisonToolResult(string tool, string content) => _hostileResults[tool] = content;

    /// <summary>Applies raw journal lines, as the tick loop would.</summary>
    public void ApplyJournal(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines)
        {
            // A line that does not parse would leave the scenario running against an empty world and passing
            // for the wrong reason, which is the failure mode this whole phase is about.
            if (!JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) || parsed is null)
            {
                throw new InvalidOperationException($"A scenario's journal line did not parse: {line}");
            }

            GameState.Apply(parsed);
        }
    }

    /// <summary>What the model is told about the world, exactly as the app assembles it.</summary>
    public string? LiveGameState() =>
        Situation.Describe(GameState.Active, LiveStatus, DateTimeOffset.UtcNow);

    /// <summary>Every file under <c>data/</c>, as relative path to content hash.</summary>
    public IReadOnlyDictionary<string, string> SnapshotData()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(Paths.Data))
        {
            return snapshot;
        }

        foreach (var file in Directory.EnumerateFiles(Paths.Data, "*", SearchOption.AllDirectories))
        {
            snapshot[Path.GetRelativePath(Paths.Data, file)] =
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        }

        return snapshot;
    }

    public void Dispose() => _install.Dispose();

    /// <summary>The same descriptor with every handler wrapped.</summary>
    private CapabilityDescriptor Recording(CapabilityDescriptor descriptor) =>
        descriptor with
        {
            Tools = [.. descriptor.Tools.Select(tool => tool with
            {
                Handler = async (arguments, cancellationToken) =>
                {
                    if (_hostileResults.TryGetValue(tool.Name, out var poisoned))
                    {
                        _ran.Add((tool.Name, Describe(arguments), true));
                        return ToolResult.Ok(poisoned);
                    }

                    var result = await tool.Handler(arguments, cancellationToken).ConfigureAwait(false);
                    _ran.Add((tool.Name, Describe(arguments), !result.IsError));
                    return result;
                },
            })],
        };

    private static string Describe(ToolArguments arguments) =>
        System.Text.Json.JsonSerializer.Serialize(
            arguments.Values.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString() ?? string.Empty));

    /// <summary>Keeps the secret store usable without a per-user, per-machine DPAPI blob.</summary>
    private sealed class PlainProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;

        public bool TryUnprotect(
            byte[] ciphertext,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? plaintext)
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

/// <summary>A throwaway install root, so every scenario gets its own <c>data/</c>.</summary>
public sealed class TempInstall : IDisposable
{
    public TempInstall()
    {
        Root = Path.Combine(Path.GetTempPath(), "d47-scenarios", Guid.NewGuid().ToString("N"));
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
        // A leftover temp folder is not worth failing a run over.
        }
    }
}
