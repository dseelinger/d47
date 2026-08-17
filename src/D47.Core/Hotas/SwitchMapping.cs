using D47.Core.Input;

namespace D47.Core.Hotas;

/// <summary>
/// One position of one switch, and what the Commander means by it.
/// <para>
/// <b><see cref="Button"/> is nullable, and that is the whole point.</b> Every maintained switch
/// on the bench held exactly one button at all times including at centre, which would make this
/// an <c>int</c> — but nothing in <c>RawGameController</c> requires it, and a three-position
/// switch reporting nothing held at centre is an equally legitimate design that one vendor's
/// hardware cannot rule out. So *nothing held* is a position like any other, named by its
/// absence. A reconciler carrying the bench's encoding as a constant would read that centre as
/// <em>still where it last was</em> and drive the wrong state silently, which is the exact
/// failure this phase exists to prevent (list.md Phase 21, item 3).
/// </para>
/// </summary>
/// <param name="Button">The button held at this position, or null when none is.</param>
/// <param name="Action">
/// The <see cref="GameActions"/> id this position asks for, or null for a position that means
/// nothing — the centre detent of a three-position switch used as two states.
/// </param>
/// <param name="State">
/// What the position means. Never <see cref="DesiredState.Toggle"/>: a position is a state, and a
/// position that toggled would be the edge-triggered remapper this phase replaces.
/// </param>
public sealed record SwitchPosition(int? Button, string? Action = null, DesiredState State = DesiredState.On)
{
    /// <summary>How the position reads in a report or on the panel.</summary>
    public string Describe() => Button is { } button ? $"button {button}" : "nothing held";

    public bool IsAssigned => Action is { Length: > 0 };
}

/// <summary>
/// One switch the Commander walked, and what each of its positions does (list.md Phase 21,
/// items 2 and 4).
/// </summary>
public sealed record SwitchMapping
{
    /// <summary>What the Commander calls it. Display only — nothing is keyed on this.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The <c>NonRoamableId</c> the walk happened on. <b>Never VID+PID.</b> Turning 4x32 mode on
    /// or off renumbers every button on the throttle while leaving VID+PID identical, so a
    /// VID+PID key produces exactly the silent misfire this is meant to prevent, whereas the
    /// non-roamable id changes and the mapping fails closed (docs/spikes/hotas-switch-read.md,
    /// finding 7).
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// What the device looked like when the walk happened, for the row and the report. Carried
    /// because Windows reports <c>HID-compliant game controller</c> for everything, so without
    /// it a mapping that has failed closed cannot say <em>which</em> stick it was learned on.
    /// </summary>
    public string Device { get; init; } = string.Empty;

    public required IReadOnlyList<SwitchPosition> Positions { get; init; }

    /// <summary>
    /// The modes this switch is worth acting in, taken from the actions its positions name.
    /// Mode-gated by the same <see cref="ControlContext"/> that decides what is worth
    /// advertising — a gear switch flipped on foot is not a request to do anything.
    /// </summary>
    public ControlContext Contexts =>
        Positions
            .Select(position => position.Action is { } id ? GameActions.Find(id) : null)
            .Where(action => action is not null)
            .Aggregate(ControlContext.None, (all, action) => all | action!.Contexts);

    /// <summary>
    /// The position this reading is at, or null when it is at none of them.
    /// <para>
    /// Null is <em>unknown</em>, never <em>unchanged</em>. Two of the switch's own buttons held
    /// at once is not a position it was walked through, and neither is nothing-held on a switch
    /// whose walk found a button at every stop — both mean the reading cannot be trusted to name
    /// a state, and the reconciler leaves the game alone rather than picking the nearest guess.
    /// </para>
    /// </summary>
    public SwitchPosition? At(HotasReading reading)
    {
        SwitchPosition? found = null;

        foreach (var position in Positions)
        {
            if (position.Button is not { } button || !reading.IsHeld(button))
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = position;
        }

        // Nothing held. That is a position when the walk saw one, and unknown when it did not.
        return found ?? Positions.FirstOrDefault(position => position.Button is null);
    }

    public bool IsAssigned => Positions.Any(position => position.IsAssigned);
}

/// <summary>Why a stored switch was refused, in words that name it and the problem.</summary>
public sealed record SwitchProblem(string Name, string Reason);

/// <summary>
/// Checking a switch against the closed vocabularies before it is allowed to exist. The same
/// rule macros follow, and for the same reason: the file is hand-editable, so what it can say
/// has to be checked rather than trusted.
/// </summary>
public static class SwitchValidation
{
    public const int MaxNameLength = 40;

    /// <summary>
    /// The most switches one Commander may map. Not a technical limit — a bound on how much of
    /// the ship is being driven by things d47 is watching, and a number no real HOTAS approaches.
    /// </summary>
    public const int MaxSwitches = 32;

    /// <summary>The most positions one switch may have. A rotary is not a switch; a four-way is.</summary>
    public const int MaxPositions = 12;

    /// <summary>
    /// Every action a switch position may name: the ones Elite reports the state of, and no
    /// others.
    /// <para>
    /// This is the rule that keeps item 3 honest. An action with no reported flag cannot be
    /// asked <em>are you already there</em>, so a switch bound to it could only press — and a
    /// switch that presses is the edge-triggered remapper that desyncs the first time the game
    /// changes its own mind, which is the thing this phase exists to replace.
    /// </para>
    /// </summary>
    public static IReadOnlyList<GameAction> Assignable { get; } =
        [.. GameActions.All.Where(action => action.Reports is not null)];

    public static bool CanAssign(string actionId) =>
        Assignable.Any(action => string.Equals(action.Id, actionId, StringComparison.Ordinal));

    /// <summary>Checks one switch. Returns null when it is fine, or the reason it is not.</summary>
    public static string? Problem(SwitchMapping mapping)
    {
        var name = mapping.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            return "A switch needs a name.";
        }

        if (name.Length > MaxNameLength)
        {
            return $"That name is longer than {MaxNameLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(mapping.DeviceId))
        {
            return "A switch has to say which device it was learned on.";
        }

        if (mapping.Positions.Count < 2)
        {
            return "A switch needs at least two positions. Walk it through all of them.";
        }

        if (mapping.Positions.Count > MaxPositions)
        {
            return $"A switch may have at most {MaxPositions} positions.";
        }

        // Two positions that read the same button are indistinguishable at reconcile time, so
        // one of them would silently never happen.
        var buttons = mapping.Positions.Select(position => position.Button).ToList();

        if (buttons.Distinct().Count() != buttons.Count)
        {
            return "Two positions of that switch read the same button, so D47 cannot tell them apart.";
        }

        foreach (var position in mapping.Positions)
        {
            if (position.Button is < 0)
            {
                return "A position cannot name a negative button.";
            }

            if (position.Action is not { Length: > 0 } action)
            {
                continue;
            }

            if (GameActions.Find(action) is null)
            {
                return $"There is no action called '{action}'.";
            }

            if (!CanAssign(action))
            {
                return $"Elite does not report the state of '{action}', so a switch cannot mean it — "
                       + "it could only press it, which is the thing switches are being used to avoid.";
            }

            if (position.State == DesiredState.Toggle)
            {
                return "A switch position has to mean on or off. A position that toggled would be a press.";
            }
        }

        return null;
    }
}
