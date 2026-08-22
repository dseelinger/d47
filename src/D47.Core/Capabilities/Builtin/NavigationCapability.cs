using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Journal;

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
    /// Opens a watch on NavRoute.json <em>before</em> the first key is sent, so that the check
    /// afterwards can insist on a route written after the attempt began.
    /// <para>
    /// In the app because it waits, and Core reads no clock. That it is answerable at all is
    /// the thing that makes auto-plot honest: Elite writes the whole route to a file the moment
    /// one is plotted, so "did that work" has a real answer rather than an assumption.
    /// </para>
    /// <para>
    /// <b>Why a watch rather than a check.</b> The first check read the file after the keys and
    /// asked whether its route ended at the system — and on 2026-08-21 the Commander plotted a
    /// route by hand, then asked for the same one by voice, and heard "course plotted" for a
    /// macro that had plotted nothing. A route that was already there is not evidence that the
    /// keys did anything. The watch remembers when the file was last written and only a newer
    /// write counts.
    /// </para>
    /// </summary>
    public required Func<IPlotWatch> WatchRoute { get; init; }

    /// <summary>
    /// Waits for the galaxy map to be open (<c>true</c>) or closed again (<c>false</c>), as
    /// Status.json's <c>GuiFocus</c> reports it, and answers whether that happened in time. Null
    /// means Status.json was never readable, so d47 could not tell.
    /// <para>
    /// In the app for the same reason as <see cref="ConfirmPlot"/>: it waits. What it buys is a
    /// macro that keys off the game rather than off a guess — the search box is typed into once
    /// the map is actually showing, and "the map is still open" is reported rather than assumed
    /// away.
    /// </para>
    /// </summary>
    public required Func<bool, CancellationToken, Task<bool?>> AwaitGalaxyMap { get; init; }

    /// <summary>A surface that reaches no clipboard and no game. For tests that are not about it.</summary>
    public static NavigationSurface Inert => new()
    {
        Clipboard = new RecordingClipboard { Works = false },
        Actions = ActionSurface.Inert,
        AutoPlotEnabled = () => false,
        WatchRoute = () => new FixedPlotWatch(null),
        AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(null),
    };
}

/// <summary>
/// One plotting attempt's view of the route file, opened before the first key goes. See
/// <see cref="NavigationSurface.WatchRoute"/>.
/// </summary>
public interface IPlotWatch
{
    /// <summary>
    /// Whether a route ending at the named system was written <em>after</em> this watch was
    /// opened. False when the file was readable and no such route appeared in time; null when
    /// it never became readable at all.
    /// </summary>
    Task<bool?> ConfirmAsync(string system, CancellationToken cancellationToken);
}

/// <summary>A watch that answers what it was told to. In Core for the replay harness, like <see cref="RecordingClipboard"/>.</summary>
public sealed class FixedPlotWatch(bool? answer) : IPlotWatch
{
    public Task<bool?> ConfirmAsync(string system, CancellationToken cancellationToken) => Task.FromResult(answer);
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
/// <para>
/// <b>The drive is the Commander's own macro</b>, given 2026-08-21 after the original three-key
/// attempt — open, paste, return — turned out to plot nothing: it assumed the search box had
/// focus when the map opened, and it does not. The sequence below walks to the box explicitly
/// (up, select), pastes, commits the search with return, gives the camera time to arrive,
/// brushes the camera once to arm the selector, holds select long enough to plot, and toggles
/// the map shut. Every wait in it is the Commander's figure, and the two things d47 <em>can</em>
/// see — Status.json saying the map is showing, NavRoute.json saying a route exists — are
/// checked where they fall rather than assumed.
/// </para>
/// <para>
/// <b>Return, not "down".</b> The first cut of the macro stepped into the result list with the
/// UI down key and selected the first entry, and the Commander watched it type an S into the
/// search box instead: the text field keeps focus after the paste, so an interface key sent to
/// it is a character. Return is what the field itself does with a search, and the map then
/// flies to the first match on its own. The same reason the map is closed with its own toggle
/// rather than with "back".
/// </para>
/// </summary>
public static class NavigationCapability
{
    public const string Id = "navigation";

    public const string AutoPlotKey = "actions.autoPlot";

