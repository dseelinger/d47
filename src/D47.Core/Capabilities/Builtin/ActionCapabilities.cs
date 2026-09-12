using D47.Core.Actions;
using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything the four action capabilities need from outside Core: the Commander's bindings, what the
/// game is currently reporting, whether they have allowed d47 to press keys at all, and something to
/// press with.
/// </summary>
public sealed record ActionSurface
{
    public required Func<EliteBinds> Binds { get; init; }

    /// <summary>The live status.</summary>
    public required Func<GameStatus> Status { get; init; }

    public required IGameInput Input { get; init; }

    /// <summary>Whether keyboard actions are switched on.</summary>
    public required Func<bool> Enabled { get; init; }

    /// <summary>Whether there is a system to jump to.</summary>
    public Func<bool> SystemTargeted { get; init; } = () => false;

    /// <summary>Says one line, now, and does not wait for it to be spoken (#158).</summary>
    public Action<string> Acknowledge { get; init; } = _ => { };

    /// <summary>The terse reply a performed action is acknowledged with (#66).</summary>
    public Acknowledgements Acknowledgements { get; init; } = new();

    public ControlContext Context => ControlContexts.Of(Status());

    /// <summary>A surface that reaches no game: no bindings, no mode, nothing switched on.</summary>
    public static ActionSurface Inert => new()
    {
        Binds = () => EliteBinds.None,
        Status = () => GameStatus.Unknown,
        Input = new RecordingGameInput { IsGameRunning = false },
        Enabled = () => false,
    };
}

/// <summary>
/// What the compound ship commands need on top of <see cref="ActionSurface"/> (Phase 52): somewhere to
/// wait for the game to change its mind.
/// </summary>
public sealed record ShipCommandSurface
{
    /// <summary>Whether each command is switched on.</summary>
    public required Func<string, bool> Enabled { get; init; }

    /// <summary>
    /// Awaits <c>GuiFocus</c> reaching or leaving the left panel — the one <see
    /// cref="Actions.Launch.Panel"/> names, which is <c>ExternalPanel</c>.
    /// </summary>
    public required Func<bool, CancellationToken, Task<bool?>> AwaitLeftPanel { get; init; }

    /// <summary>Awaits the <c>Docked</c> flag clearing, which is what says the ship actually left.</summary>
    public required Func<CancellationToken, Task<bool?>> AwaitUndocked { get; init; }

    /// <summary>Awaits the next status sample, which is what the boost loop watches.</summary>
    public required Func<CancellationToken, Task<GameStatus>> NextStatus { get; init; }

    /// <summary>
    /// Opens a watch on the journal before the contacts walk sends its first key, so the confirmation
    /// cannot count a docking request that was already in (#150).
    /// </summary>
    public Func<IDockingWatch> WatchDockingRequest { get; init; } = () => new FixedDockingWatch(null);

    /// <summary>
    /// Whether a docking computer is fitted, or null where the loadout cannot answer — which "take us
    /// in" treats as fitted rather than refusing on no evidence (#150).
    /// </summary>
    public Func<bool?> DockingComputerFitted { get; init; } = () => null;

    /// <summary>The wall clock, injected because no Core type reads one.</summary>
    public required Func<DateTimeOffset> Now { get; init; }

    /// <summary>A surface that reaches no game and permits nothing.</summary>
    public static ShipCommandSurface Inert => new()
    {
        Enabled = _ => false,
        AwaitLeftPanel = (_, _) => Task.FromResult<bool?>(null),
        AwaitUndocked = _ => Task.FromResult<bool?>(null),
        NextStatus = _ => Task.FromResult(GameStatus.Unknown),

        // Never read: this surface refuses at Enabled before anything reaches a loop that would want a clock.
        Now = () => DateTimeOffset.UnixEpoch,
    };
}

/// <summary>Voice control of the ship, its systems, its panels and the SRV (Phase 10, items 6 to 9).</summary>
public static class ActionCapabilities
{
    public const string KeyboardActionsKey = "actions.keyboard";

