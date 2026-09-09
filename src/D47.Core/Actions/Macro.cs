using D47.Core.Input;

namespace D47.Core.Actions;

/// <summary>
/// One step of a macro: an action from the allowlist, the state to leave it in, and how long to wait
/// afterwards.
/// </summary>
/// <param name="Action">An id from <see cref="GameActions"/>.</param>
/// <param name="State">on, off, or toggle.</param>
/// <param name="PauseMs">Milliseconds to wait after this step.</param>
public sealed record MacroStep(string Action, DesiredState State = DesiredState.Toggle, int PauseMs = 250)
{
    /// <summary>The longest a single step may wait.</summary>
    public const int MaxPauseMs = 10_000;
}

/// <summary>A named sequence the Commander authored.</summary>
public sealed record Macro
{
    public required string Name { get; init; }

    public required IReadOnlyList<MacroStep> Steps { get; init; }

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

        if (reservedPhrases.Contains(name, StringComparer.OrdinalIgnoreCase))
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