    /// <summary>
    /// Between the paste and the return. Elite's map takes a moment to accept a pasted name and
    /// populate its results, and a return into an empty list searches for nothing.
    /// </summary>
    private static readonly TimeSpan SearchSettle = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Between one interface key and the next. Elite reads input per frame and its panels animate
    /// between states; two taps inside one animation are one tap.
    /// </summary>
    private static readonly TimeSpan BetweenKeys = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// After the search, for the camera to fly to the system. The Commander's figure — long
    /// enough for the far side of the bubble, short enough not to be noticed for the next system
    /// over — chosen over scaling it by distance.
    /// </summary>
    private static readonly TimeSpan CameraSettle = TimeSpan.FromSeconds(3);

    /// <summary>How long select is held on the star. A tap opens the system; a hold plots to it.</summary>
    private static readonly TimeSpan PlotHold = TimeSpan.FromMilliseconds(1200);

    /// <summary>
    /// A brush of the camera sideways, between the camera arriving and the held select.
    /// <para>
    /// <b>This is what arms the selector.</b> After the search flies the camera to the system, the
    /// selector over the star is a plain circle and pressing select does nothing — the Commander
    /// found this by hand on 2026-08-21 after a cut of this macro without the brush plotted
    /// nothing. The smallest camera movement draws the arrows around the selector, and with the
    /// arrows showing and the selector still over the star, the held select plots. As brief as a
    /// press can be: the Commander's instruction was "less than a tenth of a second", and the
    /// floor is <see cref="InputSequence.TapHold"/> — under one frame Elite does not see the key
    /// at all, and anything longer moves the selector off the star.
    /// </para>
    /// </summary>
    private static readonly TimeSpan Nudge = InputSequence.TapHold;

    /// <summary>
    /// The camera key, resolved here and advertised nowhere. It is not in <see cref="GameActions.All"/>
    /// because that list is the <c>control_interface</c> tool's closed vocabulary and its
    /// documentation page, and a sideways camera brush is not something a Commander asks for by
    /// voice — it exists for this macro alone. Right first, left as the fallback, since either
    /// arms the selector and a Commander may have bound only one.
    /// </summary>
    private static readonly IReadOnlyList<GameAction> NudgeKeys =
    [
        new()
        {
            Id = "galaxy_map_nudge",
            Label = "the galaxy map camera",
            Group = GameActions.Interface,
            Variants = [new ActionVariant("CamTranslateRight", ControlContext.AnyShip | ControlContext.Srv)],
        },
        new()
        {
            Id = "galaxy_map_nudge",
            Label = "the galaxy map camera",
            Group = GameActions.Interface,
            Variants = [new ActionVariant("CamTranslateLeft", ControlContext.AnyShip | ControlContext.Srv)],
        },
    ];

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

        var (keys, reason) = MapKeys.Resolve(surface.Actions);

        if (keys is null)
        {
            return ToolResult.Ok(
                $"{copied} I could not drive the galaxy map myself — "
                + $"{reason} Paste it into the map's search box to plot it.");
        }

        // Opened before the first key, so a route that was already in the file cannot pass as
        // the result of this attempt.
        var watch = surface.WatchRoute();

        // The map is opened on its own and the rest waits for Status.json to say it is showing,
        // because the remaining keys are interface keys: typed into the cockpit instead of the
        // map they are a W and a space bar sent to a flying ship. A map that is already open is
        // not toggled shut first.
        if (surface.Actions.Status().GuiFocus != GuiFocus.GalaxyMap)
        {
            var opened = await surface.Actions.Input.SendAsync(keys.Open(), cancellationToken).ConfigureAwait(false);

            if (!opened.Sent)
            {
                return ToolResult.Ok($"{copied} I could not drive the galaxy map: {opened.Reason}");
            }

            if (await surface.AwaitGalaxyMap(true, cancellationToken).ConfigureAwait(false) is false)
            {
                return ToolResult.Ok(
                    $"{copied} I pressed the galaxy map key and the map did not open, so I have not "
                    + "typed anything. Paste it into the map's search box to plot it.");
            }
        }

        var sent = await surface.Actions.Input.SendAsync(keys.Search(), cancellationToken).ConfigureAwait(false);

        if (!sent.Sent)
        {
            return ToolResult.Ok($"{copied} I could not drive the galaxy map: {sent.Reason}");
        }

        // Plotted or not, the map is toggled shut: the Commander asked for a course, not for a
        // map left open over the cockpit. The check comes first so the closing cannot land on a
        // route still being calculated.
        var confirmed = await watch.ConfirmAsync(system, cancellationToken).ConfigureAwait(false);
        var closing = await surface.Actions.Input.SendAsync(keys.Close(), cancellationToken).ConfigureAwait(false);
        var closed = closing.Sent && await surface.AwaitGalaxyMap(false, cancellationToken).ConfigureAwait(false) is not false;
        var stillOpen = closed ? string.Empty : " The galaxy map is still open.";

