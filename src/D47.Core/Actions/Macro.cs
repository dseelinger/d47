using D47.Core.Conversation;
using D47.Core.Input;

namespace D47.Core.Actions;

/// <summary>
/// One step of a macro: an action from the allowlist and the state to leave it in, or a key chord, and
/// how long to wait afterwards.
/// </summary>
/// <param name="Action">An id from <see cref="GameActions"/>; empty for a chord step.</param>
/// <param name="State">on, off, or toggle; ignored for a chord step.</param>
/// <param name="PauseMs">Milliseconds to wait after this step.</param>
/// <param name="Chord">The keys a chord step presses; null for an action step.</param>
public sealed record MacroStep(
    string Action = "",
    DesiredState State = DesiredState.Toggle,
    int PauseMs = 250,
    KeyChord? Chord = null)
{
    public bool IsChord => Chord is not null;

    /// <summary>The longest a single step may wait.</summary>
    public const int MaxPauseMs = 10_000;
}

/// <summary>A named sequence the Commander authored.</summary>
public sealed record Macro
{
    public required string Name { get; init; }

    public required IReadOnlyList<MacroStep> Steps { get; init; }

    /// <summary>True when a step presses a key chord, which only the Commander may run.</summary>
    public bool HoldsChord => Steps.Any(step => step.IsChord);

    /// <summary>The most steps one macro may hold.</summary>
    public const int MaxSteps = 20;
}

/// <summary>Why a macro was refused, in words that name the macro and the problem.</summary>
public sealed record MacroProblem(string Name, string Reason);

/// <summary>
/// Checking a macro against the closed vocabularies before it is allowed to exist (Phase 10, "validated
/// against closed vocabularies and the action allowlist").
/// </summary>
public static class MacroValidation
{
    /// <summary>A macro name has to be sayable and has to not collide with anything already meaningful.</summary>
    public const int MaxNameLength = 40;

    /// <summary>Checks one macro.</summary>
    public static string? Problem(Macro macro, IReadOnlyCollection<string> reservedPhrases)
    {
        var name = macro.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            return "A macro needs a name.";
        }

        if (name.Length > MaxNameLength)
        {
            return $"\"{name}\" is longer than {MaxNameLength} characters.";
        }

        if (!name.All(c => char.IsLetterOrDigit(c) || c == ' '))
        {
            return $"\"{name}\" has punctuation in it. Macro names are words, so they can be said out loud.";
        }

        var said = KeywordRouter.Folded(name);

        if (reservedPhrases.Any(phrase => string.Equals(KeywordRouter.Folded(phrase), said, StringComparison.OrdinalIgnoreCase)))
        {
            return $"\"{name}\" is already a command D47 understands, so a macro cannot take that name.";
        }

        if (macro.Steps.Count == 0)
        {
            return $"\"{name}\" has no steps.";
        }

        if (macro.Steps.Count > Macro.MaxSteps)
        {
            return $"\"{name}\" has {macro.Steps.Count} steps; the most is {Macro.MaxSteps}.";
        }

        foreach (var step in macro.Steps)
        {
            if (step.Chord is { } chord)
            {
                if (!string.IsNullOrEmpty(step.Action))
                {
                    return $"\"{name}\" has a step with both an action and a key chord; a step has one or the other.";
                }

                if (KeyChord.Problem(chord) is { } refused)
                {
                    return $"\"{name}\" has a key chord that is refused: {refused}";
                }

                if (step.PauseMs is < 0 or > MacroStep.MaxPauseMs)
                {
                    return $"\"{name}\" waits {step.PauseMs} ms somewhere; the most is {MacroStep.MaxPauseMs}.";
                }

                continue;
            }

            if (GameActions.Find(step.Action) is null)
            {
                return $"\"{name}\" uses an action D47 does not have: {step.Action}.";
            }

            if (GameActions.Find(step.Action)!.Group == GameActions.Weapons)
            {
                // The catalogue carries the fire groups for the arrival honk.
                return $"\"{name}\" tries to fire weapons, which macros are not allowed to do.";
            }

            if (step.PauseMs is < 0 or > MacroStep.MaxPauseMs)
            {
                return $"\"{name}\" waits {step.PauseMs} ms somewhere; the most is {MacroStep.MaxPauseMs}.";
            }
        }

        return null;
    }
}
