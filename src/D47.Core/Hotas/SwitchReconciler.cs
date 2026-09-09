using System.Collections.Concurrent;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Hotas;

/// <summary>What a mapped switch is currently able to do, which is what the panel shows.</summary>
public enum SwitchHealth
{
    /// <summary>Read, mapped, and reconciling.</summary>
    Ready,

    /// <summary>The device it was learned on is not there.</summary>
    Reassign,

    /// <summary>Something else is driving the same action, so d47 has stopped touching it.</summary>
    Paused,

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
}

/// <summary>One reconcile waiting to be carried out.</summary>
/// <param name="Switch">The switch that asked, for the log and for the refusal.</param>
/// <param name="Label">How the action — or the page — is named mid-sentence.</param>
/// <param name="Steps">Empty when this is only something to say, or only somewhere to go.</param>
/// <param name="Destination">
/// The root key the host is to put the panel on, for a position that names a page rather than an action
/// (Phase 46).
/// </param>
public readonly record struct PendingReconcile(
    string Switch,
    string Label,
    IReadOnlyList<InputStep> Steps,
    string? Say = null,
    string? Destination = null);

/// <summary>Everything the reconciler needs from outside, per poll.</summary>
public readonly record struct SwitchTick
{
    public required DateTimeOffset Now { get; init; }

    public required IReadOnlyList<HotasReading> Readings { get; init; }

    public required GameStatus Status { get; init; }

    public required EliteBinds Binds { get; init; }

    /// <summary>Whether the Commander has switched switch reconciling on, and key injection with it.</summary>
    public required bool Enabled { get; init; }

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
    /// The root key the panel is showing, or null when d47 cannot say — no surface is up, or two
    /// surfaces are showing different things.
    /// </summary>
    public string? Showing { get; init; }

    public ControlContext Context => ControlContexts.Of(Status);
}

/// <summary>The reconciler (Phase 21, "A switch position means a state, not a press").</summary>
public sealed class SwitchReconciler(ILogger<SwitchReconciler> logger)
{
    /// <summary>
    /// How long after a reconcile an unexplained change back counts as something else driving the same
    /// action.
    /// </summary>
    public static readonly TimeSpan ContestWindow = TimeSpan.FromSeconds(4);

    private readonly ConcurrentQueue<PendingReconcile> _pending = new();

    /// <summary>Guards the three dictionaries below.</summary>
    private readonly Lock _gate = new();

    /// <summary>Where each switch was on the last poll that named a position.</summary>
    private readonly Dictionary<string, SwitchPosition> _last = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Switches d47 has stopped reconciling, and why.</summary>
    private readonly Dictionary<string, string> _paused = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Watch> _watching = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<SwitchState> _states = [];

    /// <summary>Every mapped switch as it stands.</summary>
    public IReadOnlyList<SwitchState> States => _states;

    /// <summary>The switches that currently sit against the game's state.</summary>
    public IReadOnlyList<SwitchState> Disagreeing =>
        [.. _states.Where(state => state.Disagrees is not null)];

    /// <summary>Un-pauses a switch.</summary>
    public void Resume(string name)
    {
        lock (_gate)
        {
            _paused.Remove(name);
            _watching.Remove(name);

            // The switch is wherever it is; the next flip is the next question.
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

    /// <summary>One poll.</summary>
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
        // Item 4, and the whole of it.
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

        // Watched before the flip is acted on, so a second flip inside the window ends the watch rather than
        // being blamed for what the first one started — and before the pause is read below, so a switch
        // paused on this poll reports it on this poll.
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

        return state with
        {
            Disagrees = Disagreement(tick, position),
            Collides = Collision(tick, mapping, position),
        };
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

    private SwitchState Reconcile(SwitchTick tick, SwitchMapping mapping, SwitchPosition position, bool flipped)
    {
        var ready = new SwitchState { Name = mapping.Name, Health = SwitchHealth.Ready, Position = position };

        if (position.Destination is { Length: > 0 } destination)
        {
            return ReconcilePanel(tick, mapping, position, destination, flipped, ready);
        }

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
            // The refusal carries the reason, for the same reason the tool path's does: the audience is
            // HOTAS-heavy, so an action bound only to a stick is the common case, and it fails as silence
            // unless something says why.
            return ready with { Note = reach.Reason };
        }

        if (!flipped)
        {
            // Between flips nothing is touched.
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

        // Item 5's watch starts here, with the state the press was for and the mode it was made in.
        _watching[mapping.Name] = new Watch(action, position.State, tick.Now + ContestWindow, tick.Context);

        return ready;
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

        if (string.Equals(tick.Showing, destination, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Switch {Name} moved to {Position}; the panel is already on {Page}, so nothing moved",
                mapping.Name,
                position.Describe(),
                page.Root.Word);

            return ready;
        }

        logger.LogInformation(
            "Switch {Name} moved to {Position}; showing {Page}",
            mapping.Name,
            position.Describe(),
            page.Root.Word);

        _pending.Enqueue(new PendingReconcile(mapping.Name, page.Root.Word, [], Destination: destination));

        return ready;
    }

    private static PanelDestination? Page(SwitchTick tick, string key) =>
        tick.Destinations.FirstOrDefault(page => string.Equals(page.Root.Key, key, StringComparison.Ordinal));

    /// <summary>Item 5.</summary>
    private void Watching(SwitchTick tick, SwitchMapping mapping, bool flipped)
    {
        if (!_watching.TryGetValue(mapping.Name, out var watch))
        {
            return;
        }

        // A mode change explains a change back, and one of them is not hypothetical: hardpoints retract by
        // themselves on entering supercruise.
        if (flipped || tick.Context != watch.Context)
        {
            _watching.Remove(mapping.Name);
            return;
        }

        if (tick.Now > watch.Until)
        {
            _watching.Remove(mapping.Name);

            // The press that never took, said out loud (#147).
            if (!watch.Arrived && watch.Action.AlreadyIn(watch.State, tick.Status) == false)
            {
                var silent = $"I set {watch.Action.Label} from {mapping.Name} and it did not take. "
                             + "Something else may be bound to the same button.";

                logger.LogWarning(
                    "Switch {Name} pressed {Action} and the state never arrived",
                    mapping.Name,
                    watch.Action.Id);

                _pending.Enqueue(new PendingReconcile(mapping.Name, watch.Action.Label, [], silent));
            }

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
    private static string? Disagreement(SwitchTick tick, SwitchPosition position)
    {
        if (position.Destination is { Length: > 0 } destination)
        {
            // Only when d47 can say what the panel is showing, and only against a page that exists — a key
            // nothing answers to is reported on the row, not annunciated.
            if (tick.Showing is not { } showing
                || string.Equals(showing, destination, StringComparison.Ordinal)
                || Page(tick, destination) is null)
            {
                return null;
            }

            return $"the panel is on {Page(tick, showing)?.Root.Word ?? showing}";
        }

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
