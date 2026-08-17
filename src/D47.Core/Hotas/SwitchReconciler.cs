using System.Collections.Concurrent;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Hotas;

/// <summary>What a mapped switch is currently able to do, which is what the panel shows.</summary>
public enum SwitchHealth
{
    /// <summary>Read, mapped, and reconciling.</summary>
    Ready,

    /// <summary>
    /// The device it was learned on is not there. A mode change on the throttle does this, and
    /// it is the right answer rather than a fault — see <see cref="SwitchMapping.DeviceId"/>.
    /// </summary>
    Reassign,

    /// <summary>Something else is driving the same action, so d47 has stopped touching it.</summary>
    Paused,

    /// <summary>Captured but no position names an action yet.</summary>
    Unassigned,

    /// <summary>Nothing is switched on that would let it press anything.</summary>
    Off,

    /// <summary>
    /// No controllers are being read yet. Distinct from <see cref="Reassign"/> on purpose: the
    /// device list fills in over seconds rather than at startup (docs/spikes/hotas-switch-read.md,
    /// finding 1), so for the first moments of every run <em>every</em> switch is missing its
    /// device — and a surface calling that "needs reassigning" would raise a false alarm about a
    /// perfectly good mapping, every single launch.
    /// </summary>
    Waiting,
}

/// <summary>
/// One mapped switch as it stands right now: where it is, whether d47 can act on it, and whether
/// it agrees with the game.
/// </summary>
public sealed record SwitchState
{
    public required string Name { get; init; }

    public required SwitchHealth Health { get; init; }

    /// <summary>Where the switch is sitting, or null when the reading does not name a position.</summary>
    public SwitchPosition? Position { get; init; }

    /// <summary>A sentence about the switch as a whole. Empty when there is nothing to say.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>
    /// Set when the switch's current position asks for a state the game is not in (list.md
    /// Phase 21, "Show which switches disagree with the game").
    /// <para>
    /// A stale switch is harmless under the reconcile rule and costs one extra flip — but only
    /// if the Commander can see it coming. This is the answer real aviation reached with
    /// annunciators rather than servos, for the same reason: showing the state is cheap and
    /// moving the switch is not.
    /// </para>
    /// </summary>
    public string? Disagrees { get; init; }
}

/// <summary>One reconcile waiting to be carried out. Nothing in Core presses anything.</summary>
/// <param name="Switch">The switch that asked, for the log and for the refusal.</param>
/// <param name="Label">How the action is named mid-sentence.</param>
/// <param name="Steps">Empty when this is only something to say.</param>
public readonly record struct PendingReconcile(
    string Switch,
    string Label,
    IReadOnlyList<InputStep> Steps,
    string? Say = null);

/// <summary>Everything the reconciler needs from outside, per poll.</summary>
public readonly record struct SwitchTick
{
    public required DateTimeOffset Now { get; init; }

    public required IReadOnlyList<HotasReading> Readings { get; init; }

    public required GameStatus Status { get; init; }

    public required EliteBinds Binds { get; init; }

    /// <summary>Whether the Commander has switched switch reconciling on, and key injection with it.</summary>
    public required bool Enabled { get; init; }

    public ControlContext Context => ControlContexts.Of(Status);
}

/// <summary>
/// The reconciler (list.md Phase 21, "A switch position means a state, not a press").
/// <para>
/// <b>The flip is the question and the game's own status is the answer.</b> On a flip, d47 asks
/// whether Elite is already in the state that position means, and presses the Commander's
/// binding once only if it is not. Gear already down from a voice command and the switch moved
/// to down does nothing at all, which is the entire point — every existing remapper is
/// edge-triggered and blind, and that is why a maintained switch desyncs the first time the game
/// changes its own mind on docking or a relog.
/// </para>
/// <para>
/// <b>Between flips nothing is touched</b>, so voice, the game's own automation and the switch
/// never fight. A switch that is where it was on the last poll produces no press, no matter how
/// far the game has drifted from it — the drift is shown (<see cref="SwitchState.Disagrees"/>)
/// rather than corrected.
/// </para>
/// <para>
/// <b>This is not an autonomous action.</b> The Commander flipping a switch is the Commander
/// asking, which is why it is not on the autonomous runner and not gated by its settings.
/// </para>
/// <para>
/// Reachable from the panel only — never from the tool surface, because a hostile in-game
/// message must not be able to remap a switch (architecture.md §7).
/// </para>
/// </summary>
public sealed class SwitchReconciler(ILogger<SwitchReconciler> logger)
{
    /// <summary>
    /// How long after a reconcile an unexplained change back counts as something else driving the
    /// same action. Long enough to cover the press, Status.json being rewritten and the tick that
    /// reads it; short enough that a change the Commander made themselves half a minute later is
    /// not blamed on a phantom binding.
    /// </summary>
    public static readonly TimeSpan ContestWindow = TimeSpan.FromSeconds(4);

