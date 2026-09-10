using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Putting text where the Commander can paste it.</summary>
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
    /// Opens a watch on NavRoute.json before the first key is sent, so that the check afterwards can
    /// insist on a route written after the attempt began.
    /// </summary>
    public required Func<IPlotWatch> WatchRoute { get; init; }

    /// <summary>
    /// Waits for the galaxy map to be open (<c>true</c>) or closed again (<c>false</c>), as
    /// Status.json's <c>GuiFocus</c> reports it, and answers whether that happened in time.
    /// </summary>
    public required Func<bool, CancellationToken, Task<bool?>> AwaitGalaxyMap { get; init; }

    /// <summary>Where the plotting attempt says how far it got, on every exit (#365).</summary>
    public ILogger Log { get; init; } = NullLogger.Instance;

    /// <summary>A surface that reaches no clipboard and no game.</summary>
    public static NavigationSurface Inert => new()
    {
        Clipboard = new RecordingClipboard { Works = false },
        Actions = ActionSurface.Inert,
        AutoPlotEnabled = () => false,
        WatchRoute = () => new FixedPlotWatch(null),
        AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(null),
    };
}

/// <summary>One plotting attempt's view of the route file, opened before the first key goes.</summary>
public interface IPlotWatch
{
    /// <summary>Whether a route ending at the named system was written after this watch was opened.</summary>
    Task<bool?> ConfirmAsync(string system, CancellationToken cancellationToken);

    /// <summary>
    /// The system the route in the file already ended at when this watch was opened, or null where
    /// there was no route, or no readable file.
    /// </summary>
    string? EndsAt => null;

    /// <summary>
    /// The route file as this watch sees it right now — its write stamp and the system its last hop
    /// names — for the input trace to record beside the keys (#365).
    /// </summary>
    string Describe() => string.Empty;
}

/// <summary>A watch that answers what it was told to.</summary>
/// <param name="answer">What <see cref="ConfirmAsync"/> reports.</param>
/// <param name="endsAt">
/// Where the route already went, for the caller that asks before it drives anything.
/// </param>
public sealed class FixedPlotWatch(bool? answer, string? endsAt = null) : IPlotWatch
{
    public string? EndsAt => endsAt;

    public Task<bool?> ConfirmAsync(string system, CancellationToken cancellationToken) => Task.FromResult(answer);
}

/// <summary>A clipboard that records instead of writing.</summary>
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

/// <summary>Getting a destination out of d47 and into the game (Phase 10, items 10 and 11).</summary>
public static class NavigationCapability
{
    public const string Id = "navigation";

    public const string AutoPlotKey = "actions.autoPlot";

    /// <summary>Between the paste and stepping into the results.</summary>
    private static readonly TimeSpan SearchSettle = TimeSpan.FromMilliseconds(1500);

    /// <summary>How many times the macro is driven before the Commander is told it did not take (#404).</summary>
    private const int Attempts = 2;

    /// <summary>Between one interface key and the next.</summary>
    private static readonly TimeSpan BetweenKeys = TimeSpan.FromMilliseconds(150);

    /// <summary>After the search, for the camera to fly to the system.</summary>
    private static readonly TimeSpan CameraSettle = TimeSpan.FromSeconds(4);

    /// <summary>How long select is held on the star.</summary>
    private static readonly TimeSpan PlotHold = TimeSpan.FromMilliseconds(1200);

    /// <summary>A brush of the camera sideways, between the camera arriving and the held select.</summary>
    private static readonly TimeSpan Nudge = TimeSpan.FromMilliseconds(30);

    /// <summary>The two camera keys, resolved here and advertised nowhere.</summary>
    private static readonly GameAction NudgeRight = new()
    {
        Id = "galaxy_map_nudge_right",
        Label = "the galaxy map camera, right",
        Group = GameActions.Interface,
        Variants = [new ActionVariant("CamTranslateRight", ControlContext.AnyShip | ControlContext.Srv)],
    };

    private static readonly GameAction NudgeLeft = new()
    {
        Id = "galaxy_map_nudge_left",
        Label = "the galaxy map camera, left",
        Group = GameActions.Interface,
        Variants = [new ActionVariant("CamTranslateLeft", ControlContext.AnyShip | ControlContext.Srv)],
    };

