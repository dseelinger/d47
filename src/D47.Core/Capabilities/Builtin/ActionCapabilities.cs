using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything the four action capabilities need from outside Core: the Commander's bindings,
/// what the game is currently reporting, whether they have allowed d47 to press keys at all,
/// and something to press with.
/// </summary>
public sealed record ActionSurface
{
    public required Func<EliteBinds> Binds { get; init; }

    /// <summary>
    /// The live status. Read per call rather than captured, because the mode changes several
    /// times a minute and a captured value would be the one it had at startup.
    /// </summary>
    public required Func<GameStatus> Status { get; init; }

    public required IGameInput Input { get; init; }

    /// <summary>
    /// Whether keyboard actions are switched on. Read through a function for the same reason,
    /// since it changes without a restart (Phase 4).
    /// </summary>
    public required Func<bool> Enabled { get; init; }

    public ControlContext Context => ControlContexts.Of(Status());

    /// <summary>
    /// A surface that reaches no game: no bindings, no mode, nothing switched on. What a test
    /// that is not about actions should use, and what d47 behaves like before Elite has ever
    /// been started — the four capabilities register either way, because which capabilities
    /// exist is not allowed to depend on whether the game is running.
    /// </summary>
    public static ActionSurface Inert => new()
    {
        Binds = () => EliteBinds.None,
        Status = () => GameStatus.Unknown,
        Input = new RecordingGameInput { IsGameRunning = false },
        Enabled = () => false,
    };
}

/// <summary>
/// What the compound ship commands need on top of <see cref="ActionSurface"/> (Phase 52):
/// somewhere to wait for the game to change its mind.
/// <para>
/// Three waits, all injected, for the reason every wait in Core is injected: no component here
/// owns a thread or reads the clock, which is what keeps the replay harness able to drive the
/// whole path at any speed. Production supplies them from the tick loop; a test supplies a
/// scripted stream and the loop runs in microseconds.
/// </para>
/// </summary>
public sealed record ShipCommandSurface
{
    /// <summary>Whether each command is switched on. One switch per command, keyed by its id.</summary>
    public required Func<string, bool> Enabled { get; init; }

    /// <summary>
    /// Awaits <c>GuiFocus</c> reaching or leaving the <b>left</b> panel — the one
    /// <see cref="Actions.Launch.Panel"/> names, which is <c>ExternalPanel</c>.
    /// <para>
    /// <b>Named for the panel rather than for the flag since #106</b>, because the flag's name is
    /// the trap: Frontier call the right-hand panel <em>internal</em> and the left-hand one
    /// <em>external</em>, so a property called <c>AwaitInternalPanel</c> made a wait on the wrong
    /// one of the two read as obviously correct for as long as it shipped.
    /// </para>
    /// </summary>
    public required Func<bool, CancellationToken, Task<bool?>> AwaitLeftPanel { get; init; }

    /// <summary>Awaits the <c>Docked</c> flag clearing, which is what says the ship actually left.</summary>
    public required Func<CancellationToken, Task<bool?>> AwaitUndocked { get; init; }

    /// <summary>Awaits the next status sample, which is what the boost loop watches.</summary>
    public required Func<CancellationToken, Task<GameStatus>> NextStatus { get; init; }

    /// <summary>
    /// A surface that reaches no game and permits nothing. What a test that is not about these
    /// commands should use, and what the tool refuses through before the Commander has switched
    /// anything on — the tool registers either way, because which tools exist is not allowed to
    /// depend on which are permitted.
    /// </summary>
    public static ShipCommandSurface Inert => new()
    {
        Enabled = _ => false,
        AwaitLeftPanel = (_, _) => Task.FromResult<bool?>(null),
        AwaitUndocked = _ => Task.FromResult<bool?>(null),
        NextStatus = _ => Task.FromResult(GameStatus.Unknown),
    };
}

