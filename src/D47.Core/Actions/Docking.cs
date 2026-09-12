using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Actions;

/// <summary>How far a docking request got.</summary>
public enum DockingEnding
{
    /// <summary>Nothing was pressed: the wrong situation, nothing targeted, or a missing binding.</summary>
    Refused,

    /// <summary>The keys went in and the journal recorded a request.</summary>
    Requested,

    /// <summary>The keys went in and no request reached the journal.</summary>
    NoRequest,

    /// <summary>The keys went in and d47 cannot tell either way.</summary>
    Unknown,
}

/// <param name="Ending">Which of the four happened.</param>
/// <param name="Message">The sentence to say back.</param>
public readonly record struct DockingOutcome(DockingEnding Ending, string Message)
{
    public bool Ok => Ending == DockingEnding.Requested;
}

/// <summary>
/// One press of the walk: an id from <see cref="GameActions"/>, how long the key is held, and how long
/// to wait afterwards.
/// </summary>
/// <param name="Action">An id from <see cref="GameActions"/>.</param>
/// <param name="HoldMs">Milliseconds the key is held down.</param>
/// <param name="GapMs">Milliseconds to wait after the key comes up.</param>
public readonly record struct DockingStep(string Action, int HoldMs = 120, int GapMs = 250);

/// <summary>
/// One docking request's view of the journal, opened before the first key is sent — Elite reports
/// nothing about the contacts list, so <c>DockingRequested</c> arriving is the only evidence the walk
/// worked (#150).
/// </summary>
public interface IDockingWatch
{
    /// <summary>Whether a docking request reached the journal after this watch was opened.</summary>
    Task<bool?> ConfirmAsync(CancellationToken cancellationToken);
}

/// <summary>A watch that answers what it was told to.</summary>
public sealed class FixedDockingWatch(bool? answer) : IDockingWatch
{
    public Task<bool?> ConfirmAsync(CancellationToken cancellationToken) => Task.FromResult(answer);
}

/// <summary>
/// Requesting docking (#150). Elite binds no action for it, so this walks the left panel's contacts tab
/// and confirms by the journal rather than by anything it can see.
/// </summary>
public static class Docking
{
    /// <summary>What this walk's trace folder is called (#365).</summary>
    private const string TraceCaller = "request-docking";

    /// <summary>
    /// The gap a traced run waits after every press, in place of the sequence's own. A still is grabbed
    /// on the writer thread behind the sequence, so the ordinary gaps are too short for one to land
    /// before the next key goes.
    /// </summary>
    private static readonly TimeSpan TracedGap = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// The walk. The comms panel goes first so that the next press opens the external panel rather than
    /// toggling it shut, and the last press closes it whether or not a request went in.
    ///
    /// The first <c>ui_select</c> commits the highlighted contact. Until it has, <c>ui_right</c> is
    /// swallowed and focus stays on the list, so the contacts panel has to be entered before its actions
    /// can be reached.
    ///
    /// Elite remembers which tab the external panel was left on, and nothing d47 can read reports it —
    /// <see cref="GuiFocus"/> says only that the panel is open. The two <c>next_panel</c> presses reach
    /// the contacts tab from the navigation tab and from nowhere else, so a panel left elsewhere sends
    /// the rest of the walk into the wrong tab. The journal is the only verdict.
    /// </summary>
    public static readonly IReadOnlyList<DockingStep> Walk =
    [
        new("comms_panel", GapMs: 500),
        new("left_panel", GapMs: 500),
        new("next_panel", GapMs: 350),
        new("next_panel", GapMs: 350),
        new("ui_select", GapMs: 350),
        new("ui_right", GapMs: 350),
        new("ui_select"),
        new("left_panel", GapMs: 0),
    ];