    public static CapabilityDescriptor Create(NavigationSurface surface) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "Navigation",
        Summary = "Put a system name on your clipboard, and try to plot a course to it.",
        Examples = ["plot a course to Shinrarta Dezhra", "copy that system name", "set course for Colonia"],
        Display = new CapabilityDisplay { PanelTitle = "Navigation", Order = 59 },
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

    /// <summary>
    /// Internal rather than private: <see cref="ShipCommands"/>' "set a course and take us out" calls
    /// straight into this rather than duplicating it, since the plotting half of that compound command
    /// is exactly this tool (#325).
    /// </summary>
    internal static async Task<ToolResult> Plot(
        ToolArguments arguments,
        NavigationSurface surface,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetString("system", out var system) || string.IsNullOrWhiteSpace(system))
        {
            return ToolResult.Error("No system was named.");
        }

        system = system.Trim();

        // Unconditionally first.
        if (!await surface.Clipboard.SetTextAsync(system, cancellationToken).ConfigureAwait(false))
        {
            return ToolResult.Error("The clipboard could not be written to, so I have not tried to plot either.");
        }

        var copied = $"{system} is on your clipboard.";

        // Asked for a course that is already set, d47 says so and sends nothing (the Commander's
        // ruling, 2026-09-08).
        if (surface.WatchRoute().EndsAt is { } already
            && string.Equals(already, system, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(
                $"The course to {system} is already plotted, so I have left it alone. {copied}");
        }

        if (!surface.AutoPlotEnabled() || !surface.Actions.Enabled())
        {
            return ToolResult.Ok(
                $"{copied} Paste it into the galaxy map's search box to plot it. "
                + "Automatic plotting is switched off.");
        }

        var (keys, reason) = MapKeys.Resolve(surface.Actions);

        if (keys is null)
        {
            return ToolResult.Ok(
                $"{copied} I could not drive the galaxy map myself — "
                + $"{reason} Paste it into the map's search box to plot it.");
        }

        // The system's name, said before the map opens (#158, widened by the Commander to cover this
        // macro).
        surface.Actions.Acknowledge($"Plotting the course to {system}.");

        // The trace, if this run asked for one, opened before the first key (<a
        // href=".com/dseelinger/d47/issues/365">#365</a>).
        var trace = surface.Actions.Input.Trace(TraceCaller);

        // How far the attempt got, said on every exit including a cancelled turn (#365).
        var reached = "the attempt was acknowledged";

        // How it ended, held rather than written where it is decided, because the verdict is the trace's last
        // line and the exit step is declared in the <c>finally</c> below (#365).
        (string Verdict, string Reason)? ending = null;

        try
        {
            trace?.Declare("system", system);

            // Opened before the first key, so a route that was already in the file cannot pass as the result
            // of this attempt.
            var watch = surface.WatchRoute();

            trace?.Declare("route at open", watch.Describe());

            bool? confirmed = null;

            // Two goes at it (#404).
            for (var attempt = 1; attempt <= Attempts; attempt++)
            {
                if (attempt > 1)
                {
                    trace?.Declare("sequence", "close before trying again");
                    surface.Log.LogInformation(
                        "Galaxy-map plot for {System}: no route on attempt {Attempt}, driving it again",
                        system,
                        attempt - 1);

                    await surface.Actions.Input
                        .SendAsync(keys.Close(), trace, cancellationToken)
                        .ConfigureAwait(false);

                    await surface.AwaitGalaxyMap(false, cancellationToken).ConfigureAwait(false);
                }

                // The map is opened on its own and the rest waits for Status.json to say it is showing,
                // because the remaining keys are interface keys: typed into the cockpit instead of the map
                // they are a W and a space bar sent to a flying ship.
                if (surface.Actions.Status().GuiFocus != GuiFocus.GalaxyMap)
                {
                    trace?.Declare("sequence", "open");

                    var opened = await surface.Actions.Input
                        .SendAsync(keys.Open(), trace, cancellationToken)
                        .ConfigureAwait(false);

                    if (!opened.Sent)
                    {
                        reached = "the map key was refused";
                        ending = ("refused", opened.Reason);

                        // The reason, not just that there was one.
                        surface.Log.LogInformation(
                            "Galaxy-map plot for {System}: the map key was refused — {Reason}",
                            system,
                            opened.Reason);

                        return ToolResult.Ok($"{copied} I could not drive the galaxy map: {opened.Reason}");
                    }

                    reached = "the map key was sent";

                    if (await surface.AwaitGalaxyMap(true, cancellationToken).ConfigureAwait(false) is false)
                    {
                        reached = "the map did not open";
                        ending = ("no map", "the map did not open within the wait");

                        return ToolResult.Ok(
                            $"{copied} I pressed the galaxy map key and the map did not open, so I have not "
                            + "typed anything. Paste it into the map's search box to plot it.");
                    }
                }
                else
                {
                    trace?.Declare("map", "already open");
                }

                reached = "the map is open";
                trace?.Declare("sequence", attempt == 1 ? "search" : "search again");

                var sent = await surface.Actions.Input
                    .SendAsync(keys.Search(), trace, cancellationToken)
                    .ConfigureAwait(false);

                if (!sent.Sent)
                {
                    reached = "the search sequence was refused";
                    ending = ("refused", sent.Reason);
                    return ToolResult.Ok($"{copied} I could not drive the galaxy map: {sent.Reason}");
                }

                reached = "the search sequence was sent";
                trace?.Declare("route after the hold", watch.Describe());

                // Plotted or not, the map is toggled shut: the Commander asked for a course, not for a map
                // left open over the cockpit.
                confirmed = await watch.ConfirmAsync(system, cancellationToken).ConfigureAwait(false);

                reached = "the route was checked";
                trace?.Declare("route at the verdict", watch.Describe());

                // Recorded the moment it is known, and again if a second attempt answers differently.
                ending = (
                    confirmed switch { true => "plotted", false => "no route", null => "cannot tell" },
                    $"the route watch answered {confirmed?.ToString() ?? "nothing"} for {system}");

                // Only a checked "no route" is worth another go: see <see cref="Attempts"/>.
                if (confirmed is not false)
                {
                    break;
                }
            }

            trace?.Declare("sequence", "close");

            var closing = await surface.Actions.Input
                .SendAsync(keys.Close(), trace, cancellationToken)
                .ConfigureAwait(false);

            var closed = closing.Sent
                && await surface.AwaitGalaxyMap(false, cancellationToken).ConfigureAwait(false) is not false;

            var stillOpen = closed ? string.Empty : " The galaxy map is still open.";

            reached = closed ? "the map was closed" : "the map was left open";

            // The three answers are genuinely different and the middle one is the reason this is verified at
            // all: believing a course is set when it is not is the failure that strands somebody.
            return confirmed switch
            {
                true => ToolResult.Relay($"Course plotted to {system}.{stillOpen}"),
                false => ToolResult.Relay(
                    $"I tried to plot {system} and no route appeared, so assume it did not work. {copied} "
                    + $"I cannot tell why.{stillOpen}"),
                null => ToolResult.Relay(
                    $"I tried to plot {system} but cannot tell whether it worked. {copied} Check the map.{stillOpen}"),
            };
        }
        finally
        {
            // In a finally rather than at each return, because the exits that most need saying are the ones
            // that do not return at all: a turn the Commander cancels mid-macro, and a fault.
            surface.Log.LogInformation(
                "Galaxy-map plot for {System} left off at {Step}",
                system,
                reached);

            trace?.Declare("exit", reached);

            // After the exit step, so the verdict is the last line however the attempt ended.
            if (ending is { } end)
            {
                trace?.Verdict(end.Verdict, end.Reason);
            }

            trace?.Dispose();
        }
    }

