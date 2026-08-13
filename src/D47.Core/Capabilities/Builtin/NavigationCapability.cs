using D47.Core.Configuration;
using D47.Core.Input;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Putting text where the Commander can paste it. Implemented in the app.</summary>
public interface IClipboard
{
    Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>What the navigation capability needs from outside Core.</summary>
public sealed record NavigationSurface
{
    public required IClipboard Clipboard { get; init; }

    public required ActionSurface Actions { get; init; }

    /// <summary>Whether the Commander has allowed the galaxy-map plotting attempt.</summary>
    public required Func<bool> AutoPlotEnabled { get; init; }

    /// <summary>
    /// Watches NavRoute.json for a route ending at the named system, and answers whether one
    /// appeared. Null means d47 could not tell either way.
    /// <para>
    /// In the app because it waits, and Core reads no clock. That it is answerable at all is
    /// the thing that makes auto-plot honest: Elite writes the whole route to a file the moment
    /// one is plotted, so "did that work" has a real answer rather than an assumption.
    /// </para>
    /// </summary>
    public required Func<string, CancellationToken, Task<bool?>> ConfirmPlot { get; init; }

    /// <summary>A surface that reaches no clipboard and no game. For tests that are not about it.</summary>
    public static NavigationSurface Inert => new()
    {
        Clipboard = new RecordingClipboard { Works = false },
        Actions = ActionSurface.Inert,
        AutoPlotEnabled = () => false,
        ConfirmPlot = (_, _) => Task.FromResult<bool?>(null),
    };
}

/// <summary>
/// A clipboard that records instead of writing. In Core rather than in the tests for the same
/// reason <c>RecordingGameInput</c> is: the replay harness needs it too.
/// </summary>
public sealed class RecordingClipboard : IClipboard
{
    private readonly List<string> _written = [];

    public bool Works { get; set; } = true;

    public IReadOnlyList<string> Written => _written;

    public string? Last => _written.Count == 0 ? null : _written[^1];

    public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Works)
        {
            _written.Add(text);
        }

        return Task.FromResult(Works);
    }
}

/// <summary>
/// Getting a destination out of d47 and into the game (list.md Phase 10, items 10 and 11).
/// <para>
/// <b>The clipboard is the primary path and the plotting attempt is a convenience on top of
/// it.</b> That ordering is the checklist's, and it is right: pasting a system name into the
/// galaxy map's search box always works, whereas driving the map with keystrokes depends on
/// where the map's focus happens to be, on its layout, and on the game's language. So the name
/// goes on the clipboard first, unconditionally, and the plot is attempted afterwards. If the
/// attempt fails the Commander has lost nothing and still has the name.
/// </para>
/// <para>
/// <b>A failed plot says so.</b> Elite writes the whole route to NavRoute.json the moment one
/// is plotted, so the attempt is verified rather than assumed — which is what stops this
/// leaving a Commander flying towards a course they do not have.
/// </para>
/// </summary>
public static class NavigationCapability
{
    public const string Id = "navigation";

    public const string AutoPlotKey = "actions.autoPlot";

    /// <summary>
    /// Between the paste and the return. Elite's map takes a moment to accept a pasted name and
    /// populate its result list, and pressing return into an empty list plots nothing.
    /// </summary>
    private static readonly TimeSpan SearchSettle = TimeSpan.FromMilliseconds(600);

