using System.Collections.Concurrent;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;

namespace D47.Core.Hotas;

/// <summary>What a mapped switch is currently able to do, which is what the panel shows.</summary>
public enum SwitchHealth
{
    /// <summary>Read, mapped, and reconciling.</summary>
    Ready,

    /// <summary>The device it was learned on is not there.</summary>
    Reassign,

    /// <summary>Captured but no position names an action yet.</summary>
    Unassigned,

    /// <summary>Nothing is switched on that would let it press anything.</summary>
    Off,

    /// <summary>No controllers are being read yet.</summary>
    Waiting,
}

/// <summary>
/// One mapped switch as it stands right now: where it is, whether d47 can act on it, and whether it
/// agrees with the game.
/// </summary>
public sealed record SwitchState
{
    public required string Name { get; init; }

    public required SwitchHealth Health { get; init; }

    /// <summary>Where the switch is sitting, or null when the reading does not name a position.</summary>
    public SwitchPosition? Position { get; init; }

    /// <summary>A sentence about the switch as a whole.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>
    /// Set when the switch's current position asks for a state the game is not in (Phase 21, "Show
    /// which switches disagree with the game").
    /// </summary>
    public string? Disagrees { get; init; }

    /// <summary>
    /// Set when Elite binds the same button this switch position sits on, so both act on every flip
    /// (#147).
    /// </summary>
    public string? Collides { get; init; }

    /// <summary>
    /// Set once something has put back what this switch set, within <see cref="SwitchReconciler.ContestWindow"/>
    /// of D47 setting it. The switch keeps acting on every flip.
    /// </summary>
    public string? Reversed { get; init; }
}

/// <summary>One reconcile waiting to be carried out.</summary>
/// <param name="Switch">The switch that asked, for the log and for the refusal.</param>
/// <param name="Label">How the action — or the page — is named mid-sentence.</param>
/// <param name="Steps">Empty when this is only something to say, or only somewhere to go.</param>
/// <param name="Destination">
/// The destination key the host is to put the panel on, for a position that names a page rather than an
/// action (Phase 46).
/// </param>
/// <param name="View">
/// The headset view, for a position that names one. Carried out off duty as well: with no <paramref name="Say"/>
/// it is one press of Shift+Win+D, with one it is only something to say.
/// </param>
/// <param name="Chord">The key chord, for a position that names one, pressed once in whatever window is in front.</param>
public readonly record struct PendingReconcile(
    string Switch,
    string Label,
    IReadOnlyList<InputStep> Steps,
    string? Say = null,
    string? Destination = null,
    HeadsetView? View = null,
    KeyChord? Chord = null);

/// <summary>Everything the reconciler needs from outside, per poll.</summary>
public readonly record struct SwitchTick
{
    public required DateTimeOffset Now { get; init; }

    public required IReadOnlyList<HotasReading> Readings { get; init; }

    public required GameStatus Status { get; init; }

    public required EliteBinds Binds { get; init; }

    /// <summary>Whether the Commander has switched switch reconciling on, and key injection with it.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Whether the Commander has let a switch change the headset view.</summary>
    public bool HeadsetEnabled { get; init; }

    /// <summary>Whether the Commander has let a switch press key chords in any window.</summary>
    public bool ChordsEnabled { get; init; }

    /// <summary>
    /// Every page a surface has registered, in bar order — the vocabulary a <see
    /// cref="SwitchPosition.Destination"/> is checked against (Phase 46).
    /// </summary>
    public IReadOnlyList<PanelDestination> Destinations
    {
        get => _destinations ?? [];
        init => _destinations = value;
    }

    // A backing field rather than an initialiser, because a record struct may not carry one without declaring
    // a constructor, and a constructor here would un-require every field.
    private readonly IReadOnlyList<PanelDestination>? _destinations;

    /// <summary>
    /// The destination key the panel is showing, or null when d47 cannot say — no surface is up, or two
    /// surfaces are showing different things.
    /// </summary>
    public string? Showing { get; init; }

    public ControlContext Context => ControlContexts.Of(Status);
}

/// <summary>The reconciler (Phase 21, "A switch position means a state, not a press").</summary>
/// <param name="headset">Which view the headset shows, or null when nothing can say.</param>
public sealed class SwitchReconciler(ILogger<SwitchReconciler> logger, IHeadsetViewSource? headset = null)
{
    /// <summary>
    /// How long after a reconcile an unexplained change back counts as something else driving the same
    /// action.
    /// </summary>
    public static readonly TimeSpan ContestWindow = TimeSpan.FromSeconds(4);

    /// <summary>The wait between two taps of one target.</summary>
    private static readonly TimeSpan PartGap = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentQueue<PendingReconcile> _pending = new();

    /// <summary>Guards the dictionaries and the view below.</summary>
    private readonly Lock _gate = new();

    /// <summary>Where each switch was on the last poll that named a position.</summary>
    private readonly Dictionary<string, SwitchPosition> _last = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The last reversal seen on each switch, as the row shows it.</summary>
    private readonly Dictionary<string, string> _reversed = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Watch> _watching = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ViewWatch> _viewWatching = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When each switch's last headset press was first seen sent.</summary>
    private readonly Dictionary<string, DateTimeOffset> _lastHeadsetPress = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The focus each switch is moving the game to, or holding it at.</summary>
    private readonly Dictionary<string, FocusGoal> _focusGoals = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The split each switch has set the distributor to, and is watching.</summary>
    private readonly Dictionary<string, PipGoal> _pipGoals = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The headset view as read on this poll, or null until something asks.</summary>
    private HeadsetView? _view;