    /// <summary>What the plot's trace folder is named after (#365).</summary>
    private const string TraceCaller = "galaxy-map-plot";

    /// <summary>
    /// The five bindings the macro presses, resolved against the Commander's own file and the mode they
    /// are in, and the three keystroke runs built from them.
    /// </summary>
    private sealed record MapKeys(
        EliteBinding Map,
        EliteBinding Up,
        EliteBinding Select,
        EliteBinding Down,
        EliteBinding NudgeRight,
        EliteBinding NudgeLeft)
    {
        private const uint Control = 0xA2;
        private const uint V = 0x56;
        private const uint Return = 0x0D;

        public static (MapKeys? Keys, string Reason) Resolve(ActionSurface actions)
        {
            var binds = actions.Binds();
            var context = actions.Context;
            var pressed = new List<EliteBinding>(3);

            foreach (var id in new[] { "galaxy_map", "ui_up", "ui_select", "ui_down" })
            {
                if (GameActions.Find(id) is not { } action)
                {
                    return (null, $"there is no {id} action.");
                }

                var reach = ActionReachability.Resolve(action, binds, context);

                if (!reach.IsOffered)
                {
                    return (null, reach.Reason);
                }

                pressed.Add(reach.Binding!);
            }

            var right = ActionReachability.Resolve(NavigationCapability.NudgeRight, binds, context);

            if (!right.IsOffered)
            {
                return (null, right.Reason);
            }

            var left = ActionReachability.Resolve(NavigationCapability.NudgeLeft, binds, context);

            if (!left.IsOffered)
            {
                return (null, left.Reason);
            }

            return (
                new MapKeys(pressed[0], pressed[1], pressed[2], pressed[3], right.Binding!, left.Binding!),
                string.Empty);
        }

        /// <summary>Step 1: open the map.</summary>
        public IReadOnlyList<InputStep> Open() => InputSequence.Tap(Map);

        /// <summary>
        /// Steps 2 to 8: walk to the search box, paste, return to search, let the camera arrive, brush
        /// the camera to arm the selector, and hold select to plot.
        /// </summary>
        public IReadOnlyList<InputStep> Search() =>
        [
            .. InputSequence.Tap(Up),
            InputStep.Wait(BetweenKeys),
            .. InputSequence.Tap(Select),
            InputStep.Wait(BetweenKeys).Watched("after-the-walk"),

            new InputStep(InputStepKind.KeyDown, Control),
            new InputStep(InputStepKind.KeyDown, V),
            InputStep.Wait(TimeSpan.FromMilliseconds(40)),
            new InputStep(InputStepKind.KeyUp, V),
            new InputStep(InputStepKind.KeyUp, Control),

            InputStep.Wait(SearchSettle).Watched("after-the-paste"),
            new InputStep(InputStepKind.KeyDown, Return),
            InputStep.Wait(TimeSpan.FromMilliseconds(40)),
            new InputStep(InputStepKind.KeyUp, Return),

            InputStep.Wait(BetweenKeys),
            .. InputSequence.Tap(Down),
            InputStep.Wait(BetweenKeys),
            .. InputSequence.Tap(Select),

            InputStep.Wait(CameraSettle).Watched("after-the-camera"),
            .. InputSequence.Hold(NudgeRight, Nudge),
            .. InputSequence.Hold(NudgeLeft, Nudge),
            InputStep.Wait(BetweenKeys).Watched("after-the-nudges"),
            .. InputSequence.WatchedAfter(InputSequence.Hold(Select, PlotHold), "after-the-hold"),
        ];

        /// <summary>Step 9: the map key again, which toggles it shut.</summary>
        public IReadOnlyList<InputStep> Close() => InputSequence.Tap(Map);
    }

    /// <summary>
    /// Protected, like every row that reaches the keyboard, and off by default because it is the one
    /// row here that presses keys rather than filling the clipboard.
    /// </summary>
    private static SettingRow AutoPlotRow() => new()
    {
        Key = AutoPlotKey,
        Advanced = true,
        Label = "Try to plot courses in the galaxy map",
        Help = "After copying a system name, opens the galaxy map, searches for it, plots to it and "
               + "closes the map again, using your own galaxy map, UI up, UI select and sideways "
               + "camera keys. "
               + "Best-effort: D47 checks afterwards whether a route actually appeared and tells you "
               + "if it did not. Needs key presses to be allowed too.",
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
