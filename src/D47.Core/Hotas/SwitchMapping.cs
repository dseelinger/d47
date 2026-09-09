using D47.Core.Input;

namespace D47.Core.Hotas;

/// <summary>One position of one switch, and what the Commander means by it.</summary>
/// <param name="Button">The button held at this position, or null when none is.</param>
/// <param name="Action">
/// The <see cref="GameActions"/> id this position asks for, or null for a position that means nothing —
/// the centre detent of a three-position switch used as two states.
/// </param>
/// <param name="State">What the position means.</param>
/// <param name="Destination">
/// The page of d47's own panel this position asks for, as the key of a root some surface registered —
/// <c>transcript.technical</c> — or null (Phase 46).
/// </param>
public sealed record SwitchPosition(
    int? Button,
    string? Action = null,
    DesiredState State = DesiredState.On,
    string? Destination = null)
{
    /// <summary>How the position reads in a report or on the panel.</summary>
    public string Describe() => Button is { } button ? $"button {button}" : "nothing held";

    public bool IsAssigned => ReachesTheGame || ReachesThePanel;

    /// <summary>Whether this position can end in a key press.</summary>
    public bool ReachesTheGame => Action is { Length: > 0 };

    /// <summary>Whether this position moves d47's own panel.</summary>
    public bool ReachesThePanel => Destination is { Length: > 0 };
}

/// <summary>
/// One switch the Commander walked, and what each of its positions does (Phase 21, items 2 and 4).
/// </summary>
public sealed record SwitchMapping
{
    /// <summary>What the Commander calls it.</summary>
    public required string Name { get; init; }

    /// <summary>The <c>NonRoamableId</c> the walk happened on.</summary>
    public required string DeviceId { get; init; }

    /// <summary>What the device looked like when the walk happened, for the row and the report.</summary>
    public string Device { get; init; } = string.Empty;

    public required IReadOnlyList<SwitchPosition> Positions { get; init; }

    /// <summary>The modes this switch is worth acting in, taken from the actions its positions name.</summary>
    public ControlContext Contexts =>
        Positions
            .Select(position => position.Action is { } id ? GameActions.Find(id) : null)
            .Where(action => action is not null)
            .Aggregate(ControlContext.None, (all, action) => all | action!.Contexts);

    /// <summary>The position this reading is at, or null when it is at none of them.</summary>
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

        // Nothing held.
        return found ?? Positions.FirstOrDefault(position => position.Button is null);
    }

    public bool IsAssigned => Positions.Any(position => position.IsAssigned);
}

/// <summary>Why a stored switch was refused, in words that name it and the problem.</summary>
public sealed record SwitchProblem(string Name, string Reason);

/// <summary>Checking a switch against the closed vocabularies before it is allowed to exist.</summary>
public static class SwitchValidation
{
    public const int MaxNameLength = 40;

    /// <summary>The most switches one Commander may map.</summary>
    public const int MaxSwitches = 32;

    /// <summary>The most positions one switch may have.</summary>
    public const int MaxPositions = 12;

    /// <summary>The longest root key a destination may carry.</summary>
    public const int MaxDestinationLength = 80;

    /// <summary>
    /// Every action a switch position may name: the ones Elite reports the state of, and no others.
    /// </summary>
    public static IReadOnlyList<GameAction> Assignable { get; } =
        [.. GameActions.All.Where(action => action.Reports is not null)];

    public static bool CanAssign(string actionId) =>
        Assignable.Any(action => string.Equals(action.Id, actionId, StringComparison.Ordinal));

    /// <summary>Checks one switch.</summary>
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

        // Two positions that read the same button are indistinguishable at reconcile time, so one of them
        // would silently never happen.
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

            if (position.ReachesThePanel)
            {
                // One meaning per position.
                if (position.ReachesTheGame)
                {
                    return "A position means one thing: an action in Elite or a page of D47's panel, not both.";
                }

                if (position.Destination!.Length > MaxDestinationLength)
                {
                    return $"That destination is longer than {MaxDestinationLength} characters.";
                }

                // Which pages exist is known only to the surfaces that registered them, so a destination is
                // checked against them at reconcile time and the row says so — the same shape as an action
                // bound to no key.
                continue;
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
