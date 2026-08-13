using D47.Core.Input;

namespace D47.Core.Actions;

/// <summary>
/// One step of a macro: an action from the allowlist, the state to leave it in, and how long to
/// wait afterwards.
/// </summary>
/// <param name="Action">An id from <see cref="GameActions"/>. Anything else is rejected on load.</param>
/// <param name="State">on, off, or toggle.</param>
/// <param name="PauseMs">
/// Milliseconds to wait after this step. The reason a macro is not just a list of keys: Elite's
/// panels animate, and the second keystroke of "open the right panel and go down twice" arrives
/// before the panel exists unless something waits.
/// </param>
public sealed record MacroStep(string Action, DesiredState State = DesiredState.Toggle, int PauseMs = 250)
{
    /// <summary>
    /// The longest a single step may wait. A macro is held input, and a typo of an extra zero
    /// would leave d47 holding the Commander's controls for nearly three hours.
    /// </summary>
    public const int MaxPauseMs = 10_000;
}

/// <summary>A named sequence the Commander authored.</summary>
public sealed record Macro
{
    public required string Name { get; init; }

    public required IReadOnlyList<MacroStep> Steps { get; init; }

    /// <summary>
    /// The most steps one macro may hold. Not a technical limit — a bound on how long d47 can
    /// be driving the ship off one sentence.
    /// </summary>
    public const int MaxSteps = 20;
}

/// <summary>Why a macro was refused, in words that name the macro and the problem.</summary>
public sealed record MacroProblem(string Name, string Reason);

/// <summary>
/// Checking a macro against the closed vocabularies before it is allowed to exist (list.md
/// Phase 10, "validated against closed vocabularies and the action allowlist").
/// <para>
/// <b>Authoring is the one input whose vocabulary cannot be closed in advance</b>, which is why
/// the checklist exempts it from "every setting can be set by voice". What can be closed is
/// everything a macro is made of: every step names an action from the same catalogue the tools
/// use, and nothing else is expressible. A macro cannot press a key that is not already
/// reachable by asking for it out loud.
/// </para>
/// </summary>
public static class MacroValidation
{
    /// <summary>
    /// A macro name has to be sayable and has to not collide with anything already meaningful.
    /// Letters, digits and single spaces — no punctuation, because a name the Commander cannot
    /// pronounce is a macro they cannot run.
    /// </summary>
    public const int MaxNameLength = 40;

    /// <summary>
    /// Checks one macro. Returns null when it is fine, or the reason it is not.
    /// <para>
    /// Reserved phrases are refused rather than shadowed. A macro called "gear down" would take
    /// over a phrase that already means something, and the Commander would have no way to tell
    /// which one ran.
    /// </para>
    /// </summary>
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
                // The catalogue carries the fire groups for the arrival honk. A macro is
                // authored text, and authored text must not be a way around "the model never
                // gets to open fire".
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