    private readonly ConcurrentQueue<PendingReconcile> _pending = new();

    /// <summary>
    /// Guards the three dictionaries below. They are written from the tick thread and reached
    /// from the UI thread by <see cref="Resume"/> — the panel's button — and a Dictionary written
    /// from two threads at once does not merely lose an entry, it corrupts. The queue and
    /// <see cref="States"/> need no lock: one is concurrent and the other is a whole-list
    /// reference swap.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>Where each switch was on the last poll that named a position. The flip detector.</summary>
    private readonly Dictionary<string, SwitchPosition> _last = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Switches d47 has stopped reconciling, and why. Runtime only — see below.</summary>
    private readonly Dictionary<string, string> _paused = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Watch> _watching = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<SwitchState> _states = [];

    /// <summary>Every mapped switch as it stands. The panel, the headset and the settings row read this.</summary>
    public IReadOnlyList<SwitchState> States => _states;

    /// <summary>The switches that currently sit against the game's state. The annunciator.</summary>
    public IReadOnlyList<SwitchState> Disagreeing =>
        [.. _states.Where(state => state.Disagrees is not null)];

    /// <summary>
    /// Un-pauses a switch. The panel offers this; nothing else does.
    /// <para>
    /// A pause is held here and is gone at the next start, deliberately. A stored pause would
    /// outlive the leftover binding that caused it and would need its own way to be cleared — a
    /// setting nobody knows exists, silently not reconciling a switch that has worked for a
    /// month. Re-detecting costs one flip and says so out loud.
    /// </para>
    /// </summary>
    public void Resume(string name)
    {
        lock (_gate)
        {
            _paused.Remove(name);
            _watching.Remove(name);

            // The switch is wherever it is; the next flip is the next question. Forgetting where
            // it was means resuming cannot itself press anything.
            _last.Remove(name);
        }
    }

    public bool IsPaused(string name)
    {
        lock (_gate)
        {
            return _paused.ContainsKey(name);
        }
    }

