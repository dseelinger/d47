using D47.Core.Actions;
using D47.Core.Configuration;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The three compound spoken commands of Phase 52: <em>take us out</em>,
/// <em>separate and engage</em> and <em>separate and supercruise</em>.
/// <para>
/// <b>Protected, and that is the whole design.</b> These reach the ship, so they are reachable
/// from the model-free keyword router and the panel and are never advertised to the model — a
/// spoken command that has to wait for a model round trip is a command given at the wrong moment,
/// which is why the router exists at all. Being protected also means they cost no tool-surface
/// bytes: <c>ToolProfiles</c> leaves a protected tool out of the advertisement entirely, which
/// matters with the surface as close to its ceiling as it is.
/// </para>
/// <para>
/// One tool with three values rather than three tools, because the docs gate asks for a fenced
/// schema per tool and these three differ in one word. The switches are the opposite way round —
/// one row each — for the reason the checklist gives.
/// </para>
/// </summary>
public static class ShipCommands
{
    public const string TakeUsOut = "take_us_out";
    public const string SeparateAndEngage = "separate_and_engage";
    public const string SeparateAndSupercruise = "separate_and_supercruise";

    public const string LaunchKey = "actions.takeUsOut";
    public const string SeparateEngageKey = "actions.separateAndEngage";
    public const string SeparateSupercruiseKey = "actions.separateAndSupercruise";

    /// <summary>
    /// The tool. Protected, so the model never sees it and never pays for it.
    /// </summary>
    public static ToolDefinition Tool(ActionSurface actions, ShipCommandSurface commands) => new()
    {
        Name = "ship_command",
        Description =
            "Compound ship commands: leave the pad, or break a mass lock and engage. "
            + "Spoken only — the Commander reaches these by voice or from the panel.",
        Protected = true,
        Parameters =
        [
            new ToolParameter
            {
                Name = "command",
                Type = ToolParameterType.String,
                Description = "Which command to run.",
                Required = true,
                AllowedValues = [TakeUsOut, SeparateAndEngage, SeparateAndSupercruise],
            },
        ],
        Commands =
        [
            new ToolCommandPhrase(
                "take us out",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = TakeUsOut }),

            new ToolCommandPhrase(
                "separate and engage",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = SeparateAndEngage }),

            new ToolCommandPhrase(
                "separate and supercruise",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = SeparateAndSupercruise }),
        ],
        Handler = (arguments, cancellationToken) => Run(arguments, actions, commands, cancellationToken),
    };

    private static async Task<ToolResult> Run(
        ToolArguments arguments,
        ActionSurface actions,
        ShipCommandSurface commands,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetString("command", out var command) || string.IsNullOrWhiteSpace(command))
        {
            return ToolResult.Error("No command was named.");
        }

        // The general gate first, then the command's own. Two refusals rather than one, because
        // "you have not let me press keys at all" and "you have not let me do this one" are
        // different things to fix and a Commander would go to a different row for each.
        if (!actions.Enabled())
        {
            return ToolResult.Error(
                "Pressing keys in Elite is switched off. The Commander can turn it on in settings; "
                + "it is not something I can turn on for them.");
        }

        if (!commands.Enabled(command))
        {
            return ToolResult.Error(
                $"{Name(command)} is switched off. It has its own row in settings, separate from the "
                + "others, and only the Commander can turn it on.");
        }

        if (command == TakeUsOut)
        {
            var launched = await Launch
                .RunAsync(actions, commands.AwaitLeftPanel, commands.AwaitUndocked, cancellationToken)
                .ConfigureAwait(false);

            return launched.Ok ? ToolResult.Ok(launched.Message) : ToolResult.Error(launched.Message);
        }

        // The only difference between the two separations, and it is deliberate that they differ
        // in nothing else (Phase 52, item 4).
        var finisher = command == SeparateAndSupercruise ? "supercruise" : "hyperspace";

        var outcome = await Separation
            .RunAsync(actions, finisher, commands.NextStatus, SeparationLimits.Default, cancellationToken)
            .ConfigureAwait(false);

        return outcome.Ok ? ToolResult.Ok(outcome.Message) : ToolResult.Error(outcome.Message);
    }

    private static string Name(string command) => command switch
    {
        TakeUsOut => "Taking us out",
        SeparateAndEngage => "Separate and engage",
        SeparateAndSupercruise => "Separate and supercruise",
        _ => command,
    };

    /// <summary>
    /// One row per command. Protected, like every row that reaches the keyboard: the model reads
    /// untrusted text, so a switch it could flip to grant itself the ship is privilege escalation
    /// rather than a convenience.
    /// </summary>
    public static IReadOnlyList<SettingRow> Rows() =>
    [
        new()
        {
            Key = LaunchKey,
            Advanced = true,
            Label = "Let D47 take us out of a station",
            Help = "Lets \"take us out\" walk the left panel to the launch button. Elite has no launch "
                   + "binding, so this is a menu walk rather than a key press, and it depends on the "
                   + "panel being where D47 expects. Needs key presses to be allowed as well.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "on",
            DocsAnchor = "take-us-out",
            Protected = true,
            Commands =
            [
                new SettingCommandPhrase("you may take us out of stations", "true"),
                new SettingCommandPhrase("do not take us out of stations", "false"),
            ],
            Binding = new SettingBinding
            {
                Read = s => s.Actions.TakeUsOut ? "true" : "false",
                Write = (s, v) => s with { Actions = s.Actions with { TakeUsOut = v is "true" } },
            },
        },

        new()
        {
            Key = SeparateEngageKey,
            Advanced = true,
            Label = "Let D47 separate and engage",
            Help = "Lets \"separate and engage\" go to full throttle and boost until the mass lock "
                   + "breaks, then jump. Bounded at four boosts and twenty seconds, and it says so "
                   + "rather than stopping quietly. Needs key presses to be allowed as well.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "on",
            DocsAnchor = "separate",
            Protected = true,
            Commands =
            [
                new SettingCommandPhrase("you may separate and engage", "true"),
                new SettingCommandPhrase("do not separate and engage", "false"),
            ],
            Binding = new SettingBinding
            {
                Read = s => s.Actions.SeparateAndEngage ? "true" : "false",
                Write = (s, v) => s with { Actions = s.Actions with { SeparateAndEngage = v is "true" } },
            },
        },

        new()
        {
            Key = SeparateSupercruiseKey,
            Advanced = true,
            Label = "Let D47 separate and supercruise",
            Help = "The same, ending in supercruise instead of a jump. Its own row because a "
                   + "Commander may want one and not the other: a jump needs a destination locked "
                   + "in the nav panel and refuses without one, where supercruise needs nothing.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "on",
            DocsAnchor = "separate",
            Protected = true,
            Commands =
            [
                new SettingCommandPhrase("you may separate and supercruise", "true"),
                new SettingCommandPhrase("do not separate and supercruise", "false"),
            ],
            Binding = new SettingBinding
            {
                Read = s => s.Actions.SeparateAndSupercruise ? "true" : "false",
                Write = (s, v) => s with { Actions = s.Actions with { SeparateAndSupercruise = v is "true" } },
            },
        },
    ];

    /// <summary>Which switch a command reads, for the host that wires <see cref="ShipCommandSurface"/>.</summary>
    public static bool IsEnabled(D47Settings settings, string command) => command switch
    {
        TakeUsOut => settings.Actions.TakeUsOut,
        SeparateAndEngage => settings.Actions.SeparateAndEngage,
        SeparateAndSupercruise => settings.Actions.SeparateAndSupercruise,
        _ => false,
    };
}