    public static CapabilityDescriptor Create(NavigationSurface surface) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "Navigation",
        Summary = "Put a system name on your clipboard, and try to plot a course to it.",
        Examples = ["plot a course to Shinrarta Dezhra", "copy that system name", "set course for Colonia"],
        Display = new CapabilityDisplay { PanelTitle = "Navigation", Order = 55 },
        Settings = [AutoPlotRow()],
        Tools =
        [
            new ToolDefinition
            {
                Name = "copy_to_clipboard",
                Description =
                    "Put text on the Commander's clipboard so they can paste it into the game or a "
                    + "browser. Use for system names, routes and values they asked for.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "text",
                        Type = ToolParameterType.String,
                        Description = "What to put on the clipboard.",
                        Required = true,
                    },
                ],
                Handler = (arguments, cancellationToken) => Copy(arguments, surface, cancellationToken),
            },

            new ToolDefinition
            {
                Name = "plot_course",
                Description =
                    "Put a system name on the clipboard and, if the Commander has allowed it, try to "
                    + "plot a course to it in the galaxy map. The plotting attempt is best-effort and "
                    + "is verified afterwards; the clipboard always works.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "system",
                        Type = ToolParameterType.String,
                        Description = "The star system to plot to, spelled as the game spells it.",
                        Required = true,
                    },
                ],
                Handler = (arguments, cancellationToken) => Plot(arguments, surface, cancellationToken),
            },
        ],
    };

    private static async Task<ToolResult> Copy(
        ToolArguments arguments,
        NavigationSurface surface,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetString("text", out var text) || string.IsNullOrWhiteSpace(text))
        {
            return ToolResult.Error("There was nothing to copy.");
        }

        return await surface.Clipboard.SetTextAsync(text, cancellationToken).ConfigureAwait(false)
            ? ToolResult.Ok($"Copied to the clipboard: {text}")
            : ToolResult.Error("The clipboard could not be written to.");
    }

    private static async Task<ToolResult> Plot(
        ToolArguments arguments,
        NavigationSurface surface,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetString("system", out var system) || string.IsNullOrWhiteSpace(system))
        {
            return ToolResult.Error("No system was named.");
        }

        system = system.Trim();

        // Unconditionally first. Whatever happens to the plotting attempt, the Commander ends
        // up holding the name.
        if (!await surface.Clipboard.SetTextAsync(system, cancellationToken).ConfigureAwait(false))
        {
            return ToolResult.Error("The clipboard could not be written to, so I have not tried to plot either.");
        }

        var copied = $"{system} is on your clipboard.";

        if (!surface.AutoPlotEnabled() || !surface.Actions.Enabled())
        {
            return ToolResult.Ok(
                $"{copied} Paste it into the galaxy map's search box to plot it. "
                + "Automatic plotting is switched off.");
        }

        if (BuildAttempt(surface.Actions) is not { } steps)
        {
            return ToolResult.Ok(
                $"{copied} I could not open the galaxy map myself — "
                + $"{Reason(surface.Actions)} Paste it into the map's search box to plot it.");
        }

        var sent = await surface.Actions.Input.SendAsync(steps, cancellationToken).ConfigureAwait(false);

        if (!sent.Sent)
        {
            return ToolResult.Ok($"{copied} I could not drive the galaxy map: {sent.Reason}");
        }

        var confirmed = await surface.ConfirmPlot(system, cancellationToken).ConfigureAwait(false);

        // The three answers are genuinely different and the middle one is the reason this is
        // verified at all: believing a course is set when it is not is the failure that strands
        // somebody.
        return confirmed switch
        {
            true => ToolResult.Ok($"Course plotted to {system}."),
            false => ToolResult.Ok(
                $"I tried to plot {system} and no route appeared, so assume it did not work. {copied} "
                + "The search box may not have had focus."),
            null => ToolResult.Ok(
                $"I tried to plot {system} but cannot tell whether it worked. {copied} Check the map."),
        };
    }

    /// <summary>
    /// The keystrokes that drive the map. Deliberately short: open the map, focus the search
    /// field, paste, return. Every extra step is another thing that depends on a layout d47
    /// cannot see.
    /// <para>
    /// The paste is a plain Ctrl+V rather than one of the Commander's bindings, because Elite
    /// does not bind paste — it is the operating system's, and the search box is an ordinary
    /// text field.
    /// </para>
    /// </summary>
    private static IReadOnlyList<InputStep>? BuildAttempt(ActionSurface actions)
    {
        if (GameActions.Find("galaxy_map") is not { } map)
        {
            return null;
        }

        var reach = ActionReachability.Resolve(map, actions.Binds(), actions.Context);

        if (!reach.IsOffered)
        {
            return null;
        }

        const uint control = 0xA2;
        const uint v = 0x56;
        const uint enter = 0x0D;

        return
        [
            .. InputSequence.Tap(reach.Binding!),
            InputStep.Wait(TimeSpan.FromSeconds(1)),

            new InputStep(InputStepKind.KeyDown, control),
            new InputStep(InputStepKind.KeyDown, v),
            InputStep.Wait(TimeSpan.FromMilliseconds(40)),
            new InputStep(InputStepKind.KeyUp, v),
            new InputStep(InputStepKind.KeyUp, control),

            InputStep.Wait(SearchSettle),
            new InputStep(InputStepKind.KeyDown, enter),
            InputStep.Wait(TimeSpan.FromMilliseconds(40)),
            new InputStep(InputStepKind.KeyUp, enter),
        ];
    }

    private static string Reason(ActionSurface actions) =>
        GameActions.Find("galaxy_map") is { } map
            ? ActionReachability.Resolve(map, actions.Binds(), actions.Context).Reason
            : "there is no galaxy map action.";

    /// <summary>
    /// Protected, like every row that reaches the keyboard, and off by default because it is
    /// the one row here that presses keys rather than filling the clipboard.
    /// </summary>
    private static SettingRow AutoPlotRow() => new()
    {
        Key = AutoPlotKey,
        Label = "Try to plot courses in the galaxy map",
        Help = "After copying a system name, opens the galaxy map and pastes it in. Best-effort: it "
               + "depends on the map's focus and layout, so D47 checks afterwards whether a route "
               + "actually appeared and tells you if it did not. Needs key presses to be allowed too.",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "off",
        DocsAnchor = "letting-it-drive-the-map",
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("try to plot courses yourself", "true"),
            new SettingCommandPhrase("stop plotting courses yourself", "false"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Actions.AutoPlot ? "true" : "false",
            Write = (s, v) => s with { Actions = s.Actions with { AutoPlot = v is "true" } },
        },
    };
}