    /// <summary>
    /// One poll. Decides, and queues; nothing here sends, because the tick is synchronous and
    /// a key press is not (see <c>TickLoop</c>).
    /// </summary>
    public void Poll(SwitchTick tick, IReadOnlyList<SwitchMapping> mappings)
    {
        var states = new List<SwitchState>(mappings.Count);

        lock (_gate)
        {
            foreach (var mapping in mappings)
            {
                try
                {
                    states.Add(Examine(tick, mapping));
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
        // Item 4, and the whole of it. The mapping is keyed on the NonRoamableId, so a throttle
        // whose 4x32 mode has been changed is simply not here — and the honest answer is to ask
        // for a reassignment rather than to drive whatever now sits at that button index.
        if (tick.Readings.FirstOrDefault(reading => reading.Id == mapping.DeviceId) is not { } reading)
        {
            _last.Remove(mapping.Name);
            _watching.Remove(mapping.Name);

            return new SwitchState
            {
                Name = mapping.Name,
                Health = tick.Readings.Count == 0 ? SwitchHealth.Waiting : SwitchHealth.Reassign,
                Note = tick.Readings.Count == 0
                    ? "D47 is still finding your controllers."
                    : "This switch was learned on a device D47 can no longer find, so it is doing nothing. "
                      + "Assign it again. Turning 4x32 mode on or off does this: it renumbers every button "
                      + $"on the throttle, so the mapping is retired rather than pointed at the wrong one ({mapping.Device}).",
            };
        }

        var position = mapping.At(reading);
        var flipped = position is not null && Flipped(mapping.Name, position);

        // Watched before the flip is acted on, so a second flip inside the window ends the watch
        // rather than being blamed for what the first one started — and before the pause is read
        // below, so a switch paused on this poll reports it on this poll. A state list one poll
        // behind the pause is a panel that says Ready about a switch d47 has already given up on.
        //
        // Safe against a paused switch: pausing removes the watch, so this returns immediately.
        Watching(tick, mapping, flipped);

        if (_paused.TryGetValue(mapping.Name, out var why))
        {
            return new SwitchState
            {
                Name = mapping.Name,
                Health = SwitchHealth.Paused,
                Position = position,
                Note = why,
            };
        }

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

        var state = Reconcile(tick, mapping, position, flipped);

        return state with { Disagrees = Disagreement(tick, position) };
    }

    /// <summary>
    /// Whether this poll is a flip. Returns false the first time a switch is seen, which is what
    /// keeps startup — and a device coming back after a reconnect — from pressing anything: d47
    /// learns where the switch is rather than acting on finding it there.
    /// </summary>
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

    private SwitchState Reconcile(SwitchTick tick, SwitchMapping mapping, SwitchPosition position, bool flipped)
    {
        var ready = new SwitchState { Name = mapping.Name, Health = SwitchHealth.Ready, Position = position };

        if (!tick.Enabled)
        {
            return ready with
            {
                Health = SwitchHealth.Off,
                Note = "Switch reconciling is off. Turn it on to let this switch operate the ship.",
            };
        }

        if (position.Action is not { Length: > 0 } id || GameActions.Find(id) is not { } action)
        {
            return ready with { Note = $"{position.Describe()} means nothing." };
        }

        var reach = ActionReachability.Resolve(action, tick.Binds, tick.Context);

        if (!reach.IsOffered)
        {
            // The refusal carries the reason, for the same reason the tool path's does: the
            // audience is HOTAS-heavy, so an action bound only to a stick is the common case,
            // and it fails as silence unless something says why.
            return ready with { Note = reach.Reason };
        }

        if (!flipped)
        {
            // Between flips nothing is touched. This is the line that keeps voice, the game's
            // own automation and the switch from ever fighting.
            return ready;
        }

        if (action.AlreadyIn(position.State, tick.Status) == true)
        {
            logger.LogInformation(
                "Switch {Name} moved to {Position}; {Action} is already there, so nothing was pressed",
                mapping.Name,
                position.Describe(),
                action.Id);

            return ready;
        }

        logger.LogInformation(
            "Switch {Name} moved to {Position}; pressing {Gesture} for {Action}",
            mapping.Name,
            position.Describe(),
            reach.Binding!.Gesture(),
            action.Id);

        _pending.Enqueue(new PendingReconcile(mapping.Name, action.Label, InputSequence.Tap(reach.Binding!)));

        // Item 5's watch starts here, with the state the press was for and the mode it was made
        // in. Both are needed: one to notice the change back, the other to know it was explained.
        _watching[mapping.Name] = new Watch(action, position.State, tick.Now + ContestWindow, tick.Context);

        return ready;
    }

    /// <summary>
    /// Item 5. Detecting the cause statically is not possible — with SimApp Pro or Joystick
    /// Gremlin in play, Elite's binds name a vJoy device that cannot be traced back to the
    /// physical switch — so this watches for the symptom instead, which also catches the leftover
    /// binding and the case nobody has thought of.
    /// </summary>
    private void Watching(SwitchTick tick, SwitchMapping mapping, bool flipped)
    {
        if (!_watching.TryGetValue(mapping.Name, out var watch))
        {
            return;
        }

        // A mode change explains a change back, and one of them is not hypothetical: hardpoints
        // retract by themselves on entering supercruise. The game changing its own mind during a
        // mode change is the game doing its job, not a second binding.
        if (flipped || tick.Now > watch.Until || tick.Context != watch.Context)
        {
            _watching.Remove(mapping.Name);
            return;
        }

        var there = watch.Action.AlreadyIn(watch.State, tick.Status);

        if (there == true)
        {
            _watching[mapping.Name] = watch with { Arrived = true };
            return;
        }

        if (there != false || !watch.Arrived)
        {
            return;
        }

        var why = $"Something else is bound to {watch.Action.Label}: it went back on its own right after D47 "
                  + $"set it. D47 has stopped reconciling {mapping.Name} rather than fight it.";

        _paused[mapping.Name] = why;
        _watching.Remove(mapping.Name);

        logger.LogWarning("Switch {Name} contested on {Action}; reconciling paused", mapping.Name, watch.Action.Id);

        _pending.Enqueue(new PendingReconcile(mapping.Name, watch.Action.Label, [], why));
    }

    /// <summary>
    /// Whether the position the switch is sitting in asks for a state the game is not in. Only
    /// where the action applies in this mode — a gear switch is not disagreeing with anything
    /// while the Commander is on foot.
    /// </summary>
    private static string? Disagreement(SwitchTick tick, SwitchPosition position)
    {
        if (position.Action is not { Length: > 0 } id || GameActions.Find(id) is not { } action)
        {
            return null;
        }

        if ((action.Contexts & tick.Context) == 0)
        {
            return null;
        }

        return action.AlreadyIn(position.State, tick.Status) == false
            ? $"{action.Label} is {(position.State == DesiredState.On ? "off" : "on")}"
            : null;
    }

    private readonly record struct Watch(
        GameAction Action,
        DesiredState State,
        DateTimeOffset Until,
        ControlContext Context,
        bool Arrived = false);
}
