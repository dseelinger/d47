using D47.Core.Audio;

namespace D47.App.Panel;

/// <summary>
/// The speech loop, written onto the Technical page as it happens
/// (docs/plans/change-requests.md item 6).
/// <para>
/// <b>The page's premise was "the same conversation, with the diagnostics left in", and it
/// showed almost none of them.</b> Five things appended to it, all turn-level. Meanwhile the
/// loop that a Commander most often needs to see — heard you, transcribing, thinking, speaking —
/// reported itself only to a log file, where it is one subsystem among eight and interleaved
/// with everything else.
/// </para>
/// <para>
/// <b>Derived from <see cref="LoopState"/> rather than hand-written per call site.</b> The
/// request asked for hand-written stage lines, on the reasoning that log lines are phrased for a
/// log file — which is right — but a hand-maintained list is how this page got thin in the first
/// place. The loop already has an enum naming every stage it can be in and already raises an
/// event on entering one, so the list of stages is the enum: a new state is a compile error here
/// rather than a line nobody remembered to add.
/// </para>
/// <para>
/// <b>Appended, not updated in place.</b> "As they happen" is about when a line arrives, not
/// about a control that rewrites itself: this is a transcript, and its value is that the stage
/// before the failure is still on screen underneath it. The panel already has a control of the
/// other kind — the microphone indicator — and it answers a different question, which is what is
/// true right now rather than what happened.
/// </para>
/// </summary>
public sealed class SpeechLoopTrace(PanelViewModel panel, Func<DateTimeOffset>? now = null)
{
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.Now);

    private LoopState _last = LoopState.Idle;

    /// <summary>
    /// Writes a line for a state the loop has entered, or nothing where there is nothing worth
    /// saying.
    /// <para>
    /// Idle is the state the loop returns to after everything, so a line for it would double the
    /// length of the page to say "and then nothing was happening". The same state twice says
    /// nothing either — the loop can re-enter one, and a page repeating itself reads as a stall
    /// rather than as steady state.
    /// </para>
    /// </summary>
    public void Entered(LoopState state)
    {
        if (state == _last)
        {
            return;
        }

        _last = state;

        if (Describe(state) is not { } said)
        {
            return;
        }

        // The clock is read here, on the App side. Core owns the loop and Core reads no clock,
        // so the ordering these lines carry is added at the surface that already has one.
        panel.Append($"[{_now():HH:mm:ss}] {said}\n", TranscriptKind.Technical);
    }

    /// <summary>
    /// What a stage is called on a page a person reads. Deliberately not the log's wording: a log
    /// line is written for grepping and this is written for someone watching a turn go past.
    /// </summary>
    private static string? Describe(LoopState state) => state switch
    {
        LoopState.Listening => "Microphone open, listening.",
        LoopState.Transcribing => "Turning what you said into words.",
        LoopState.Thinking => "Working on an answer.",
        LoopState.Speaking => "Speaking the answer.",
        LoopState.Answered => "Answered.",
        LoopState.Unsure => "Answered, but unsure.",
        LoopState.Failed => "The response could not be completed.",

        // Idle. Nothing to say about the loop being at rest, and it is entered after every
        // single turn — the one state whose line would be pure volume.
        _ => null,
    };
}
