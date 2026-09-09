using D47.Core.Callouts;
using D47.Core.Input;

namespace D47.Core.Actions;

/// <summary>
/// What an autonomous action decided to do this tick: a sequence to send, and a sentence explaining
/// itself.
/// </summary>
/// <param name="Steps">What to press.</param>
/// <param name="Say">What to say about it, or null to act silently.</param>
public readonly record struct AutonomousDecision(IReadOnlyList<InputStep> Steps, string? Say = null)
{
    public static readonly AutonomousDecision Nothing = new([]);

    public bool Acts => Steps.Count > 0;
}

/// <summary>
/// One thing d47 does to the game with nobody asking (Phase 10, "Autonomous actions are opt-in per
/// action").
/// </summary>
public interface IAutonomousAction
{
    /// <summary>Stable id.</summary>
    string Id { get; }

    /// <summary>How a Commander hears it named.</summary>
    string Label { get; }

    /// <summary>Whether the Commander has switched this one on.</summary>
    bool IsEnabled { get; }

    /// <summary>Anything to do this tick.</summary>
    AutonomousDecision Examine(CalloutContext context);
}