    private IReadOnlyList<SwitchState> _states = [];

    /// <summary>Every mapped switch as it stands.</summary>
    public IReadOnlyList<SwitchState> States => _states;

    /// <summary>The switches that currently sit against the game's state.</summary>
    public IReadOnlyList<SwitchState> Disagreeing =>
        [.. _states.Where(state => state.Disagrees is not null)];

    /// <summary>
    /// What became of a headset press: the watch on it starts with the next poll once it was sent, and ends when
    /// it was not.
    /// </summary>
    public void HeadsetPressed(string name, HeadsetView view, bool sent)
    {
        lock (_gate)
        {
            // A report for an earlier flip's press leaves the newer watch alone.
            if (!_viewWatching.TryGetValue(name, out var watch) || watch.View != view)
            {
                return;
            }

            if (sent)
            {
                _viewWatching[name] = watch with { Sent = true };
            }
            else
            {
                _viewWatching.Remove(name);
            }
        }
    }

    /// <summary>One poll.</summary>
    public void Poll(SwitchTick tick, IReadOnlyList<SwitchMapping> mappings)
    {
        var states = new List<SwitchState>(mappings.Count);

        lock (_gate)
        {
            // Read at most once a poll, and only when a position or a watch asks.
            _view = null;

            foreach (var mapping in mappings)
            {
                try
                {
                    states.Add(Examine(tick, mapping) with { Reversed = _reversed.GetValueOrDefault(mapping.Name) });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Switch {Name} could not be examined", mapping.Name);

                    states.Add(new SwitchState
                    {
                        Name = mapping.Name,
                        Health = SwitchHealth.Off,
                        Note = "D47 could not read that switch.",
                    });
                }
            }
        }

        _states = states;
    }

    /// <summary>Everything decided since the last drain, in the order it was decided.</summary>
    public IReadOnlyList<PendingReconcile> Drain()
    {
        var drained = new List<PendingReconcile>();

        while (_pending.TryDequeue(out var pending))
        {
            drained.Add(pending);
        }

        return drained;
    }

    private SwitchState Examine(SwitchTick tick, SwitchMapping mapping)
    {
        // Item 4, and the whole of it.
        if (tick.Readings.FirstOrDefault(reading => reading.Id == mapping.DeviceId) is not { } reading)
        {
            _last.Remove(mapping.Name);
            _watching.Remove(mapping.Name);
            _viewWatching.Remove(mapping.Name);
            _focusGoals.Remove(mapping.Name);
            _pipGoals.Remove(mapping.Name);

            return new SwitchState
            {
                Name = mapping.Name,
                Health = tick.Readings.Count == 0 ? SwitchHealth.Waiting : SwitchHealth.Reassign,
                Note = tick.Readings.Count == 0
                    ? "D47 is still finding your controllers."
                    : "This switch was learned on a device D47 can no longer find, so it is doing nothing. "
                      + "Flip it on the HOTAS switches page and D47 will find the device again. Assign it again "
                      + "if the throttle's 4x32 mode was turned on or off: that renumbers every button on the "
                      + "throttle, so the mapping is retired rather than pointed at the wrong one.",
            };
        }

        var position = mapping.At(reading);

        // Read before Flipped moves the last position on.
        var flick = Flick(mapping, reading, position);
        var flipped = position is not null && Flipped(mapping.Name, position);

        if (flipped || flick is not null)
        {
            LogFlip(tick, mapping, position!, flick);
        }

        // Watched before the flip is acted on, so a second flip inside the window is not blamed for what the
        // first one started. A flick is a flip.
        Watching(tick, mapping, flipped || flick is not null);
        WatchingTheHeadset(tick, mapping, flipped || flick is not null);
        WatchingTheFocus(tick, mapping, flipped || flick is not null);
        var pipsInFlight = WatchingThePips(tick, mapping, flipped || flick is not null);

        if (!mapping.IsAssigned)
        {
            return new SwitchState
            {
                Name = mapping.Name,
                Health = SwitchHealth.Unassigned,
                Position = position,
                Note = "No position of this switch means anything yet.",
            };
        }

        if (position is null)
        {
            return new SwitchState
            {
                Name = mapping.Name,
                Health = SwitchHealth.Ready,
                Note = "D47 cannot tell which position that switch is in.",
            };
        }

        IReadOnlyList<WatchedPart>? kept = (flipped || flick is not null) && _watching.TryGetValue(mapping.Name, out var held)
            ? held.Parts
            : null;

        // A flick is a flip to the position pressed, then a flip back.
        if (flick is not null)
        {
            Reconcile(tick, mapping, flick, flipped: true, pipsInFlight);
            flipped = true;
        }

        var state = Reconcile(tick, mapping, position, flipped, pipsInFlight);

        // Only a position that presses actions decides against the unconfirmed presses; any other flip ends the watch.
        if (kept is not null && _watching.TryGetValue(mapping.Name, out var after) && ReferenceEquals(after.Parts, kept))
        {
            _watching.Remove(mapping.Name);
        }

        return state with
        {
            Disagrees = Disagreement(tick, position),
            Collides = Collision(tick, mapping, position),
        };
    }