    public static IReadOnlyList<CapabilityDescriptor> All(
        ActionSurface surface,
        ShipCommandSurface? shipCommands = null,

        // What "set a course and take us out" plots through, and what it plots to (#325).
        NavigationSurface? navigation = null,
        Conversation.LastFoundSystem? lastFound = null) =>
    [
        Create(
            "flight-controls",
            "Flight and navigation",
            GameActions.Flight,
            "control_flight",
            "Operate the landing gear, lights, cargo scoop, hardpoints and the frame shift drive.",
            ["put the gear down", "retract hardpoints", "engage supercruise", "lights off"],
            surface,
            order: 51,
            primary: true,
            extra: ShipCommands.Tool(
                surface,
                shipCommands ?? ShipCommandSurface.Inert,
                navigation ?? NavigationSurface.Inert,
                lastFound ?? new Conversation.LastFoundSystem())),

        Create(
            "ship-systems",
            "Ship systems",
            GameActions.Systems,
            "control_systems",
            "Move power between engines, weapons and systems, and reach silent running and heat sinks.",
            ["pips to engines", "balance the power", "drop a heat sink", "silent running"],
            surface,
            order: 51),

        Create(
            "panels",
            "Panels and interface",
            GameActions.Interface,
            "control_interface",
            "Open the cockpit panels, move around them, and change fire group.",
            ["open the left panel", "next fire group", "galaxy map"],
            surface,
            order: 52),

        Create(
            "srv",
            "SRV",
            GameActions.SrvGroup,
            "control_srv",
            "Operate the SRV's turret, handbrake, drive assist and ship recall.",
            ["turret on", "handbrake off", "recall my ship"],
            surface,
            order: 53),
    ];

    /// <summary>The live half: what is actually reachable this turn, as a line for prompt position 7.</summary>
    public static string? Describe(ActionSurface surface)
    {
        if (!surface.Enabled())
        {
            return null;
        }

        var context = surface.Context;

        if (context == ControlContext.None)
        {
            return null;
        }

        var offered = ActionReachability.ResolveAll(surface.Binds(), context)
            .Where(reach => reach.IsOffered)
            .Select(reach => reach.Action.Id)
            .ToArray();

        return offered.Length == 0
            ? $"The Commander is {ControlContexts.Describe(context)}. No game actions are reachable right now."
            : $"The Commander is {ControlContexts.Describe(context)}. Game actions that work right now: "
              + string.Join(", ", offered)
              + ". Any other action will be refused with a reason, so do not claim to have done one.";
    }