        // The three answers are genuinely different and the middle one is the reason this is
        // verified at all: believing a course is set when it is not is the failure that strands
        // somebody.
        return confirmed switch
        {
            true => ToolResult.Ok($"Course plotted to {system}.{stillOpen}"),
            false => ToolResult.Ok(
                $"I tried to plot {system} and no route appeared, so assume it did not work. {copied} "
                + $"The search may have matched a different system.{stillOpen}"),
            null => ToolResult.Ok(
                $"I tried to plot {system} but cannot tell whether it worked. {copied} Check the map.{stillOpen}"),
        };
    }

    /// <summary>
    /// The four bindings the macro presses, resolved against the Commander's own file and the
    /// mode they are in, and the three keystroke runs built from them.
    /// <para>
    /// All four or none: a macro that opens the map and then has no key for "select" leaves the
    /// map open over the cockpit, which is worse than the clipboard alone. So the first binding
    /// that cannot be pressed stops the whole attempt before a key is sent, and its reason is the
    /// one the Commander hears.
    /// </para>
    /// <para>
    /// The paste and the return are plain keys rather than the Commander's bindings, because
    /// Elite binds neither — they are the operating system's, and the search box is an ordinary
    /// text field.
    /// </para>
    /// </summary>
    private sealed record MapKeys(EliteBinding Map, EliteBinding Up, EliteBinding Select, EliteBinding Nudge)
    {
        private const uint Control = 0xA2;
        private const uint V = 0x56;
        private const uint Return = 0x0D;

        public static (MapKeys? Keys, string Reason) Resolve(ActionSurface actions)
        {
            var binds = actions.Binds();
            var context = actions.Context;
            var pressed = new List<EliteBinding>(3);

            foreach (var id in new[] { "galaxy_map", "ui_up", "ui_select" })
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

            // Either side will do, and the reason reported is the right-hand one's, which names
            // the key a Commander who has bound neither would most naturally add.
            var nudges = NudgeKeys.Select(key => ActionReachability.Resolve(key, binds, context)).ToArray();

            if (nudges.FirstOrDefault(reach => reach.IsOffered) is not { } nudge)
            {
                return (null, nudges[0].Reason);
            }

            return (new MapKeys(pressed[0], pressed[1], pressed[2], nudge.Binding!), string.Empty);
        }

        /// <summary>Step 1: open the map. Sent on its own so Status.json can confirm it showed.</summary>
        public IReadOnlyList<InputStep> Open() => InputSequence.Tap(Map);

        /// <summary>
        /// Steps 2 to 8: walk to the search box, paste, return to search, let the camera arrive,
        /// brush the camera to arm the selector, and hold select to plot.
        /// </summary>
        public IReadOnlyList<InputStep> Search() =>
        [
            .. InputSequence.Tap(Up),
            InputStep.Wait(BetweenKeys),
            .. InputSequence.Tap(Select),
            InputStep.Wait(BetweenKeys),

            new InputStep(InputStepKind.KeyDown, Control),
            new InputStep(InputStepKind.KeyDown, V),
            InputStep.Wait(TimeSpan.FromMilliseconds(40)),
            new InputStep(InputStepKind.KeyUp, V),
            new InputStep(InputStepKind.KeyUp, Control),

            InputStep.Wait(SearchSettle),
            new InputStep(InputStepKind.KeyDown, Return),
            InputStep.Wait(TimeSpan.FromMilliseconds(40)),
            new InputStep(InputStepKind.KeyUp, Return),

            InputStep.Wait(CameraSettle),
            .. InputSequence.Hold(Nudge, NavigationCapability.Nudge),
            InputStep.Wait(BetweenKeys),
            .. InputSequence.Hold(Select, PlotHold),
        ];

        /// <summary>Step 9: the map key again, which toggles it shut.</summary>
        public IReadOnlyList<InputStep> Close() => InputSequence.Tap(Map);
    }

    /// <summary>
    /// Protected, like every row that reaches the keyboard, and off by default because it is
    /// the one row here that presses keys rather than filling the clipboard.
    /// </summary>
    private static SettingRow AutoPlotRow() => new()
    {
        Key = AutoPlotKey,
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