    /// <summary>Walks the contacts panel and waits for the journal to confirm.</summary>
    /// <param name="actions">Bindings, status, foreground and something to press with.</param>
    /// <param name="watch">
    /// Opens a watch on the journal, called before the first key so a request already in cannot be
    /// counted as this one.
    /// </param>
    /// <param name="dockingComputer">
    /// Whether a docking computer is fitted, for the one phrase that needs one, and null for the phrases
    /// that do not. A loadout that cannot answer is treated as fitted.
    /// </param>
    /// <param name="cancellationToken">Interruption.</param>
    public static async Task<DockingOutcome> RunAsync(
        ActionSurface actions,
        Func<IDockingWatch> watch,
        Func<bool?>? dockingComputer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(watch);

        var status = actions.Status();

        if (!status.IsKnown)
        {
            return Refuse("I cannot see the ship's status, so I do not know where you are. Nothing pressed.");
        }

        if (status.Has(StatusFlags.Docked))
        {
            return Refuse("You are already docked.");
        }

        var context = actions.Context;

        if (context != ControlContext.NormalSpace)
        {
            return Refuse($"Requesting docking works in normal space, and you are {ControlContexts.Describe(context)}.");
        }

        // A map or a scanner mode takes the direction keys for its own, so the walk would drive that
        // instead. Only the cockpit panels may be in front of it; the walk starts by switching between
        // two of them, which works from any of the four or from none.
        if (status.GuiFocus is not (GuiFocus.None
            or GuiFocus.InternalPanel
            or GuiFocus.ExternalPanel
            or GuiFocus.CommsPanel
            or GuiFocus.RolePanel))
        {
            return Refuse($"{Showing(status.GuiFocus)} is open, and the walk would press its keys into that. Close it first.");
        }

        // Elite leaves Destination out when nothing is selected, and a body id of zero means the system
        // itself rather than anything in it to dock at.
        if (status.Destination is not { Body: not 0 })
        {
            return Refuse("Nothing is selected to dock at. Target the station first.");
        }

        var computer = dockingComputer?.Invoke();

        if (computer is false)
        {
            return Refuse(
                "There is no docking computer fitted, so I cannot take us in. Ask me to request docking "
                + "and fly it yourself.");
        }

        var binds = actions.Binds();
        var resolved = new Dictionary<string, EliteBinding>(StringComparer.Ordinal);

        // All seven or none, before a key is sent.
        foreach (var step in Walk)
        {
            if (resolved.ContainsKey(step.Action))
            {
                continue;
            }

            if (GameActions.Find(step.Action) is not { } action)
            {
                return Refuse($"There is no action called '{step.Action}'.");
            }

            var reach = ActionReachability.Resolve(action, binds, context);

            if (!reach.IsOffered || reach.Binding is null)
            {
                return Refuse(reach.Reason);
            }

            resolved[step.Action] = reach.Binding;
        }

        // Everything that could refuse has refused.
        actions.Acknowledge("Requesting docking.");

        // Null on every run that did not ask for one on the command line (#365).
        using var trace = actions.Input.Trace(TraceCaller);

        trace?.Declare("destination", status.Destination?.Name ?? "unnamed");
        trace?.Declare("gui focus at open", status.GuiFocus.ToString());
        trace?.Declare("docking computer", computer?.ToString() ?? "not asked");

        var journal = watch();

        // Held rather than written where it is decided: the verdict is the trace's last line, and the
        // release below is the last thing that happens.
        (string Verdict, string Reason)? ending = null;

        try
        {
            var keys = new List<InputStep>();

            for (var index = 0; index < Walk.Count; index++)
            {
                var step = Walk[index];

                keys.AddRange(InputSequence.Hold(resolved[step.Action], TimeSpan.FromMilliseconds(step.HoldMs)));

                // A traced run waits longer after every press, the last one included, so the walk is seven
                // stills of where the panel actually went rather than one blurred run.
                var gap = trace is null ? TimeSpan.FromMilliseconds(step.GapMs) : TracedGap;

                if (gap > TimeSpan.Zero)
                {
                    keys.Add(InputStep.Wait(gap).Watched($"{index + 1:00}-after-{step.Action}"));
                }
            }

            var walked = await actions.Input.SendAsync(keys, trace, cancellationToken).ConfigureAwait(false);

            if (!walked.Sent)
            {
                ending = ("refused", walked.Reason);

                return Refuse(walked.Reason);
            }

            var confirmed = await journal.ConfirmAsync(cancellationToken).ConfigureAwait(false);

            trace?.Declare("docking request seen", confirmed?.ToString() ?? "cannot tell");

            var outcome = confirmed switch
            {
                // The verdict, said after the walk; the acknowledgement went before it (#158).
                true => new DockingOutcome(DockingEnding.Requested, "Docking requested."),
                false => new DockingOutcome(
                    DockingEnding.NoRequest,
                    "I walked the contacts panel and no docking request went in. The panel may have "
                    + "opened on another tab, or the station may not have been the first contact in "
                    + "the list."),
                null => new DockingOutcome(
                    DockingEnding.Unknown,
                    "I walked the contacts panel but cannot tell whether the request went in."),
            };

            ending = (outcome.Ending.ToString(), outcome.Message);

            return outcome;
        }
        finally
        {
            actions.Input.ReleaseAll();

            if (ending is { } end)
            {
                trace?.Verdict(end.Verdict, end.Reason);
            }
        }
    }

    private static DockingOutcome Refuse(string message) => new(DockingEnding.Refused, message);

    /// <summary>How a Commander would hear the view that is in the way named.</summary>
    private static string Showing(GuiFocus focus) => focus switch
    {
        GuiFocus.StationServices => "Station services",
        GuiFocus.GalaxyMap => "The galaxy map",
        GuiFocus.SystemMap => "The system map",
        GuiFocus.Orrery => "The orrery",
        GuiFocus.FssMode => "The full spectrum scanner",
        GuiFocus.SaaMode => "The surface scanner",
        GuiFocus.Codex => "The codex",
        _ => "A full-screen view",
    };
}