    /// <summary>
    /// The one position pressed and released since the last poll, when the switch is back where it was then;
    /// otherwise null. A switch now somewhere new only passed through it, and two such positions have no
    /// order.
    /// </summary>
    private SwitchPosition? Flick(SwitchMapping mapping, HotasReading reading, SwitchPosition? current)
    {
        if (current is null || !_last.TryGetValue(mapping.Name, out var last) || last.Button != current.Button)
        {
            return null;
        }

        SwitchPosition? flick = null;

        foreach (var position in mapping.Positions)
        {
            if (position.Button is not { } button
                || button == current.Button
                || reading.IsHeld(button)
                || !reading.WasDownSinceLastRead(button))
            {
                continue;
            }

            if (flick is not null)
            {
                logger.LogInformation(
                    "Switch {Name} was in more than one position between two readings, so only where it is now counts",
                    mapping.Name);

                return null;
            }

            flick = position;
        }

        return flick;
    }

    /// <summary>Logs every flip with the Status.json values it is decided on.</summary>
    private void LogFlip(SwitchTick tick, SwitchMapping mapping, SwitchPosition position, SwitchPosition? flick)
    {
        var status = tick.Status;
        var unconfirmed = Unconfirmed(tick, mapping.Name).Values;

        logger.LogInformation(
            "Switch {Name} flipped to {Position}{Through}; Status.json read at {ReadAt}: flags {Flags}, flags2 {Flags2}, "
            + "focus {Focus}, pips {Pips}; D47's unconfirmed presses: {Unconfirmed}",
            mapping.Name,
            position.Describe(),
            flick is null ? string.Empty : $" through {flick.Describe()}",
            status.ReadAt?.ToString("O") ?? "never",
            status.Flags,
            $"0x{status.Flags2:X8}",
            status.GuiFocus,
            status.Pips?.ToString() ?? "unknown",
            unconfirmed.Count == 0
                ? "none"
                : string.Join(", ", unconfirmed.Select(part => $"{part.Action.Id} {part.State}")));
    }

    /// <summary>
    /// D47's presses for this switch that Status.json does not show yet, by action id: while one is in flight the
    /// flag still reads the state from before it.
    /// </summary>
    private Dictionary<string, WatchedPart> Unconfirmed(SwitchTick tick, string name)
    {
        var unconfirmed = new Dictionary<string, WatchedPart>(StringComparer.Ordinal);

        if (!_watching.TryGetValue(name, out var watch) || tick.Context != watch.Context || tick.Now > watch.Until)
        {
            return unconfirmed;
        }

        foreach (var part in watch.Parts)
        {
            if (!part.Arrived && part.Action.AlreadyIn(part.State, tick.Status) == false)
            {
                unconfirmed[part.Action.Id] = part;
            }
        }

        return unconfirmed;
    }

    /// <summary>
    /// Records that something put back what a switch set. The row shows it from then on; it is said once per switch.
    /// </summary>
    private void NoteReversal(SwitchMapping mapping, string label, string note, string what, TimeSpan sincePress)
    {
        var first = !_reversed.ContainsKey(mapping.Name);
        _reversed[mapping.Name] = note;

        logger.LogWarning(
            "Switch {Name}: {What} went back on its own {Since} s after D47 set it; the switch still acts on every flip",
            mapping.Name,
            what,
            sincePress.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));