    private static CapabilityDescriptor Create(
        string id,
        string name,
        string group,
        string toolName,
        string summary,
        IReadOnlyList<string> examples,
        ActionSurface surface,
        int order,

        // Whether this is the card the whole group's rows live on, rather than a number that happens to be 50
        // (#83).
        bool primary = false,
        ToolDefinition? extra = null)
    {
        // Computed once at registration and never again.
        var actions = GameActions.All.Where(action => action.Group == group).ToArray();

        return new CapabilityDescriptor
        {
            Id = id,
            Group = "Acting on the game",
            Name = name,
            Summary = summary,
            Examples = examples,

            // No keywords.
            Display = new CapabilityDisplay { PanelTitle = name, Order = order, ShowOnPanel = primary },

            // One row, on the first card only.
            Settings = primary ? [KeyboardActionsRow(), .. ShipCommands.Rows()] : [],

            Tools =
            [
                new ToolDefinition
                {
                    Name = toolName,
                    Description =
                        summary
                        + " Only the actions listed as reachable in the current game state will work; "
                        + "anything else comes back with the reason it did not.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "action",
                            Type = ToolParameterType.String,
                            Description = "Which action to perform.",
                            Required = true,
                            AllowedValues = [.. actions.Select(action => action.Id)],
                        },
                        new ToolParameter
                        {
                            Name = "state",
                            Type = ToolParameterType.String,
                            Description =
                                "What to leave it in. Elite binds a single toggle, so asking for "
                                + "\"on\" or \"off\" checks the game's own report first and does "
                                + "nothing if it is already there. Defaults to toggling.",
                            AllowedValues = ["on", "off", "toggle"],
                        },
                    ],
                    Commands = [.. Phrases(actions)],
                    Handler = (arguments, cancellationToken) => Perform(arguments, surface, cancellationToken),
                },

                .. extra is null ? Array.Empty<ToolDefinition>() : [extra],
            ],
        };
    }

    /// <summary>
    /// The model-free phrases for one group, projected from the catalogue so there is no second list of
    /// what a Commander can say.
    /// </summary>
    private static IEnumerable<ToolCommandPhrase> Phrases(IEnumerable<GameAction> actions) =>
        from action in actions
        from phrase in action.Phrases
        select new ToolCommandPhrase(
            phrase.Phrase,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["action"] = action.Id,
                ["state"] = phrase.State switch
                {
                    DesiredState.On => "on",
                    DesiredState.Off => "off",
                    _ => "toggle",
                },
            });

    private static async Task<ToolResult> Perform(
        ToolArguments arguments,
        ActionSurface surface,
        CancellationToken cancellationToken)
    {
        if (!surface.Enabled())
        {
            return ToolResult.Error(
                "Pressing keys in Elite is switched off. The Commander can turn it on in settings; "
                + "it is not something I can turn on for them.");
        }

        if (!arguments.TryGetString("action", out var id) || string.IsNullOrWhiteSpace(id))
        {
            return ToolResult.Error("No action was named.");
        }

        if (GameActions.Find(id) is not { } action)
        {
            return ToolResult.Error($"There is no action called '{id}'.");
        }

        // The jump, asked for by name, takes the same fallback to the combined FSD key as the separation's
        // finisher does, under the same target guard (#344).
        var reach = action.Id == FsdJumpReach.Hyperspace
            ? FsdJumpReach.Resolve(surface.Binds(), surface.Context, surface.SystemTargeted())
            : ActionReachability.Resolve(action, surface.Binds(), surface.Context);

        // The refusal carries the reason.
        if (!reach.IsOffered)
        {
            return ToolResult.Error(reach.Reason);
        }

        var wanted = arguments.TryGetString("state", out var state)
            ? state.ToLowerInvariant() switch
            {
                "on" => DesiredState.On,
                "off" => DesiredState.Off,
                _ => DesiredState.Toggle,
            }
            : DesiredState.Toggle;

        // The check that makes "gear down" mean down.
        if (action.AlreadyIn(wanted, surface.Status()) == true)
        {
            return ToolResult.Ok($"{Capitalise(action.Label)} is already {(wanted == DesiredState.On ? "on" : "off")}.");
        }

        var result = await surface.Input
            .SendAsync(InputSequence.Tap(reach.Binding!), cancellationToken)
            .ConfigureAwait(false);

        // Two audiences: the key that fired is what makes an "it did not do it" report diagnosable, and it
        // is not what the Commander asked to hear back.
        return result.Sent
            ? ToolResult.Ok(
                $"Pressed {reach.Binding!.Gesture()} for {action.Label}.",
                surface.Acknowledgements.For(action, wanted))
            : ToolResult.Error(result.Reason);
    }

    /// <summary>
    /// Protected: the model reads untrusted text,
    /// so a switch it could flip to grant itself the keyboard is privilege escalation rather than a
    /// convenience.
    /// </summary>
    private static SettingRow KeyboardActionsRow() => new()
    {
        Key = KeyboardActionsKey,
        Advanced = true,
        Label = "Let D47 press keys in Elite",
        Help = "Lets spoken commands operate the ship by sending your own key bindings to the game. "
               + "Keys are only ever sent while Elite is the window in front. Off until you turn it on.",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "off",
        DocsAnchor = "you-have-to-turn-it-on",
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("let yourself press keys in elite", "true"),
            new SettingCommandPhrase("you may press keys in elite", "true"),
            new SettingCommandPhrase("stop pressing keys in elite", "false"),
            new SettingCommandPhrase("do not press keys in elite", "false"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Actions.Keyboard ? "true" : "false",
            Write = (s, v) => s with { Actions = s.Actions with { Keyboard = v is "true" } },
        },
    };

    private static string Capitalise(string label) =>
        label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];
}