/// <summary>
/// Voice control of the ship, its systems, its panels and the SRV (Phase 10, items 6
/// to 9). One capability per checklist item, because each gets its own documentation page and
/// its own place in spoken help, and because a tool with forty values in its vocabulary is one
/// the model picks badly from.
/// <para>
/// <b>The schema is stable and the availability is not.</b> Each tool declares its whole group
/// as a closed vocabulary, computed once at registration, so the bytes are identical across
/// turns and prompt caching survives (architecture.md D5). What actually works right now is
/// live game state — prompt position 7, below the cache breakpoint, where it changes every
/// turn for free. Putting the current set in the schema instead would rewrite position 1 on
/// every mode change and invalidate the entire prefix, which is the exact failure the closed
/// set exists to prevent.
/// </para>
/// <para>
/// <b>Firing weapons is not here.</b> The catalogue carries <c>primary_fire</c> and
/// <c>secondary_fire</c> because the discovery scanner is fired through them and has no
/// binding of its own, but neither is on any tool and neither has a spoken phrase. The model
/// consumes untrusted text (architecture.md §7), and a hostile in-game message that can make
/// d47 open fire is a different category of problem from one that can make it turn the lights
/// on.
/// </para>
/// </summary>
public static class ActionCapabilities
{
    public const string KeyboardActionsKey = "actions.keyboard";

    public static IReadOnlyList<CapabilityDescriptor> All(
        ActionSurface surface,
        ShipCommandSurface? shipCommands = null) =>
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
            extra: ShipCommands.Tool(surface, shipCommands ?? ShipCommandSurface.Inert)),

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

    /// <summary>
    /// The live half: what is actually reachable this turn, as a line for prompt position 7.
    /// Null when there is nothing to say, so a Commander who is not in the game is not billed
    /// for an empty list on every turn.
    /// </summary>
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

        // Whether this is the card the whole group's rows live on, rather than a number that
        // happens to be 50 (#83). This used to be `order == 50` in two places, which quietly
        // made the display order load-bearing for two things that are not about ordering at
        // all: whether the card is drawn, and whether it carries the keyboard-actions row and
        // the ship commands. Changing where the card sits in the nav would have switched both
        // off, silently, and neither has anything to do with where it sits.
        bool primary = false,
        ToolDefinition? extra = null)
    {
        // Computed once at registration and never again. This is the closed vocabulary that
        // has to serialise byte-identically across turns.
        var actions = GameActions.All.Where(action => action.Group == group).ToArray();

        return new CapabilityDescriptor
        {
            Id = id,
            Group = "Acting on the game",
            Name = name,
            Summary = summary,
            Examples = examples,

            // No keywords. A keyword routes a capability's first argument-free tool, and every
            // tool here requires an action — so a keyword would match and then reach nothing.
            // The declared phrases below are the model-free route instead, and they name the
            // action rather than merely the subject.
            Display = new CapabilityDisplay { PanelTitle = name, Order = order, ShowOnPanel = primary },

            // One row, on the first card only. Pressing keys in the game is one decision, not
            // four, and four copies of it is four things to switch off.
            //
            // The compound commands are the exception, and deliberately: each is its own row
            // (Phase 52, item 5), because a Commander who trusts d47 to supercruise may
            // well not trust it to boost, and because "take us out" walks a menu by guesswork
            // where everything else presses one key the Commander bound themselves. Every one of
            // them is gated by the row above as well, so a Commander who has not allowed key
            // injection at all has not allowed it here either.
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
    /// The model-free phrases for one group, projected from the catalogue so there is no second
    /// list of what a Commander can say.
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

        var reach = ActionReachability.Resolve(action, surface.Binds(), surface.Context);

        // The refusal carries the reason. An action that fails as silence is the failure this
        // whole path exists to prevent (Phase 10, item 1).
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

        // The check that makes "gear down" mean down. Elite binds one toggle, so pressing it
        // when the gear is already down retracts it — the opposite of what was asked, at the
        // moment it matters most.
        if (action.AlreadyIn(wanted, surface.Status()) == true)
        {
            return ToolResult.Ok($"{Capitalise(action.Label)} is already {(wanted == DesiredState.On ? "on" : "off")}.");
        }

        var result = await surface.Input
            .SendAsync(InputSequence.Tap(reach.Binding!), cancellationToken)
            .ConfigureAwait(false);

        return result.Sent
            ? ToolResult.Ok($"Pressed {reach.Binding!.Gesture()} for {action.Label}.")
            : ToolResult.Error(result.Reason);
    }

    /// <summary>
    /// Protected, and the reason is the one architecture.md §7 gives: the model reads
    /// untrusted text, so a switch it could flip to grant itself the keyboard is privilege
    /// escalation rather than a convenience. The panel, a hotkey and the model-free keyword
    /// router all reach this row; the LLM does not.
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