        if (first)
        {
            _pending.Enqueue(new PendingReconcile(mapping.Name, label, [], note));
        }
    }

    /// <summary>Whether this poll is a flip.</summary>
    private bool Flipped(string name, SwitchPosition position)
    {
        if (!_last.TryGetValue(name, out var last))
        {
            _last[name] = position;
            return false;
        }

        if (last.Button == position.Button)
        {
            return false;
        }

        _last[name] = position;

        return true;
    }

    private SwitchState Reconcile(
        SwitchTick tick,
        SwitchMapping mapping,
        SwitchPosition position,
        bool flipped,
        bool pipsInFlight)
    {
        var ready = new SwitchState { Name = mapping.Name, Health = SwitchHealth.Ready, Position = position };

        if (position.Destination is { Length: > 0 } destination)
        {
            return ReconcilePanel(tick, mapping, position, destination, flipped, ready);
        }

        if (position.View is { } view)
        {
            return ReconcileHeadset(tick, mapping, position, view, flipped, ready);
        }

        if (position.Chord is { } chord)
        {
            return ReconcileChord(tick, mapping, position, chord, flipped, ready);
        }

        if (!tick.Enabled)
        {
            return ready with
            {
                Health = SwitchHealth.Off,
                Note = "Switch reconciling is off. Turn it on to let this switch operate the ship.",
            };
        }

        if (FocusTargets.Find(position.Target) is { } focus)
        {
            return ReconcileFocus(tick, mapping, position, focus, flipped, ready);
        }

        if (PipTargets.Find(position.Target) is { } pips)
        {
            return ReconcilePips(tick, mapping, position, pips, flipped, pipsInFlight, ready);
        }

        var (label, wanted) = Wanted(position);

        if (wanted.Count == 0)
        {
            return ready with { Note = $"{position.Describe()} means nothing." };
        }

        var presses = new List<(GameAction Action, DesiredState State, EliteBinding Binding)>();
        var pressed = new List<WatchedPart>();

        foreach (var (action, state) in wanted)
        {
            var reach = ActionReachability.Resolve(action, tick.Binds, tick.Context);

            if (!reach.IsOffered)
            {
                // The refusal carries the reason, for the same reason the tool path's does: the audience is
                // HOTAS-heavy, so an action bound only to a stick is the common case, and it fails as silence
                // unless something says why.
                return ready with { Note = reach.Reason };
            }

            presses.Add((action, state, reach.Binding!));
        }

        if (!flipped)
        {
            // Between flips nothing is touched.
            return ready;
        }

        // A press Status.json does not show yet is decided against the state it was for, not the flag.
        var unconfirmed = Unconfirmed(tick, mapping.Name);
        var steps = new List<InputStep>();

        foreach (var (action, state, binding) in presses)
        {
            var flag = action.AlreadyIn(state, tick.Status);
            WatchedPart? inFlight = state != DesiredState.Toggle && unconfirmed.TryGetValue(action.Id, out var part)
                ? part
                : null;
            var there = inFlight is { } last ? last.State == state : flag;

            logger.LogInformation(
                "Switch {Name} moved to {Position}; {Action} {State}: Status.json says already there {Flag}, "
                + "D47 last pressed for {InFlight}; {Decision}",
                mapping.Name,
                position.Describe(),
                action.Id,
                state,
                flag?.ToString() ?? "unknown",
                inFlight?.State.ToString() ?? "nothing unconfirmed",
                there == true ? "nothing pressed" : $"pressing {binding.Gesture()}");

            if (there == true)
            {
                continue;
            }

            // A tap straight after an unconfirmed one is kept apart from it.
            if (steps.Count > 0 || inFlight is not null)
            {
                steps.Add(InputStep.Wait(PartGap));
            }

            steps.AddRange(InputSequence.Tap(binding));
            pressed.Add(new WatchedPart(action, state));
        }

        // Item 5's watch starts here, with the states the presses were for and the mode they were made in. A press
        // still unconfirmed for an action this flip did not press stays watched.
        List<WatchedPart> watched =
        [
            .. pressed,
            .. unconfirmed.Values.Where(part => pressed.All(made => made.Action.Id != part.Action.Id)),
        ];

        if (watched.Count == 0)
        {
            _watching.Remove(mapping.Name);
        }
        else
        {
            _watching[mapping.Name] = new Watch(watched, tick.Now + ContestWindow, tick.Context);
        }

        if (pressed.Count > 0)
        {
            _pending.Enqueue(new PendingReconcile(mapping.Name, label, steps));
        }

        return ready;
    }

    /// <summary>
    /// A position that names a focus. A flip sets a goal; every poll after it presses the next step, closing the
    /// open interface first and opening the target once the game reads the cockpit.
    /// </summary>
    private SwitchState ReconcileFocus(
        SwitchTick tick,
        SwitchMapping mapping,
        SwitchPosition position,
        FocusTarget target,
        bool flipped,
        SwitchState ready)
    {
        var current = tick.Status.GuiFocus;
        FocusGoal? held = _focusGoals.TryGetValue(mapping.Name, out var known) ? known : null;

        if (flipped)
        {
            held = null;
            _focusGoals.Remove(mapping.Name);

            if (current == target.Focus)
            {
                logger.LogInformation(
                    "Switch {Name} moved to {Position}; {Focus} is already open, so nothing was pressed",
                    mapping.Name,
                    position.Describe(),
                    current);

                return ready;
            }

            held = new FocusGoal(target, FocusStage.Start, tick.Now + ContestWindow);
        }

        if (held is not { } goal)
        {
            return current == target.Focus || FocusTargets.Of(current) is not null
                ? ready
                : ready with { Note = Untouched(current) };
        }

        if (current == target.Focus)
        {
            _focusGoals[mapping.Name] = goal with { Stage = FocusStage.Arrived, Until = tick.Now + ContestWindow };
            return ready;
        }

        if (goal.Stage == FocusStage.Arrived)
        {
            return ready;
        }

        if (goal.Stage != FocusStage.Start && tick.Now > goal.Until)
        {
            _focusGoals.Remove(mapping.Name);

            var silent = $"I set {FocusTargets.Say(target.Focus)} from {mapping.Name} and it did not take. "
                         + "Something else may be bound to the same button.";

            logger.LogWarning("Switch {Name} pressed for {Target} and the focus never arrived", mapping.Name, target.Id);
            _pending.Enqueue(new PendingReconcile(mapping.Name, FocusTargets.Say(target.Focus), [], silent));

            return ready;
        }

        var closing = FocusTargets.Of(current);

        if (closing is null)
        {
            _focusGoals.Remove(mapping.Name);
            return ready with { Note = Untouched(current) };
        }

        if (current != GuiFocus.None && goal.Stage == FocusStage.Closing)
        {
            return ready;
        }

        if (current == GuiFocus.None && goal.Stage == FocusStage.Opening)
        {
            return ready;
        }

        // The open key shuts what it opens, and the next step waits for the game to read the cockpit.
        var press = current == GuiFocus.None ? target : closing;

        if (GameActions.Find(press.OpenAction!) is not { } action)
        {
            _focusGoals.Remove(mapping.Name);
            return ready;
        }

        var reach = ActionReachability.Resolve(action, tick.Binds, tick.Context);

        if (!reach.IsOffered)
        {
            _focusGoals.Remove(mapping.Name);
            return ready with { Note = reach.Reason };
        }

        logger.LogInformation(
            "Switch {Name} is taking the game to {Target}; pressing {Gesture} for {Action} with {Focus} open",
            mapping.Name,
            target.Id,
            reach.Binding!.Gesture(),
            action.Id,
            current);

        _pending.Enqueue(new PendingReconcile(mapping.Name, FocusTargets.Say(press.Focus), InputSequence.Tap(reach.Binding)));

        _focusGoals[mapping.Name] = goal with
        {
            Stage = current == GuiFocus.None ? FocusStage.Opening : FocusStage.Closing,
            Until = tick.Now + ContestWindow,
        };

        return ready;
    }

    /// <summary>
    /// A position that names a power split. A flip resets to balanced and presses the target's sequence as one
    /// reconcile, unless the distributor already reads the split.
    /// </summary>
    private SwitchState ReconcilePips(
        SwitchTick tick,
        SwitchMapping mapping,
        SwitchPosition position,
        PipTarget target,
        bool flipped,
        bool inFlight,
        SwitchState ready)
    {
        var actions = new List<GameAction>();
        var bindings = new List<EliteBinding>();

        foreach (var id in target.Presses.Prepend(PipTargets.Reset))
        {
            if (GameActions.Find(id) is not { } action)
            {
                return ready;
            }

            var reach = ActionReachability.Resolve(action, tick.Binds, tick.Context);

            if (!reach.IsOffered)
            {
                return ready with { Note = reach.Reason };
            }

            actions.Add(action);
            bindings.Add(reach.Binding!);
        }

        if (!flipped)
        {
            return ready;
        }

        // Presses still queued for an earlier flip are not in Status.json yet, so a match there proves nothing.
        var unsettled = inFlight || (_pipGoals.TryGetValue(mapping.Name, out var earlier) && !earlier.Arrived);
        _pipGoals.Remove(mapping.Name);

        if (!unsettled && tick.Status.Pips == target.Pips)
        {
            logger.LogInformation(
                "Switch {Name} moved to {Position}; the pips are already {Pips}, so nothing was pressed",
                mapping.Name,
                position.Describe(),
                target.Pips);

            return ready;
        }

        var steps = new List<InputStep>();

        for (var index = 0; index < actions.Count; index++)
        {
            if (index > 0)
            {
                steps.Add(InputStep.Wait(PartGap));
            }

            steps.AddRange(InputSequence.Tap(bindings[index]));
        }

        logger.LogInformation(
            "Switch {Name} moved to {Position}; pressing {Presses} for {Target} from {Pips}",
            mapping.Name,
            position.Describe(),
            string.Join(", ", actions.Select(action => action.Id)),
            target.Id,
            tick.Status.Pips?.ToString() ?? "unknown pips");

        _pending.Enqueue(new PendingReconcile(mapping.Name, target.Name, steps));
        _pipGoals[mapping.Name] = new PipGoal(target, tick.Now + ContestWindow, tick.Context);

        return ready;
    }

    /// <summary>Pips that leave the target within the contest window are noted on the switch; pips that never reach it are said aloud.</summary>
    /// <returns>True when the switch flipped before the presses for the last pip target showed in Status.json.</returns>
    private bool WatchingThePips(SwitchTick tick, SwitchMapping mapping, bool flipped)
    {
        if (!_pipGoals.TryGetValue(mapping.Name, out var goal))
        {
            return false;
        }

        if (flipped || tick.Context != goal.Context || tick.Status.Pips is not { } pips)
        {
            _pipGoals.Remove(mapping.Name);
            return flipped && !goal.Arrived;
        }

        if (pips == goal.Target.Pips)
        {
            _pipGoals[mapping.Name] = goal with { Arrived = true };
            return false;
        }

        if (tick.Now > goal.Until)
        {
            _pipGoals.Remove(mapping.Name);

            if (!goal.Arrived)
            {
                var silent = $"I set the power distributor to {goal.Target.Name} from {mapping.Name} and it did not take. "
                             + "Something else may be bound to the same buttons.";

                logger.LogWarning("Switch {Name} pressed for {Target} and the pips never arrived", mapping.Name, goal.Target.Id);
                _pending.Enqueue(new PendingReconcile(mapping.Name, goal.Target.Name, [], silent));
            }

            return false;
        }

        if (!goal.Arrived)
        {
            return false;
        }

        _pipGoals.Remove(mapping.Name);

        NoteReversal(
            mapping,
            goal.Target.Name,
            $"Something else changed the power distributor right after D47 set it to {goal.Target.Name}. "
            + $"D47 still acts on every flip of {mapping.Name}.",
            $"the pips for {goal.Target.Id}, now {pips},",
            tick.Now - (goal.Until - ContestWindow));

        return false;
    }

    private static string Untouched(GuiFocus focus) =>
        $"{Capitalised(FocusTargets.Say(focus))} is open, so this switch pressed nothing.";

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Focus that leaves its target within the contest window is noted on the switch.</summary>
    private void WatchingTheFocus(SwitchTick tick, SwitchMapping mapping, bool flipped)
    {
        if (!_focusGoals.TryGetValue(mapping.Name, out var goal) || goal.Stage != FocusStage.Arrived)
        {
            return;
        }

        if (flipped || tick.Now > goal.Until)
        {
            _focusGoals.Remove(mapping.Name);
            return;
        }

        if (tick.Status.GuiFocus == goal.Target.Focus)
        {
            return;
        }

        _focusGoals.Remove(mapping.Name);

        NoteReversal(
            mapping,
            FocusTargets.Say(goal.Target.Focus),
            $"Something else took {FocusTargets.Say(goal.Target.Focus)} away right after D47 set it. "
            + $"D47 still acts on every flip of {mapping.Name}.",
            $"{goal.Target.Id}, now {tick.Status.GuiFocus},",
            tick.Now - (goal.Until - ContestWindow));
    }

    /// <summary>The label and the action states a position asks for; empty when it asks for none.</summary>
    private static (string Label, IReadOnlyList<(GameAction Action, DesiredState State)> Wanted) Wanted(
        SwitchPosition position)
    {
        if (position.Target is { Length: > 0 } id)
        {
            if (SwitchTargets.Find(id) is not { } target)
            {
                return (id, []);
            }

            return (target.Name, [.. target.Parts
                .Where(part => GameActions.Find(part.Action) is not null)
                .Select(part => (GameActions.Find(part.Action)!, part.State))]);
        }

        if (position.Action is { Length: > 0 } actionId && GameActions.Find(actionId) is { } action)
        {
            return (action.Label, [(action, position.State)]);
        }

        return (string.Empty, []);
    }

    /// <summary>A position that names a page of d47's own panel (Phase 46).</summary>
    private SwitchState ReconcilePanel(
        SwitchTick tick,
        SwitchMapping mapping,
        SwitchPosition position,
        string destination,
        bool flipped,
        SwitchState ready)
    {
        if (tick.Destinations.Count == 0)
        {
            return ready with { Note = "D47's panel is not up yet." };
        }

        if (RelativeTab.Direction(destination) is not null)
        {
            return ReconcileRelativeTab(mapping, position, destination, flipped, ready);
        }

        if (Page(tick, destination) is not { } page)
        {
            // The vocabulary is whatever the surfaces registered, so this is where a hand-edited key is
            // checked — by name, the way every other refusal in this file is.
            return ready with { Note = $"Nothing on D47's panel is called '{destination}'." };
        }

        if (!flipped)
        {
            return ready;
        }

        if (tick.Showing is { } showing && Agrees(tick, destination, showing))
        {
            logger.LogInformation(
                "Switch {Name} moved to {Position}; the panel is already on {Page}, so nothing moved",
                mapping.Name,
                position.Describe(),
                page.Word);

            return ready;
        }

        logger.LogInformation(
            "Switch {Name} moved to {Position}; showing {Page}",
            mapping.Name,
            position.Describe(),
            page.Word);

        _pending.Enqueue(new PendingReconcile(mapping.Name, page.Word, [], Destination: destination));

        return ready;
    }

    /// <summary>A position that moves the panel one tab along the bar; each surface steps from its own tab.</summary>
    private SwitchState ReconcileRelativeTab(
        SwitchMapping mapping,
        SwitchPosition position,
        string destination,
        bool flipped,
        SwitchState ready)
    {
        if (!flipped)
        {
            return ready;
        }

        logger.LogInformation(
            "Switch {Name} moved to {Position}; moving the panel to the {Word}",
            mapping.Name,
            position.Describe(),
            RelativeTab.Word(destination));

        _pending.Enqueue(new PendingReconcile(
            mapping.Name,
            destination == RelativeTab.Next ? "the next tab" : "the previous tab",
            [],
            Destination: destination));

        return ready;
    }

    /// <summary>A position that names a headset view.</summary>
    private SwitchState ReconcileHeadset(
        SwitchTick tick,
        SwitchMapping mapping,
        SwitchPosition position,
        HeadsetView view,
        bool flipped,
        SwitchState ready)
    {
        if (!tick.HeadsetEnabled)
        {
            return ready with
            {
                Health = SwitchHealth.Off,
                Note = "Changing the headset view is off. Turn on Let a HOTAS switch change the headset view "
                       + "to let this switch do it.",
            };
        }

        var showing = Headset();

        if (showing == HeadsetView.Unknown)
        {
            return ready with { Note = "Virtual Desktop is not streaming, so the headset view was left alone." };
        }

        if (!flipped)
        {
            return ready;
        }

        if (showing == view)
        {
            logger.LogInformation(
                "Switch {Name} moved to {Position}; the headset is already showing {View}, so nothing was pressed",
                mapping.Name,
                position.Describe(),
                view);

            return ready;
        }

        logger.LogInformation(
            "Switch {Name} moved to {Position}; the headset is showing {Showing}, so pressing Shift+Win+D for {View}",
            mapping.Name,
            position.Describe(),
            showing,
            view);

        _pending.Enqueue(new PendingReconcile(mapping.Name, Word(view), [], View: view));
        _viewWatching[mapping.Name] = new ViewWatch(view, showing);

        return ready;
    }

    /// <summary>A position that names a key chord: one press per flip into it, with nothing read back.</summary>
    private SwitchState ReconcileChord(
        SwitchTick tick,
        SwitchMapping mapping,
        SwitchPosition position,
        KeyChord chord,
        bool flipped,
        SwitchState ready)
    {
        if (!tick.ChordsEnabled)
        {
            return ready with
            {
                Health = SwitchHealth.Off,
                Note = "Pressing key chords is off. Turn on Let a HOTAS switch press key chords in any window "
                       + "to let this switch do it.",
            };
        }

        if (!flipped)
        {
            return ready;
        }

        logger.LogInformation(
            "Switch {Name} moved to {Position}; pressing {Chord}",
            mapping.Name,
            position.Describe(),
            chord.Describe());

        _pending.Enqueue(new PendingReconcile(mapping.Name, chord.Describe(), [], Chord: chord));

        return ready;
    }

    /// <summary>Which view the headset shows on this poll.</summary>
    private HeadsetView Headset() => _view ??= headset?.Read() ?? HeadsetView.Unknown;

    /// <summary>How a view is named mid-sentence.</summary>
    private static string Word(HeadsetView view) => view == HeadsetView.Vr ? "SteamVR" : "the desktop view";

    /// <summary>
    /// A press of Shift+Win+D that did not change the view is said once and logged; the switch stays ready, so the
    /// next flip presses again.
    /// </summary>
    private void WatchingTheHeadset(SwitchTick tick, SwitchMapping mapping, bool flipped)
    {
        if (!_viewWatching.TryGetValue(mapping.Name, out var watch))
        {
            return;
        }

        if (flipped)
        {
            _viewWatching.Remove(mapping.Name);
            return;
        }

        var showing = Headset();

        if (showing == watch.View)
        {
            _viewWatching.Remove(mapping.Name);
            return;
        }

        if (!watch.Sent)
        {
            return;
        }

        if (showing == HeadsetView.Unknown && !watch.SawUnknown)
        {
            watch = watch with { SawUnknown = true };
            _viewWatching[mapping.Name] = watch;
        }

        if (watch.Until is not { } until)
        {
            TimeSpan? sincePrevious = _lastHeadsetPress.TryGetValue(mapping.Name, out var previous)
                ? tick.Now - previous
                : null;

            _lastHeadsetPress[mapping.Name] = tick.Now;
            _viewWatching[mapping.Name] = watch with { Until = tick.Now + ContestWindow, SincePrevious = sincePrevious };
            return;
        }

        if (tick.Now <= until)
        {
            return;
        }

        _viewWatching.Remove(mapping.Name);

        // A reading lost since the press says nothing about the shortcut.
        if (showing != watch.From)
        {
            return;
        }

        const string said = "Shift+Win+D did not change the headset view. If you changed Virtual Desktop's "
                            + "shortcut, set it back.";

        logger.LogWarning(
            "Switch {Name} pressed Shift+Win+D for {View} and the headset view did not change: {Before} before the "
            + "press, {After} when the watch ended, {SincePrevious} since this switch's previous headset press, "
            + "reading lost in between: {SawUnknown}",
            mapping.Name,
            watch.View,
            watch.From,
            showing,
            watch.SincePrevious?.ToString() ?? "no previous press",
            watch.SawUnknown);

        _pending.Enqueue(new PendingReconcile(mapping.Name, Word(watch.View), [], said, View: watch.View));
    }

    private static PanelDestination? Page(SwitchTick tick, string key) =>
        tick.Destinations.FirstOrDefault(page => string.Equals(page.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// Whether the panel is where a position asks: on that destination, or, for a root, on one of the root's
    /// sub-pages, which is still that root.
    /// </summary>
    private static bool Agrees(SwitchTick tick, string destination, string showing) =>
        string.Equals(showing, destination, StringComparison.Ordinal)
        || (Page(tick, showing) is { SubPage: not null } beside
            && string.Equals(beside.Root.Key, destination, StringComparison.Ordinal));

    /// <summary>Item 5.</summary>
    private void Watching(SwitchTick tick, SwitchMapping mapping, bool flipped)
    {
        if (!_watching.TryGetValue(mapping.Name, out var watch))
        {
            return;
        }

        // A mode change explains a change back, and one of them is not hypothetical: hardpoints retract by
        // themselves on entering supercruise.
        if (tick.Context != watch.Context)
        {
            _watching.Remove(mapping.Name);
            return;
        }

        // A flip keeps only the unconfirmed presses, for the flip to be decided against; a press that arrived is no
        // longer watched for a change back.
        if (flipped)
        {
            var unconfirmed = watch.Parts.Where(part => !part.Arrived).ToArray();

            if (unconfirmed.Length == 0)
            {
                _watching.Remove(mapping.Name);
            }
            else
            {
                _watching[mapping.Name] = watch with { Parts = unconfirmed };
            }

            return;
        }

        if (tick.Now > watch.Until)
        {
            _watching.Remove(mapping.Name);

            // The press that never took, said out loud (#147).
            foreach (var part in watch.Parts)
            {
                if (part.Arrived || part.Action.AlreadyIn(part.State, tick.Status) != false)
                {
                    continue;
                }

                var silent = $"I set {part.Action.Label} from {mapping.Name} and it did not take. "
                             + "Something else may be bound to the same button.";

                logger.LogWarning(
                    "Switch {Name} pressed {Action} and the state never arrived",
                    mapping.Name,
                    part.Action.Id);

                _pending.Enqueue(new PendingReconcile(mapping.Name, part.Action.Label, [], silent));
            }

            return;
        }

        var parts = watch.Parts.ToArray();

        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            var there = part.Action.AlreadyIn(part.State, tick.Status);

            if (there == true)
            {
                parts[index] = part with { Arrived = true };
                continue;
            }

            if (there != false || !part.Arrived)
            {
                continue;
            }

            _watching.Remove(mapping.Name);

            NoteReversal(
                mapping,
                part.Action.Label,
                $"Something else is bound to {part.Action.Label}: it went back on its own right after D47 set it. "
                + $"D47 still acts on every flip of {mapping.Name}.",
                $"{part.Action.Id} {part.State} (flags {tick.Status.Flags})",
                tick.Now - (watch.Until - ContestWindow));

            return;
        }

        _watching[mapping.Name] = watch with { Parts = parts };
    }

    /// <summary>Whether Elite binds the same button, in which case every flip acts twice (#147).</summary>
    private static string? Collision(SwitchTick tick, SwitchMapping mapping, SwitchPosition position)
    {
        // Only a position that actually does something.
        if (!position.IsAssigned || position.Action is not { Length: > 0 } || position.Button is not { } button)
        {
            return null;
        }

        if (EliteBinds.EliteDeviceToken(mapping.Device) is not { } device)
        {
            return null;
        }

        var bound = tick.Binds.UsingJoystickButton(button, device);

        if (bound.Count == 0)
        {
            return null;
        }

        var actions = string.Join(" and ", bound.Select(binding => binding.Action).Distinct());

        return $"Elite binds this button to {actions} as well, so both act on every flip. "
               + "Unbind it in Elite, or move this switch to a button Elite does not use.";
    }

    /// <summary>Whether the position the switch is sitting in asks for a state the game is not in.</summary>
    private string? Disagreement(SwitchTick tick, SwitchPosition position)
    {
        if (position.View is { } view)
        {
            if (!tick.HeadsetEnabled)
            {
                return null;
            }

            return Headset() switch
            {
                var showing when showing == view => null,
                HeadsetView.Desktop => "the headset is showing the desktop",
                HeadsetView.Vr => "the headset is showing SteamVR",
                _ => null,
            };
        }

        if (position.Destination is { Length: > 0 } destination)
        {
            if (RelativeTab.Direction(destination) is not null)
            {
                return null;
            }

            // Only when d47 can say what the panel is showing, and only against a page that exists — a key
            // nothing answers to is reported on the row, not annunciated.
            if (tick.Showing is not { } showing
                || Agrees(tick, destination, showing)
                || Page(tick, destination) is null)
            {
                return null;
            }

            return $"the panel is on {Page(tick, showing)?.Word ?? showing}";
        }

        if (FocusTargets.Find(position.Target) is { } focus)
        {
            var open = tick.Status.GuiFocus;

            return open == focus.Focus ? null : $"{FocusTargets.Say(open)} is open";
        }

        if (PipTargets.Find(position.Target) is { } pipTarget)
        {
            return (pipTarget.Contexts & tick.Context) != 0
                   && tick.Status.Pips is { } pips
                   && pips != pipTarget.Pips
                ? $"pips are {pips}"
                : null;
        }

        foreach (var (action, state) in Wanted(position).Wanted)
        {
            if ((action.Contexts & tick.Context) != 0 && action.AlreadyIn(state, tick.Status) == false)
            {
                return $"{action.Label} is {(state == DesiredState.On ? "off" : "on")}";
            }
        }

        return null;
    }

    private enum FocusStage
    {
        /// <summary>Nothing pressed yet.</summary>
        Start,

        /// <summary>The open interface's key was pressed; waiting for the cockpit.</summary>
        Closing,

        /// <summary>The target's key was pressed; waiting for the target.</summary>
        Opening,

        /// <summary>The target is open; <c>Until</c> ends the contest watch.</summary>
        Arrived,
    }

    private readonly record struct FocusGoal(FocusTarget Target, FocusStage Stage, DateTimeOffset Until);

    private readonly record struct PipGoal(
        PipTarget Target,
        DateTimeOffset Until,
        ControlContext Context,
        bool Arrived = false);

    private readonly record struct WatchedPart(GameAction Action, DesiredState State, bool Arrived = false);

    private readonly record struct Watch(
        IReadOnlyList<WatchedPart> Parts,
        DateTimeOffset Until,
        ControlContext Context);

    /// <summary>
    /// A press of Shift+Win+D waiting for the view to change from <c>From</c> to <c>View</c>, timed from the first
    /// poll after it was sent.
    /// </summary>
    private readonly record struct ViewWatch(
        HeadsetView View,
        HeadsetView From,
        bool Sent = false,
        DateTimeOffset? Until = null,
        TimeSpan? SincePrevious = null,
        bool SawUnknown = false);
}
