using D47.Core.Actions;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The compound spoken commands: take us out, separate and engage, separate and supercruise (Phase 52),
/// and set a course and take us out (#325).
/// </summary>
public static class ShipCommands
{
    public const string TakeUsOut = "take_us_out";
    public const string SeparateAndEngage = "separate_and_engage";
    public const string SeparateAndSupercruise = "separate_and_supercruise";
    public const string SetCourseAndTakeUsOut = "set_course_and_take_us_out";

    public const string LaunchKey = "actions.takeUsOut";
    public const string SeparateEngageKey = "actions.separateAndEngage";
    public const string SeparateSupercruiseKey = "actions.separateAndSupercruise";

    /// <summary>The tool.</summary>
    /// <param name="navigation">
    /// What <see cref="SetCourseAndTakeUsOut"/> plots through — the same surface <c>plot_course</c>
    /// itself uses (#325).
    /// </param>
    /// <param name="lastFound">
    /// What <see cref="SetCourseAndTakeUsOut"/> plots to: the system a nearest-first commodity search
    /// last found, read at the moment the command runs.
    /// </param>
    public static ToolDefinition Tool(
        ActionSurface actions, ShipCommandSurface commands, NavigationSurface navigation, LastFoundSystem lastFound) => new()
    {
        Name = "ship_command",
        Description =
            "Compound ship commands: leave the pad, break a mass lock and engage, or set a course "
            + "to what a nearest-first search just found and leave the pad. Spoken only — the "
            + "Commander reaches these by voice or from the panel.",
        Protected = true,
        Parameters =
        [
            new ToolParameter
            {
                Name = "command",
                Type = ToolParameterType.String,
                Description = "Which command to run.",
                Required = true,
                AllowedValues = [TakeUsOut, SeparateAndEngage, SeparateAndSupercruise, SetCourseAndTakeUsOut],
            },
        ],
        Commands =
        [
            new ToolCommandPhrase(
                "take us out",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = TakeUsOut }),

            .. SeparatePhrases(),

            new ToolCommandPhrase(
                "separate and supercruise",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = SeparateAndSupercruise }),

            new ToolCommandPhrase(
                "set a course and take us out",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = SetCourseAndTakeUsOut }),
        ],
        Handler = (arguments, cancellationToken) => Run(arguments, actions, commands, navigation, lastFound, cancellationToken),
    };

    /// <summary>
    /// Every way of saying separate and engage: <c>[get clear | get us clear | separate | boost] and
    /// [jump | engage | hyperspace]</c>, the Commander's own pattern.
    /// </summary>
    private static IEnumerable<ToolCommandPhrase> SeparatePhrases()
    {
        string[] openings = ["get clear", "get us clear", "separate", "boost"];
        string[] finishers = ["jump", "engage", "hyperspace"];

        return
            from opening in openings
            from finisher in finishers
            select new ToolCommandPhrase(
                $"{opening} and {finisher}",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = SeparateAndEngage });
    }

    private static async Task<ToolResult> Run(
        ToolArguments arguments,
        ActionSurface actions,
        ShipCommandSurface commands,
        NavigationSurface navigation,
        LastFoundSystem lastFound,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetString("command", out var command) || string.IsNullOrWhiteSpace(command))
        {
            return ToolResult.Error("No command was named.");
        }

        // The general gate first, then the command's own.
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

        // One launch, whether or not a course is plotted into it first: the compound command is the plain one
        // with a plot in front of it and its sentence on the front of the answer.
        if (command is TakeUsOut or SetCourseAndTakeUsOut)
        {
            var prefix = string.Empty;

            if (command == SetCourseAndTakeUsOut)
            {
                if (lastFound.System is not { Length: > 0 } system)
                {
                    return ToolResult.Error(
                        "Nothing has been found yet to set a course to. Run a nearest-first search first.");
                }

                var plotted = await NavigationCapability
                    .Plot(
                        new ToolArguments(
                            new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system }),
                        navigation,
                        cancellationToken)
                    .ConfigureAwait(false);

                // Best-effort by design (#325): plot_course itself only ever errors when the clipboard cannot
                // be written to, everything else it reports as a sentence rather than a failure.
                if (plotted.IsError)
                {
                    return plotted;
                }

                prefix = $"{plotted.Content} ";
            }

            var launched = await Launch
                .RunAsync(actions, commands.AwaitLeftPanel, commands.AwaitUndocked, cancellationToken)
                .ConfigureAwait(false);

            var said = prefix + launched.Message;

            return launched.Ok ? ToolResult.Ok(said) : ToolResult.Error(said);
        }

        // The only difference between the two separations, and it is deliberate that they differ in nothing
        // else (Phase 52, item 4).
        var finisher = command == SeparateAndSupercruise ? "supercruise" : "hyperspace";

        var outcome = await Separation
            .RunAsync(
                actions, finisher, commands.NextStatus, commands.Now, SeparationLimits.Default, cancellationToken)
            .ConfigureAwait(false);

        return outcome.Ok ? ToolResult.Ok(outcome.Message) : ToolResult.Error(outcome.Message);
    }

    private static string Name(string command) => command switch
    {
        TakeUsOut => "Taking us out",
        SeparateAndEngage => "Separate and engage",
        SeparateAndSupercruise => "Separate and supercruise",
        SetCourseAndTakeUsOut => "Taking us out",
        _ => command,
    };

    /// <summary>One row per command.</summary>
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
                   + "rather than stopping silently. Needs key presses to be allowed as well.",
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

        // Its own launch key is Taking us out's own switch (#325): the command wraps that exact action, and a
        // Commander who has told d47 not to walk the panel has told it once.
        SetCourseAndTakeUsOut => settings.Actions.TakeUsOut,
        _ => false,
    };
}
