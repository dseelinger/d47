using D47.Core.Callouts;
using D47.Core.Input;

namespace D47.Core.Actions;

/// <summary>
/// What an autonomous action decided to do this tick: a sequence to send, and a sentence
/// explaining itself. Either may be absent.
/// </summary>
/// <param name="Steps">What to press. Empty means it decided not to act.</param>
/// <param name="Say">
/// What to say about it, or null to act silently. An autonomous action that fails is the case
/// that most needs a sentence: the Commander did not ask for it, so they have no reason to be
/// watching for it to work.
/// </param>
public readonly record struct AutonomousDecision(IReadOnlyList<InputStep> Steps, string? Say = null)
{
    public static readonly AutonomousDecision Nothing = new([]);

    public bool Acts => Steps.Count > 0;
}

/// <summary>
/// One thing d47 does to the game with nobody asking (Phase 10, "Autonomous actions
/// are opt-in per action").
/// <para>
/// <b>This is a category, and the category has its rule before it has a second member.</b>
/// Every other capability acts on something the Commander said, and the sentence they said is
/// the consent. An action that fires a game input on a journal event has no such sentence
/// behind it, so it needs a different kind of consent: each one is off by default and enabled
/// on its own settings row, protected from the model like everything else that reaches the
/// keyboard.
/// </para>
/// <para>
/// Deciding is separate from sending for the reason <c>TickLoop</c> gives: the tick is
/// synchronous and must not block, and the honk holds a key for six seconds. An implementation
/// returns what it would send; the runner does the awaiting somewhere else.
/// </para>
/// </summary>
public interface IAutonomousAction
{
    /// <summary>Stable id. Used for the settings row, for logging, and for nothing else.</summary>
    string Id { get; }

    /// <summary>How a Commander hears it named.</summary>
    string Label { get; }

    /// <summary>Whether the Commander has switched this one on. Off until they do.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Anything to do this tick. Returning nothing is the overwhelmingly common case; this
    /// runs ten times a second.
    /// <para>
    /// Implementations must fold the priming tick into their state and act on none of it. The
    /// startup tick replays the whole journal backlog, and a honk for every system the
    /// Commander visited this afternoon is a worse bug than never honking at all.
    /// </para>
    /// </summary>
    AutonomousDecision Examine(CalloutContext context);
}
